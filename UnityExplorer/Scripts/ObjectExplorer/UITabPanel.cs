using UniverseLib.UI.Models;

namespace UnityExplorer.ObjectExplorer
{
    public abstract class UITabPanel : UIModel
    {
        public abstract string Name { get; }

        public abstract void Update();
    }
}