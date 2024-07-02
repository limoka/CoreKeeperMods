using BookMod.Components;
using BookMod.Model;
using BookMod.UI;
using CoreLib.Equipment;
using CoreLib.UserInterface;
using CoreLib.Util.Extensions;

namespace BookMod
{
    public class BookSlot : EquipmentSlot, IModEquipmentSlot
    {
        public const string BookObjectType = "BookMod:Book";
        
        protected override EquipmentSlotType slotType => EquipmentModule.GetEquipmentSlotType<BookSlot>();


        public override void HandleInput(bool interactPressed, bool interactReleased, bool secondInteractPressed, bool secondInteractReleased,
            bool interactIsHeldDown,
            bool secondInteractIsHeldDown)
        {
            if (!interactPressed && !secondInteractPressed) return;
            
            if (secondInteractPressed || secondInteractIsHeldDown)
            {
                OpenBook();
            }
        }

        private void OpenBook()
        {
            if (PugDatabase.HasComponent<BookCD>(objectData.objectID))
            {
                BookCD bookCd = PugDatabase.GetComponent<BookCD>(objectData.objectID);
                if (bookCd.bookId <= 0) return;
                if (bookCd.bookId >= BookMod.books.Count) return;
                
                if (Manager.menu.IsAnyMenuActive()) return;

                BookUI.currentBook = BookMod.books[bookCd.bookId];
                BookUI.currentBookItem = objectData;
                
                UserInterfaceModule.OpenModUI(BookMod.BOOK_UI_ID);
            }
        }

        public ObjectType GetSlotObjectType()
        {
            return EquipmentModule.GetObjectType(BookObjectType);
        }

        private ContainedObjectsBuffer AsBuffer(ObjectDataCD objectDataCd)
        {
            return new ContainedObjectsBuffer()
            {
                objectData = objectDataCd
            };
        }
        
        public void UpdateSlotVisuals(PlayerController controller)
        {
            ObjectDataCD objectDataCd = controller.GetHeldObject();
            ObjectInfo objectInfo = PugDatabase.GetObjectInfo(objectDataCd.objectID, objectDataCd.variation);

            ContainedObjectsBuffer objectsBuffer = AsBuffer(objectDataCd);

            controller.InvokeVoid("ActivateCarryableItemSpriteAndSkin", new object[]
            {
                controller.carryablePlaceItemSprite,
                controller.carryablePlaceItemPugSprite,
                controller.carryableSwingItemSkinSkin,
                objectInfo,
                objectsBuffer
            });

            controller.carryablePlaceItemSprite.sprite = objectInfo.smallIcon;
            controller.carryablePlaceItemColorReplacer.UpdateColorReplacerFromObjectData(objectsBuffer);
        }
    }
}