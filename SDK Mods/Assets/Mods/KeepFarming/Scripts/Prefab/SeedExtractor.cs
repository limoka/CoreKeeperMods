using System;
using System.Collections.Generic;
using KeepFarming;
using Pug.Sprite;
using Pug.UnityExtensions;
using PugProperties;
using PugTilemap;
using UnityEngine;

namespace Mods.KeepFarming.Scripts.Prefab
{
    public class SeedExtractor : CraftingBuilding
    {
        [SerializeField] private SpriteObject baseRenderer;
        [SerializeField] private SpriteObject mushRenderer;

        private bool isActive;
        private bool prevIsActive;

        private ObjectDataCD lastItem;
        
        private static readonly int baseAnim = Property.StringToHash("seed-extractor-base");
        private static readonly int mushAnim = Property.StringToHash("plant-mush");
        
        public override void OnOccupied()
        {
            base.OnOccupied();
            Manager.multiMap.SetHiddenTile(RenderPosition.RoundToInt2(), 4, TileType.circuitPlate, 0);
            prevIsActive = false;
        }

        protected override void OnHide()
        {
            Manager.multiMap.ClearHiddenTileOfType(RenderPosition.RoundToInt2(), TileType.circuitPlate);
            base.OnHide();
            prevIsActive = false;
            lastItem = default;
        }

        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            if (!entityExist)
            {
                return;
            }

            UpdateAnimation();
            if (!isActive) return;
            
            ObjectDataCD inputData = craftingHandler.inventoryHandler.GetObjectData(0);
            
            if (lastItem.Equals(inputData)) return;
            lastItem = inputData;
            
            if (!SeedExtractorSystem.seedExtractorRecipes.IsCreated || 
                !SeedExtractorSystem.seedExtractorRecipes.ContainsKey(inputData))
            {
                mushRenderer.primaryGradientMap = null;
                mushRenderer.ApplyVisualChange();
                return;
            }
            
            var cookingIngredient = PugDatabase.GetComponent<CookingIngredientCD>(inputData);
            mushRenderer.primaryGradientMap = ECSManager_Patch.gradientMaps.GetValueOrDefault(cookingIngredient);
            mushRenderer.ApplyVisualChange();
        }

        private void UpdateAnimation()
        {
            if (isActive == prevIsActive) return;
            prevIsActive = isActive;
            
            if (!isActive)
            {
                mushRenderer.gameObject.SetActive(false);
                mushRenderer.StopAnimation();
                baseRenderer.StopAnimation();
                return;
            }
            
            mushRenderer.gameObject.SetActive(true);
            baseRenderer.PlayAnimation(baseAnim);
            mushRenderer.PlayAnimation(mushAnim);
        }

        protected override void OnActive()
        {
            base.OnActive();
            isActive = true;
        }

        protected override void OnInactive()
        {
            base.OnInactive();
            isActive = false;
        }
        
    }
}