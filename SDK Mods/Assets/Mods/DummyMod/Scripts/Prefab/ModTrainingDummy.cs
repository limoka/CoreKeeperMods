

using CoreLib.Submodule.UserInterface;

namespace DummyMod
{
    public class ModTrainingDummy : TrainingDummy
    {

        public void OnUse()
        {
            UserInterfaceModule.OpenModUI(entity, TheDummyMod.DUMMY_UI_ID);
        }
    }
}