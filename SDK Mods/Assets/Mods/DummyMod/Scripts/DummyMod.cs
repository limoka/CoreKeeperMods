using CoreLib;
using CoreLib.Submodules.ModEntity;
using CoreLib.Submodules.ModEntity.Atributes;
using CoreLib.UserInterface;
using CoreLib.Util.Extensions;
using PugMod;
using Unity.Entities;
using UnityEngine;
using Logger = CoreLib.Util.Logger;

namespace DummyMod
{
    [EntityModification]
    public class TheDummyMod : IMod
    {
        public const string VERSION = "1.0.1";
        public const string MOD_ID = "DummyMod";

        internal static Logger Log = new Logger("Dummy Mod");

        public const string DUMMY_UI_ID = MOD_ID + ":DummyUI";

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            CoreLibMod.LoadModules(
                typeof(UserInterfaceModule),
                typeof(EntityModule));

            var modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError("Failed to load Dummy mod: mod metadata not found!");
                return;
            }

            EntityModule.RegisterEntityModifications(modInfo.ModId);
            modInfo.TryLoadBurstAssembly();

            Log.LogInfo("Mod loaded successfully");
        }

        public void Init() { }

        public void Shutdown() { }

        [EntityModification(ObjectID.Carpenter)]
        private static void EditAutomationTable(Entity entity, GameObject authoring, EntityManager entityManager)
        {
            var canCraftBuffer = entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);
            var item = API.Authoring.GetObjectID("DummyMod:Dummy");
            
            for (int i = 0; i < canCraftBuffer.Length; i++)
            {
                if (canCraftBuffer[i].objectID == item) return;
                if (canCraftBuffer[i].objectID != ObjectID.None) continue;

                Log.LogInfo($"Adding itemId {item} to AutomationTable");
                var craft = canCraftBuffer[i];
                craft.objectID = item;
                craft.amount = 1;
                craft.entityAmountToConsume = 0;
                canCraftBuffer[i] = craft;
                return;
            }

            addBufferEntry(canCraftBuffer, item);
        }

        private static void addBufferEntry(DynamicBuffer<CanCraftObjectsBuffer> canCraftBuffer, ObjectID itemId)
        {
            Log.LogInfo($"Adding itemId {itemId} to AutomationTable");
            canCraftBuffer.Add(new CanCraftObjectsBuffer
            {
                objectID = itemId,
                amount = 1,
                entityAmountToConsume = 0
            });
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is not GameObject go) return;
            
            var entityMono = go.GetComponent<EntityMonoBehaviour>();
            if (entityMono != null)
            {
                EntityModule.EnablePooling(go);
            }

            UserInterfaceModule.RegisterModUI(go);

            var objectAuthoring = go.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null)
            {
                EntityModule.AddToAuthoringList(go);
            }
        }

        public void Update() { }
    }
}