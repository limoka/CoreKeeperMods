using CoreLib;
using CoreLib.Submodule.Entity;
using CoreLib.Submodule.Entity.Attribute;
using CoreLib.Submodule.UserInterface;
using CoreLib.Util.Extension;
using Interaction;
using PugMod;
using Unity.Entities;
using UnityEngine;
using Logger = CoreLib.Util.Logger;

namespace DummyMod
{
    public class TheDummyMod : IMod
    {
        public const string VERSION = "2.0.0";
        public const string MOD_ID = "DummyMod";

        internal static Logger Log = new Logger("Dummy Mod");

        public const string DUMMY_UI_ID = MOD_ID + ":DummyUI";

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            CoreLibMod.LoadSubmodule(
                typeof(UserInterfaceModule),
                typeof(EntityModule));

            var modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError("Failed to load Dummy mod: mod metadata not found!");
                return;
            }

            Log.LogInfo("Mod loaded successfully");
        }

        public void Init() { }

        public void Shutdown() { }

        [EntityModification(ObjectID.TrainingDummy)]
        private static void EditTrainingDummy(Entity entity, GameObject authoring, EntityManager entityManager)
        {
            Log.LogInfo("Modifying TrainingDummy");
            entityManager.AddComponentData(entity, new DummyCD()
            {
                minDamage = int.MaxValue
            });
            entityManager.AddBuffer<DummyDamageBuffer>(entity);
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is not GameObject go) return;

            UserInterfaceModule.RegisterModUI(go);
        }

        public void Update() { }
    }
}