using System;
using System.Collections.Generic;
using BookMod.Model;
using CoreLib.UserInterface;
using CoreLib.UserInterface.Util;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ImagePosition = BookMod.Model.ImagePosition;

namespace BookMod.UI
{
    public class BookUI : UIelement, IModUI
    {
        public GameObject root;
        public GameObject Root => root;
        public bool showWithPlayerInventory => false;
        public bool shouldPlayerCraftingShow => false;

        public SpriteRenderer background;
        public SmallTitleUIBackground title;

        public Transform textRoot;
        public PugText textTemplate;
        public SpriteRenderer image;
        
        public Vector3 topImagePos;
        public Vector3 middleImagePos;
        public Vector3 bottomImagePos;

        public Vector3 startOffset;

        private List<PugText> textPool = new List<PugText>();
        

        private bool needsRefresh;
        private int currentPage;

        internal static ObjectDataCD currentBookItem;
        internal static BookData currentBook;
        
        protected void Awake()
        {
            HideUI();
        }

        public void ShowUI()
        {
            root.SetActive(true);
            needsRefresh = true;
            currentPage = 0;
            Update();
        }

        public void HideUI()
        {
            root.SetActive(false);
        }

        private void Update()
        {
            if (!this.IsVisible()) return;

            RefreshBook();
        }

        private void RefreshBook(bool force = false)
        {
            if (!needsRefresh && !force) return;
            
            UIManager.CraftingUITheme craftingUITheme = Manager.ui.GetCraftingUITheme(currentBook.backgroundTheme);

            background.sprite = craftingUITheme.background;
            
            title.defaultTitle = currentBook.title;
            title.UpdateThemeTextAndBackground(craftingUITheme, WindowAlignment.Left);
            
            var page = currentBook.pages[currentPage];
            float startPos = 0;

            int paragraphsCount = page.paragraphs.Count;
            
            for (int i = 0; i < paragraphsCount; i++)
            {
                string paragraph = page.paragraphs[i];
                PugText text;
                
                if (i < textPool.Count)
                {
                    text = textPool[i];
                }
                else
                {
                    text = Instantiate(textTemplate, textRoot, false);
                    text.transform.localPosition = new Vector3();
                    textPool.Add(text);
                }
                
                text.gameObject.SetActive(true);
                text.Render(paragraph);
                text.SetTempColor(craftingUITheme.textColor);
                text.transform.localPosition = startOffset + new Vector3(0, startPos, 0);
                startPos -= text.dimensions.height;

            }

            for (int i = paragraphsCount; i < textPool.Count; i++)
            {
                textPool[i].gameObject.SetActive(false);
            }

            Sprite sprite = null;

            if (!string.IsNullOrEmpty(page.imageResource))
            {
                sprite = BookMod.Load<Sprite>(page.imageResource);
                if (sprite == null)
                {
                    BookMod.Log.LogWarning($"Failed to load image asset at path: {page.imageResource}");
                }
            }

            if (sprite != null)
            {
                image.gameObject.SetActive(true);
                image.sprite = sprite;

                var imageTransform = image.transform;
                imageTransform.localPosition = page.imagePosition switch
                {
                    ImagePosition.Top => topImagePos,
                    ImagePosition.Middle => middleImagePos,
                    ImagePosition.Bottom => bottomImagePos,
                    _ => imageTransform.localPosition
                };
            }
            else
            {
                image.gameObject.SetActive(false);
            }
            
            needsRefresh = false;
        }

        public void OnPrevPage()
        {
            currentPage -= 1;
            if (currentPage < 0) currentPage = 0;

            RefreshBook(true);
        }

        public void OnNextPage()
        {
            currentPage += 1;
            
            if (currentPage >= currentBook.pages.Count) 
                currentPage = currentBook.pages.Count - 1;

            RefreshBook(true);
        }
    }
}