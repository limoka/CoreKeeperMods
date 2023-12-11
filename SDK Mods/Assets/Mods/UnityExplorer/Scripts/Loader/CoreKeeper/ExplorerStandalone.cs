
using System;
using System.IO;
using UnityEngine;
using UnityExplorer.Config;
using UnityExplorer.Loader.Standalone;
#if CPP
using UnhollowerRuntimeLib;
#endif

namespace UnityExplorer
{
	public class ExplorerStandalone : IExplorerLoader
    {
        public static ExplorerStandalone Instance { get; protected set; }

        /// <summary>
        /// Invoked whenever Explorer logs something. Subscribe to this to handle logging.
        /// </summary>
        public static event Action<string, LogType> OnLog;

        public ConfigHandler ConfigHandler => configHandler;
        internal StandaloneConfigHandler configHandler;

        public string ExplorerFolderName => ExplorerCore.DEFAULT_EXPLORER_FOLDER_NAME;
        public string ExplorerFolderDestination
        {
            get
            {
                CheckExplorerFolder();
                return explorerFolderDest;
            }
        }
        protected static string explorerFolderDest;
        
        Action<object> IExplorerLoader.OnLogMessage => log => { OnLog?.Invoke(log?.ToString() ?? "", LogType.Log); };
        Action<object> IExplorerLoader.OnLogWarning => log => { OnLog?.Invoke(log?.ToString() ?? "", LogType.Warning); };
        Action<object> IExplorerLoader.OnLogError   => log => { OnLog?.Invoke(log?.ToString() ?? "", LogType.Error); };

        /// <summary>
        /// Call this to initialize UnityExplorer without adding a log listener
        /// </summary>
        /// <returns>The new (or active, if one exists) instance of ExplorerStandalone.</returns>
        public static ExplorerStandalone CreateInstance() => CreateInstance(null);
        
        /// <summary>
        /// Call this to initialize UnityExplorer with the provided log listener
        /// </summary>
        /// <param name="logListener">Your log listener to handle UnityExplorer logs.</param>
        /// <returns>The new (or active, if one exists) instance of ExplorerStandalone.</returns>
        public static ExplorerStandalone CreateInstance(Action<string, LogType> logListener)
        {
            if (Instance != null)
                return Instance;

            var instance = new ExplorerStandalone();
            instance.Init();
            instance.CheckExplorerFolder();

            if (logListener != null)
                OnLog += logListener;

            return instance;
        }

        internal void Init()
        {
            Instance = this;
            configHandler = new StandaloneConfigHandler();

            ExplorerCore.Init(this);
        }

        protected virtual void CheckExplorerFolder()
        {
            if (explorerFolderDest == null)
            {
                string assemblyLocation = Uri.UnescapeDataString(new Uri(typeof(ExplorerCore).Assembly.CodeBase).AbsolutePath);
                explorerFolderDest = Path.GetDirectoryName(assemblyLocation);
            }
        }
    }
}
