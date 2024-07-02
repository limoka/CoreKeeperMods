using System;
using KeepFarming;
using PugTilemap;
using UnityEngine;

namespace Mods.KeepFarming.Scripts.Prefab
{
    public class SeedExtractor : CraftingBuilding
    {
        [SerializeField] private SpriteRenderer baseRenderer;
        [SerializeField] private SpriteRenderer mushRenderer;
        [SerializeField] private ColorReplacer mushColorReplacer;

        [SerializeField] private Sprite[] baseSprites;
        [SerializeField] private Sprite[] mushSprites;

        public int ticksPerFrame = 1;

        private bool isActive;
        private int currentFrame;
        private int currentFrameTicks;


        public override void OnOccupied()
        {
            base.OnOccupied();
            Manager.multiMap.SetHiddenTile(RenderPosition.RoundToInt2(), 4, TileType.circuitPlate, 0);
        }

        protected override void OnHide()
        {
            Manager.multiMap.ClearHiddenTileOfType(RenderPosition.RoundToInt2(), TileType.circuitPlate);
            base.OnHide();
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
            if (!SeedExtractorSystem.seedExtractorRecipes.IsCreated || 
                !SeedExtractorSystem.seedExtractorRecipes.ContainsKey(inputData))
            {
                mushColorReplacer.SetActiveColorReplacement(0);
                return;
            }
            
            var cookingIngredient = PugDatabase.GetComponent<CookingIngredientCD>(inputData);
            var replaceColors = mushColorReplacer.colorReplacementData.replacementColors[0];
                
            replaceColors.colorList[0] = cookingIngredient.brightestColor;
            replaceColors.colorList[1] = cookingIngredient.brightColor;
            replaceColors.colorList[2] = cookingIngredient.darkColor;
            replaceColors.colorList[3] = cookingIngredient.darkestColor;
            
            mushColorReplacer.SetActiveColorReplacement(1);
        }

        private void UpdateAnimation()
        {
            if (!isActive)
            {
                currentFrame = 0;
                currentFrameTicks = 0;
                mushRenderer.gameObject.SetActive(false);
                baseRenderer.sprite = baseSprites[currentFrame];
                return;
            }
            
            mushRenderer.gameObject.SetActive(true);

            currentFrameTicks++;
            if (currentFrameTicks >= ticksPerFrame)
            {
                currentFrame++;
                if (currentFrame >= baseSprites.Length)
                    currentFrame = 0;
                currentFrameTicks = 0;
            }

            baseRenderer.sprite = baseSprites[currentFrame];
            mushRenderer.sprite = mushSprites[currentFrame];
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

        protected void OnValidate()
        {
            baseSprites ??= Array.Empty<Sprite>();
            mushSprites ??= Array.Empty<Sprite>();
            if (mushSprites.Length != baseSprites.Length)
            {
                Array.Resize(ref mushSprites, baseSprites.Length);
            }

            if (mushColorReplacer != null)
            {
                var repColors = mushColorReplacer.colorReplacementData.replacementColors;
                if (repColors.Count <= 0)
                {
                    repColors.Add(new ColorList());
                }

                var colorList = repColors[0].colorList;
                while (colorList.Count < mushColorReplacer.colorReplacementData.srcColors.Count)
                {
                    colorList.Add(Color.black);
                }
            }
        }
    }
}