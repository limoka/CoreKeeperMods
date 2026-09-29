using System;
using System.Collections.Generic;
using System.Text;
using ModReporter.Scripts;
using PugMod;
using UnityEngine;

namespace ModReporter.UI
{
    public class ModReporterWindow : MonoBehaviour
    {
        public GameObject rootContent;
        
        public Transform modListContent;
        public GameObject itemPrefab;

        public GameObject detailGo;
        
        public InfoField modNameField;
        public InfoField dependenciesField;
        public InfoField errorsField;
        public InfoField logField;

        private Dictionary<string, ModItem> _modItems = new Dictionary<string, ModItem>();
        private string currentViewingModName;

        public bool isVisible => rootContent.activeSelf;
        
        private void OnEnable()
        {
            ModReporterMod.modStateChanged += RefreshForMod;
        }

        private void OnDisable()
        {
            ModReporterMod.modStateChanged -= RefreshForMod;
        }

        public void Show()
        {
            rootContent.SetActive(true);
            Cursor.visible = true;
        }

        public void Hide()
        {
            rootContent.SetActive(false);
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Show();
            }
        }


        public void FillUp()
        {
            foreach (ModInfo mod in ModReporterMod.modInfos.Values)
            {
                var item = Instantiate(itemPrefab, modListContent);
                var modItem = item.GetComponent<ModItem>();
                modItem.SetState(mod.modName, mod.status, OnItemClicked);
                _modItems.Add(mod.modName, modItem);
            }
        }

        public void RefreshForMod(string modName)
        {
            if (_modItems.ContainsKey(modName))
            {
                ModItem modItem = _modItems[modName];
                
                ModInfo mod = ModReporterMod.modInfos[modName];
                
                modItem.SetState(mod.modName, mod.status, OnItemClicked);
                if (currentViewingModName.Equals(modName))
                {
                    OnItemClicked(modName);
                }
            }
        }

        public void OnItemClicked(string modName)
        {
            if (!string.IsNullOrEmpty(currentViewingModName))
            {
                _modItems[currentViewingModName].SetSelected(false);
                currentViewingModName = "";
            }
            
            if (!ModReporterMod.modInfos.ContainsKey(modName))
            {
                detailGo.SetActive(false);
                return;
            }

            var mod = ModReporterMod.modInfos[modName];
            
            modNameField.SetText(mod.modName);

            CollectDependencies(mod);
            CollectLogs(mod);
            
            detailGo.SetActive(true);
            _modItems[modName].SetSelected(true);
            currentViewingModName = modName;
        }

        private void CollectLogs(ModInfo mod)
        {
            StringBuilder logSb = new StringBuilder();
            StringBuilder errorSb = new StringBuilder();

            foreach (LogEntry log in mod.logEntries)
            {
                if (log.logType is LogType.Error or LogType.Warning)
                {
                    if (log.logType == LogType.Warning)
                    {
                        errorSb.Append("<color=\"yellow\">");
                        logSb.Append("<color=\"yellow\">");
                    }
                    else
                    {
                        errorSb.Append("<color=\"red\">");
                        logSb.Append("<color=\"red\">");
                    }

                    errorSb.Append(log.message);
                    logSb.Append(log.message);
                    errorSb.Append("</color>");
                    logSb.Append("</color>");
                    if (!string.IsNullOrEmpty(log.stacktrace))
                    {
                        errorSb.Append(":\n");
                        errorSb.Append(log.stacktrace);
                        errorSb.Append("\n");
                        
                        logSb.Append(":\n");
                        logSb.Append(log.stacktrace);
                        logSb.Append("\n");
                    }
                    else
                    {
                        errorSb.Append("\n");
                        logSb.Append("\n");
                    }
                }
                else
                {
                    logSb.Append(log.message);
                    logSb.Append("\n");
                }
            }

            errorsField.SetText(errorSb.ToString());
            logField.SetText(logSb.ToString());
        }

        private void CollectDependencies(ModInfo mod)
        {
            if (mod.dependenciesUnknown)
            {
                dependenciesField.SetText("(Unknown)");
                return;
            }
            StringBuilder sb = new StringBuilder();
            foreach (ModMetadata.Dependency dependency in mod.dependencies)
            {
                sb.Append(dependency.modName);
                sb.Append(", ");
            }

            dependenciesField.SetText(sb.ToString());
        }
    }
}