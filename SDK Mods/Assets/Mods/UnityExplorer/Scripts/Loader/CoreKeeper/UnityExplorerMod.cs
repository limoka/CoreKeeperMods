using System;
using System.Collections.Generic;
using System.Linq;
using ECSExtension;
using ECSExtension.Panels;
using PugMod;
using Unity.Entities;
using UnityEngine;
using UnityExplorer;
using UnityExplorer.Inspectors;
using UnityExplorer.Loader.Standalone;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using Logger = UnityExplorer.Loader.CoreKeeper.Logger;
using Object = UnityEngine.Object;

namespace Mods.UnityExplorer.Scripts.Loader.CoreKeeper
{
    public class UnityExplorerMod : IMod
    {
        public const string NAME = "Unity Explorer";
        internal static LoadedMod modInfo;
        public static Logger logger = new Logger("Unity Explorer");

        
        public void EarlyInit()
        {
            modInfo = GetModInfo(this);
            if (modInfo == null)
            {
                Debug.LogError($"[Unity Explorer]: Failed to load {NAME}: mod metadata not found!");
                return;
            }
            
            ExplorerEditorLoader.Initialize();

            InspectorManager.customInspectors.Add(EntityAdder);
            InspectorManager.equalityCheckers.Add(typeof(Entity), EntityEqualityChecker);
            UE_UIManager.onInit += UIManagerOnInit;
            logger.LogInfo("Added Entity Inspector");
        }
        
        public static LoadedMod GetModInfo(IMod mod)
        {
            return API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(mod));
        }
        
        private static void UIManagerOnInit()
        {
            ObjectExplorerPanel explorerPanel = UE_UIManager.GetPanel<ObjectExplorerPanel>(UE_UIManager.Panels.ObjectExplorer);
            explorerPanel.AddTab(new WorldExplorer(explorerPanel));
            logger.LogInfo("Added World Explorer");
        }
        
        private static bool EntityEqualityChecker(object o1, object o2)
        {
            if (o1 is Entity e1 && o2 is Entity e2)
            {
                return e1.Equals(e2);
            }

            return false;
        }

        private static Type EntityAdder(object o)
        {
            if (o is Entity)
            {
                return typeof(EntityInspector);
            }

            return null;
        }

        public void Init()
        {
        }

        public void Shutdown()
        {
            List<InspectorBase> entityInspectors = new List<InspectorBase>();
            foreach (InspectorBase inspector in InspectorManager.Inspectors)
            {
                if (inspector is EntityInspector)
                {
                    entityInspectors.Add(inspector);
                }
            }

            foreach (InspectorBase inspector in entityInspectors)
            {
                inspector.CloseInspector();
            }

            InspectorManager.customInspectors.Remove(EntityAdder);
            InspectorManager.equalityCheckers.Remove(typeof(Entity));
            logger.LogInfo("Removed Entity Inspector");
        }

        public void ModObjectLoaded(Object obj)
        {
        }

        public void Update()
        {
        }
    }
}