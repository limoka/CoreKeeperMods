using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using PugAutomation;
using Unity.Entities;
using Unity.Jobs;


[assembly: InternalsVisibleTo("KeepFarming")]
[module: UnverifiableCode]
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore 618

namespace KeepFarming.Access;

internal static class AccessExtensions
{

    internal static ComponentTypeHandle<MoverCD> GetMoverCDTypeHandle_Public(this PugAutomationSystem __instance)
    {
        return __instance.__PugAutomation_MoverCD_RW_ComponentTypeHandle;
    }
    
    internal static EntityQuery GetMoverMoveAndPickupQuery_Public(this PugAutomationSystem __instance)
    {
        return __instance.mover_move_and_pickup_Query;
    }
    
    internal static JobHandle GetDependency_Public(this PugAutomationSystem __instance)
    {
        return __instance.Dependency;
    }
    
    internal static void SetDependency_Public(this PugAutomationSystem __instance, JobHandle handle)
    {
        __instance.Dependency = handle;
    }
}