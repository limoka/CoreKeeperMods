using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using PugAutomation;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


[assembly: InternalsVisibleTo("UnityExplorer")]
[module: UnverifiableCode]
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore 618

namespace UnityExplorer.Access;

internal static class AccessExtensions
{

    internal static TextGenerator cachedInputTextGenerator_Public(this InputField field)
    {
        return field.cachedInputTextGenerator;
    }

    internal static void DestroyDropdownList_Public(this Dropdown dropdown, GameObject go)
    {
        dropdown.DestroyDropdownList(go);
    }

    internal static Canvas getCanvasPublic(this GraphicRaycaster gr)
    {
        return gr.canvas;
    }
    
    internal static Scene GetScene(int handle)
    {
        return new Scene() { m_Handle = handle };
    }

    internal static Image getFillImage(this Slider slider)
    {
        return slider.m_FillImage;
    }
}