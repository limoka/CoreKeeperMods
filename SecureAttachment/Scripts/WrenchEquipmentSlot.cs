using CoreLib.Submodule.EquipmentSlot;
using CoreLib.Submodule.EquipmentSlot.Interface;
using UnityEngine;
using PlayerEquipment;

namespace SecureAttachment
{
    public class WrenchEquipmentSlot : PlaceObjectSlot, IModEquipmentSlot
    { 
        public const string WrenchObjectType = "SecureAttachment:Wrench";

        protected override EquipmentSlotType slotType =>
            EquipmentSlotModule.GetEquipmentSlotType<WrenchEquipmentSlot>();

        public ObjectType GetSlotObjectType()
        {
            return EquipmentSlotModule.GetObjectType(WrenchObjectType);
        }
        
        public static void ToggleMode()
        {
            var pc = Manager.main.player;
            if (pc == null) return;
            
            if (Manager.ui.isAnyInventoryShowing) return;
            if (Manager.ui.instrumentUI.isShowing) return;
            if (Manager.menu.IsAnyMenuActive()) return;
            if (!Manager.input.singleplayerInputModule.InputEnabled) return;
            
            var heldObject = pc.GetHeldObject();
            var heldObjectInfo = PugDatabase.GetObjectInfo(heldObject.objectID);

            if (heldObjectInfo == null ||
                heldObjectInfo.objectType != EquipmentSlotModule.GetObjectType(WrenchObjectType)) return;

            var message = new SecureAttachmentRPC()
            {
                command = MessageType.CycleWrenchMode,
                player = pc.entity
            };

            var entityManager = pc.world.EntityManager;
            
            entityManager.SendCommand(message);
        }
        

        public void UpdateSlotVisuals(PlayerController controller)
        {
            ObjectDataCD objectDataCd = controller.GetHeldObject();
            ObjectInfo objectInfo = PugDatabase.GetObjectInfo(objectDataCd.objectID, objectDataCd.variation);

            controller.carryablePlaceItemSprite.gameObject.SetActive(true);
            
            Sprite iconOverride = Manager.ui.itemOverridesTable.GetIconOverride(controller.visuallyEquippedContainedObject.objectData, true);
            controller.carryablePlaceItemSprite.sprite = iconOverride != null ? iconOverride : objectInfo?.smallIcon;

            controller.carryablePlaceItemColorReplacer.UpdateColorReplacerFromObjectData(controller.visuallyEquippedContainedObject);
        }
    }
}