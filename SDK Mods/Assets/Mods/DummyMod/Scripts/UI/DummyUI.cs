using System;
using CoreLib.UserInterface;
using CoreLib.UserInterface.Util;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace DummyMod.UI
{
    public class DummyUI : UIelement, IModUI
    {
        public GameObject root;
        public GameObject Root => root;
        public bool showWithPlayerInventory => true;
        public bool shouldPlayerCraftingShow => false;

        public PugText lastDamageText;
        public PugText minDamageText;
        public PugText averageDamageText;
        public PugText maxDamageText;
        public PugText damagePerSecondText;
        public PugText maxDamagePerSecondText;
        
        protected void Awake()
        {
            HideUI();
        }
        
        public void ShowUI()
        {
            root.SetActive(true);
            LateUpdate();
        }

        public void HideUI()
        {
            root.SetActive(false);
        }

        private void Update()
        {
            if (!this.IsVisible()) return;
        
            Entity entity = UserInterfaceModule.GetInteractionEntity();
            EntityManager entityManager = world.EntityManager;
            
            if (!entityManager.Exists(entity)) return;

            var dummy = entityManager.GetComponentData<DummyCD>(entity);
            
            string minText = dummy.minDamage == int.MaxValue ? "-" : dummy.minDamage.ToString();
            
            lastDamageText.Render(dummy.lastDamage.ToString());
            minDamageText.Render(minText);
            averageDamageText.Render(dummy.averageDamage.ToString());
            maxDamageText.Render(dummy.maxDamage.ToString());
            damagePerSecondText.Render(dummy.damagePerSecond.ToString());
            maxDamagePerSecondText.Render(dummy.maxDamagePerSecond.ToString());
        }

        public void OnResetClicked()
        {
            Entity entity = UserInterfaceModule.GetInteractionEntity();
            EntityManager entityManager = world.EntityManager;
            
            if (!entityManager.Exists(entity)) return;

            ClientModCommandSystem commandSystem = world.GetExistingSystemManaged<ClientModCommandSystem>();
            commandSystem.ResetDummy(entity);
        }
        
        public void OnKillClicked()
        {
            Entity entity = UserInterfaceModule.GetInteractionEntity();
            EntityManager entityManager = world.EntityManager;
            
            if (!entityManager.Exists(entity)) return;
            
            PlayerController player = Manager.main.player;
            player.playerCommandSystem.DestroyEntity(entity, player.entity);
            HideUI();
        }
    }
}