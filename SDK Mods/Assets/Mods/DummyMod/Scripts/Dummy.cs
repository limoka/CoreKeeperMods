

using CoreLib.Submodule.UserInterface;

namespace DummyMod
{
    public class Dummy : EntityMonoBehaviour
    {

        public void OnUse()
        {
            UserInterfaceModule.OpenModUI(entity, TheDummyMod.DUMMY_UI_ID);
        }
    }
}