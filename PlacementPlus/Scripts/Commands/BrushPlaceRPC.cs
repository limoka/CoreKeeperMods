using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PlacementPlus.Commands
{
    // M2 Round 2 (plan-deharmony): Command to "execute brush placement at this anchor".
    // 'anchor' is the grid anchor calculated by the client-side patch (placementCD.bestPositionToPlaceAt).
    // On Burst-enabled servers, placementCD holds a different value calculated by the vanilla 1x1 logic 
    // (Round 1 observation: only half placed / placed in the wrong spot), so we explicitly send it over.
    public struct BrushPlaceRPC : IRpcCommand
    {
        public Entity player;
        public int3 anchor;
        public BrushKind kind;
    }

    // Names for kind values
    public enum BrushKind : byte
    {
        None = 0,
        Place,
        Roof,
        Tool
    }
}
