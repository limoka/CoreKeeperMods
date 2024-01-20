using System;
using CoreLib.UserInterface;
using UnityEngine;

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