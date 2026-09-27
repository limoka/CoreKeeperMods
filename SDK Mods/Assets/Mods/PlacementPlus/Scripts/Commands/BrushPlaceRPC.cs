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
        // 0 = Grid Placement, 1 = Roof Apparatus, 2 = Paintbrush, 3 = Shovel/Hoe (All share the 
        // same "server executes on behalf" skeleton, so they use a single RPC differentiated by type).
        public int kind;
    }

    // Names for kind values — serialized and carried as ints.
    internal static class BrushKind
    {
        internal const int Place = 0;
        internal const int Roof = 1;
        internal const int Paint = 2;
        internal const int Tool = 3;
    }
}
