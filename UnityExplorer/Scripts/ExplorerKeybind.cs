using UnityExplorer.Config;

using UnityExplorer.UI;
using UnityExplorer.UI.Widgets;
using UniverseLib.Input;
using UInputManager = UniverseLib.Input.InputManager;

namespace UnityExplorer
{
    public static class ExplorerKeybind
    {
        public static void Update()
        {
            // check master toggle
            if (UInputManager.GetKeyDown(ConfigManager.Master_Toggle.Value))
            {
                UE_UIManager.ShowMenu = !UE_UIManager.ShowMenu;
            }
            TimeScaleKeyBindUpdate();
        }

        private static void TimeScaleKeyBindUpdate()
        {
            var widget = TimeScaleWidget.Instance;
            if (widget == null)
            {
                return;
            }

            if (UInputManager.GetKeyDown(ConfigManager.TIME_SCALE_TOGGLE.Value))
            {
                widget.OnPauseButtonClicked();
            }
            if (UInputManager.GetKeyDown(ConfigManager.LOCK_TIME_SCALE_TO_ZERO.Value))
            {
                widget.LockTo(0.0f);
            }
            if (UInputManager.GetKeyDown(ConfigManager.LOCK_TIME_SCALE_TO_NORMAL.Value))
            {
                widget.LockTo(1.0f);
            }
            if (UInputManager.GetKeyDown(ConfigManager.LOCK_TIME_SCALE_TO_HALF.Value))
            {
                widget.LockTo(widget.DesiredTime * 0.5f);
            }
            if (UInputManager.GetKeyDown(ConfigManager.LOCK_TIME_SCALE_TO_DOUBLE.Value))
            {
                widget.LockTo(widget.DesiredTime * 2.0f);
            }
        }
    }
}
