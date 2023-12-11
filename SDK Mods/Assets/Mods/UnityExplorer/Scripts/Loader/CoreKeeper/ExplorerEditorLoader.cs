
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityExplorer.Loader.Standalone
{
    public class ExplorerEditorLoader : ExplorerStandalone
    {
        public new string ExplorerFolderName => $"{ExplorerCore.DEFAULT_EXPLORER_FOLDER_NAME}~";

        public static void Initialize()
        {
            Instance = new ExplorerEditorLoader();
            OnLog += LogHandler;
            Instance.configHandler = new StandaloneConfigHandler();

            ExplorerCore.Init(Instance);
        }

        static void LogHandler(string message, LogType logType)
        {
            Debug.unityLogger.Log(logType, "[Unity Explorer]", message);
        }

        protected override void CheckExplorerFolder()
        {
            if (explorerFolderDest == null)
                explorerFolderDest = Path.GetDirectoryName(Application.dataPath);
        }
    }
}
