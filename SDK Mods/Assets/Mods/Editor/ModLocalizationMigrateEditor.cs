using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorKit.Scripts.Mods.Editor
{
    [CustomEditor(typeof(TextAsset))]
    public class ModLocalizationMigrateEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _baseEditor;

        private void OnEnable()
        {
            // Find the internal TextAssetInspector type
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.TextAssetInspector");
            if (type != null)
            {
                _baseEditor = CreateEditor(targets, type);
            }
        }

        private void OnDisable()
        {
            if (_baseEditor != null)
                DestroyImmediate(_baseEditor);
        }

        public override void OnInspectorGUI()
        {
            // Draw the default TextAsset inspector (text area/edit box)
            if (_baseEditor != null)
                _baseEditor.OnInspectorGUI();

            // 1. Check if the filename matches 'localization'
            string assetPath = AssetDatabase.GetAssetPath(target);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);

            // StringComparison makes it case-insensitive if desired
            if (fileName.Equals("localization", StringComparison.OrdinalIgnoreCase))
            {
                DrawCustomUI();
            }
        }

        private void DrawCustomUI()
        {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;

            // Add your custom buttons or UI here
            if (GUILayout.Button("Migrate localization to Data Blocks!"))
            {
                DoMigration();
            }

            GUI.enabled = wasEnabled;
        }

        private readonly HashSet<string> _warnedLanguages = new HashSet<string>();
        private readonly Dictionary<string, TextDataBlock> _addedBlocks = new Dictionary<string, TextDataBlock>();
        
        private void DoMigration()
        {
            Debug.Log("Starting localization migration for " + target.name);
        
            _warnedLanguages.Clear();
            _addedBlocks.Clear();
            
            if (!ValidateDirectory(out string dataBlockDir)) return;
            if (!ReadFile(out string[] lines, out string[] header)) return;

            var languages = ScriptableData.GetDataBlocks<LanguageDataBlock>();
            if (languages == null || languages.Count == 0)
            {
                Debug.LogError("Localization migration failed! No language DataBlocks found!");
                return;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Split('\t');
                if (line.Length < 4) continue;
                
                string fileDirectory = null;
                TextDataBlock textBlock;

                bool isDesc = line[0].EndsWith("Desc");
                if (!isDesc)
                {
                    var key = line[0].Replace(':', '_');

                    fileDirectory = dataBlockDir;
                    if (key.Contains('/'))
                    {
                        var keyParts = key.Split('/');
                        fileDirectory = Path.Combine(keyParts.SkipLast(1).Prepend(dataBlockDir).ToArray());
                        key = keyParts.Last();
                    }

                    //Key	Type	Desc	English	German	Japanese	Korean	Spanish	Chinese (Simplified)	Thai

                    textBlock = CreateInstance<TextDataBlock>();

                    // Add all known languages
                    foreach (var language in languages)
                        textBlock.ValidateForLanguage(language);

                    SerializedObject so = new SerializedObject(textBlock);

                    so.FindProperty("m_Name").stringValue = key;
                    so.FindProperty("m_localizationHint").stringValue = line[2];

                    so.ApplyModifiedProperties();
                }
                else
                {
                    var baseKey = line[0][..^4];
                    if (!_addedBlocks.TryGetValue(baseKey, out textBlock))
                    {
                        Debug.LogWarning($"Failed to find DataBlock for {line[0]}. Is desc line coming before main one?");
                        continue;
                    }
                    
                    Debug.Log($"Will use {textBlock.name} as block for {line[0]}");
                }

                for (int j = 3; j < header.Length; j++)
                {
                    var languageName = header[j].Trim();
                    var value = line[j].Trim();

                    var languageEntry = languages.FirstOrDefault(block => block.name.Contains(languageName));
                    if (languageEntry == null)
                    {
                        WarnLanguageMissing(languageName);
                        continue;
                    }

                    if (isDesc)
                    {
                        var entry = textBlock.GetLocalized(languageEntry);
                        textBlock.EditorSetLocalization(languageEntry, entry.title, value);
                    }
                    else
                    {
                        textBlock.EditorSetLocalization(languageEntry, value, "");
                    }
                }

                SaveDataBlock(fileDirectory, textBlock, line[0]);
            }
            
            LanguageDataBlock primaryLanguage = LanguageDataBlock.GetPrimaryLanguage();
            
            foreach (var block in _addedBlocks.Values)
            {
                var primary = block.GetLocalized(primaryLanguage);
                block.MarkAsLocalized(primary);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Localization migration successfully finished!");
        }

        private void WarnLanguageMissing(string languageName)
        {
            if (_warnedLanguages.Contains(languageName)) return;
            
            Debug.LogWarning($"Failed to find language {languageName}!");
            _warnedLanguages.Add(languageName);
        }

        private void SaveDataBlock(
            string fileDirectory,
            TextDataBlock textBlock,
            string fullKey
        )
        {
            if (fileDirectory == null)
            {
                AssetDatabase.SaveAssetIfDirty(textBlock);
                return;
            }
            
            var absDirectory = Path.GetFullPath(fileDirectory);

            if (!Directory.Exists(absDirectory))
                Directory.CreateDirectory(absDirectory);

            var finalPath = Path.Combine(fileDirectory, $"{textBlock.name}.asset");
            Debug.Log($"Creating DataBlock at {finalPath}");
            AssetDatabase.CreateAsset(textBlock, finalPath);

            _addedBlocks.TryAdd(fullKey, textBlock);
        }

        private bool ReadFile(out string[] lines, out string[] header)
        {
            TextAsset textAsset = (TextAsset)target;
            string content = textAsset.text;

            lines = content.Split('\n');
            header = null;
            
            if (lines.Length < 2)
            {
                Debug.LogError("Localization migration failed! Localization file is empty or corrupted!");
                return false;
            }
            
            header = lines[0].Split('\t');

            if (header.Length < 4 ||
                header[0] != "Key" ||
                header[1] != "Type" ||
                header[2] != "Desc")
            {
                Debug.LogError("Localization migration failed! Localization file seems to be corrupted!");
                return false;
            }

            return true;
        }

        private bool ValidateDirectory(out string dataBlockDir)
        {
            dataBlockDir = null;
            
            string assetPath = AssetDatabase.GetAssetPath(target);
            string directory = Path.GetDirectoryName(assetPath);
            if (directory == null)
            {
                Debug.LogWarning("Failed to determine file directory!");
                return false;
            }
            
            string dataDir = Path.Combine(directory, "..", "Data");
            dataDir = Path.GetFullPath(dataDir).Replace('\\', '/');

            if (!Directory.Exists(dataDir))
            {
                Debug.LogError("Localization migration failed! DataBlock directory not found! Please create mod DataBlock directory!");
                return false;
            }
            
            dataBlockDir = "Assets" + dataDir[Application.dataPath.Length..];
            dataBlockDir = Path.Combine(dataBlockDir, "TextDataBlock");
            return true;
        }

        // Keep the original bottom preview window
        public override bool HasPreviewGUI() => _baseEditor != null && _baseEditor.HasPreviewGUI();
        public override void OnPreviewGUI(Rect r, GUIStyle background) => _baseEditor.OnPreviewGUI(r, background);
    }
}