using System.Runtime.InteropServices;
using PlacementPlus.Commands;
using PlacementPlus.Util;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PlacementPlus.Components
{
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PlacementPlusState : IComponentData
    {
        [GhostField] public int size;
        [GhostField] public int currentMaxSize;
        
        [GhostField] public int lastColorIndex;

        [GhostField] public BrushMode mode;
        [GhostField] public RoofingToolMode roofingMode;
        [GhostField] public BlockMode blockMode;

        [GhostField] public bool replaceTiles;
        [GhostField] public NetworkTick changedOnTick;
        
        public void ChangeSize(int polarity, NetworkTick currentTick)
        {
            if (mode == BrushMode.NONE)
            {
                size = 0;
                SetMode(BrushMode.SQUARE, currentTick);
            }

            SetSize(size + polarity, currentTick);
        }
        
        public void CheckSize(NetworkTick currentTick)
        {
            int maxSize = currentMaxSize;
            if (size > maxSize)
            {
                size = maxSize;
                changedOnTick = currentTick;
            }
        }
        
        public void ToggleMode(NetworkTick currentTick)
        {
            int newMode = (int)mode + 1;
            if (newMode >= (int)BrushMode.MAX)
            {
                newMode = (int)BrushMode.NONE;
            }

            SetMode((BrushMode)newMode, currentTick);
        }
        
        public void SetMode(BrushMode newMode, NetworkTick currentTick)
        {
            mode = newMode;
            changedOnTick = currentTick;
        }
        
        public void TryRotate(int variation, bool canBeRotated, bool isAMiner, NetworkTick currentTick)
        {
            if (mode == BrushMode.NONE ||
                mode == BrushMode.SQUARE) return;
            if (size == 0) return;

            if (!canBeRotated) return;

            bool isVertical = variation is 0 or 2;

            if (isAMiner)
            {
                isVertical = !isVertical;
            }

            if (isVertical)
                SetMode(BrushMode.VERTICAL, currentTick);
            else
                SetMode(BrushMode.HORIZONTAL, currentTick);
        }
        
        public void SetSize(int newSize, NetworkTick currentTick)
        {
            int maxSize = currentMaxSize;
            size = math.clamp(newSize, 0, maxSize);

            changedOnTick = currentTick;
        }
        
        public PlacementMessageRPC ToggleRoofingMode(bool backwards)
        {
            // Only these three are implemented. CHESS_PATTERN and below are
            // defined in the enum but nothing acts on them yet, so cycling
            // through them would just look broken.
            const int lastImplemented = (int)RoofingToolMode.CLEAR_ROOF;

            int newMode = (int)roofingMode + (backwards ? -1 : 1);

            if (newMode > lastImplemented)
            {
                newMode = (int)RoofingToolMode.TOGGLE;
            }

            if (newMode < 0)
            {
                newMode = lastImplemented;
            }
            roofingMode = (RoofingToolMode)newMode;

            return new PlacementMessageRPC()
            {
                messageType = ModMessageType.ROOFING_MODE_MESSAGE,
                messageData = newMode
            };
        }
        
        public PlacementMessageRPC ToggleBlockMode(bool backwards, bool allowRemove)
        {
            // REMOVE only exists in creative worlds; elsewhere the cycle stops
            // at WALL (and a leftover REMOVE state wraps back to TOGGLE).
            int lastMode = (int)(allowRemove ? BlockMode.REMOVE : BlockMode.WALL);

            int newMode = (int)blockMode + (backwards ? -1 : 1);

            if (newMode > lastMode)
            {
                newMode = (int)BlockMode.TOGGLE;
            }

            if (newMode < 0)
            {
                newMode = lastMode;
            }

            blockMode = (BlockMode)newMode;
            return new PlacementMessageRPC()
            {
                messageType = ModMessageType.BLOCK_MODE_MESSAGE,
                messageData = newMode
            };
        }
        
        public readonly BrushRect GetExtents()
        {
            int width = mode.IsHorizontal() ? size : 0;
            int height = mode.IsVertical() ? size : 0;

            return new BrushRect(width, height);
        }
    }
}