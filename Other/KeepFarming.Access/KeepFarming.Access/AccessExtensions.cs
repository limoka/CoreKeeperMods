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

public static class AccessExtensions
{

    public static EntityTypeHandle Unity_Entities_Entity_TypeHandle_Public(this PugAutomationSystem system)
    {
        return system.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
    }
    
    public static ComponentTypeHandle<MoverCD> PugAutomation_MoverCD_RW_ComponentTypeHandle_Public(this PugAutomationSystem system)
    {
        return system.__TypeHandle.__PugAutomation_MoverCD_RW_ComponentTypeHandle;
    }
    
    public static ComponentLookup<BigEntityRefCD> PugAutomation_BigEntityRefCD_RO_ComponentLookup_Public(this PugAutomationSystem system)
    {
        return system.__TypeHandle.__PugAutomation_BigEntityRefCD_RO_ComponentLookup;
    }
    
    public static ComponentLookup<PickUpObjectCD> PickUpObjectCD_RO_ComponentLookup_Public(this PugAutomationSystem system)
    {
        return system.__TypeHandle.__PickUpObjectCD_RO_ComponentLookup;
    }
    
    public static ComponentLookup<MoveeCD> PugAutomation_MoveeCD_RW_ComponentLookup_Public(this PugAutomationSystem system)
    {
        return system.__TypeHandle.__PugAutomation_MoveeCD_RW_ComponentLookup ;
    }

    public static EntityQuery GetMoverMoveAndPickupQuery_Public(this PugAutomationSystem __instance)
    {
        return __instance.__query_1171083633_0;
    }
    
    public static JobHandle GetDependency_Public(this PugAutomationSystem __instance)
    {
        return __instance.Dependency;
    }
    
    public static void SetDependency_Public(this PugAutomationSystem __instance, JobHandle handle)
    {
        __instance.Dependency = handle;
    }
}