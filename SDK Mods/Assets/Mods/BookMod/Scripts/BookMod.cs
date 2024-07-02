using System.Collections.Generic;
using System.Linq;
using System.Text;
using BookMod.Components;
using BookMod.Model;
using CoreLib;
using CoreLib.Equipment;
using CoreLib.Localization;
using CoreLib.Submodules.ModEntity;
using CoreLib.Submodules.ModEntity.Components;
using CoreLib.UserInterface;
using CoreLib.Util.Extensions;
using PugMod;
using UnityEngine;
using Logger = CoreLib.Util.Logger;

namespace BookMod
{
    public class BookMod : IMod
    {
        public const string MOD_ID = "BookMod";
        
        internal static Logger Log = new Logger("Book Mod");

        internal static List<BookData> books = new List<BookData>();
        internal static GameObject templateBook;

        public static AssetBundle[] bundles;

        public const string BOOK_UI_ID = MOD_ID + ":BookUI";
        
        public void EarlyInit()
        {
            CoreLibMod.LoadModules(
                typeof(LocalizationModule), 
                typeof(UserInterfaceModule),
                typeof(EntityModule),
                typeof(EquipmentModule));
            
            var modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError("Failed to load Dummy mod: mod metadata not found!");
                return;
            }

            bundles = modInfo.AssetBundles.ToArray();
            
            books.Add(new BookData()
            {
                title = "Null Book"
            });

            var extraBooks = modInfo.Metadata.files.Where(file => file.path.EndsWith(".book")).ToArray();

            foreach (ModFile extraBook in extraBooks)
            {
                byte[] bytes = modInfo.GetFile(extraBook.path);
                string text = Encoding.UTF8.GetString(bytes);
                
                BookData book = BookData.parseFromText(text);

                if (book != null)
                {
                    books.Add(book);
                    Log.LogInfo($"Loaded Book '{book.title}' from {extraBook.path}");
                }
            }
            
            Log.LogInfo("Book mod is loaded!");
        }

        public static T Load<T>(string path) where T : Object
        {
            foreach (AssetBundle bundle in bundles)
            {
                T result = bundle.LoadAsset<T>(path);
                if (result != null) return result;
            }

            return null;
        }

        public void Init()
        {
        }

        internal static void AddBooks()
        {
            for (int i = 1; i < books.Count; i++)
            {
                BookData book = books[i];

                GameObject bookItem = Object.Instantiate(templateBook);
                bookItem.hideFlags = HideFlags.HideAndDontSave;

                ObjectAuthoring objectAuthoring = bookItem.GetComponent<TemplateObject>().Convert();

                objectAuthoring.objectName = $"BookMod:Book_{i}";
                objectAuthoring.objectType = EquipmentModule.GetObjectType(BookSlot.BookObjectType);

                BookAuthoring bookAuthoring = bookItem.GetComponent<BookAuthoring>();
                bookAuthoring.bookId = i;

                API.Authoring.RegisterAuthoringGameObject(bookItem);

                LocalizationModule.AddTerm($"Items/{objectAuthoring.objectName}", book.title);
                LocalizationModule.AddTerm($"Items/{objectAuthoring.objectName}Desc", book.description);
                
                Log.LogInfo($"Registered book {book.title} as {objectAuthoring.objectName}");
            }
        }

        public void Shutdown()
        {
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is TextAsset text)
            {
                BookData book = BookData.parseFromText(text.text);

                if (book != null)
                {
                    books.Add(book);
                    Log.LogInfo($"Loaded Book '{book.title}' from bundle");
                }
            }

            if (obj is not GameObject go) return;
            
            UserInterfaceModule.RegisterModUI(go);
                
            var template = go.GetComponent<TemplateObject>();
            if (template != null)
            {
                templateBook = go;
            }
                
            var slot = go.GetComponent<EquipmentSlot>();

            if (slot != null)
            {
                EntityModule.AddToAuthoringList(go);
                EquipmentModule.RegisterEquipmentSlot<BookSlot>(go);
            }
        }

        public void Update()
        {
        }
    }
}