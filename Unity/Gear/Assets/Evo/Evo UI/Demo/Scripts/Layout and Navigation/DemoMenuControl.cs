using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A custom object in the Menu Bar tab's View menu: a switch for the grid or for snapping. Menus build their objects
    /// when they open, so the tab binds each one as it appears; the entry's name picks what it controls.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoMenuControl : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Switch toggle;

        DemoMenuBar owner;
        bool snap;

        // The menu bar calls this every frame while the menu is open, so it has no lambda: a captured local would allocate on
        // every call, even the ones that return early
        public void Bind(DemoMenuBar menuBar, string entry)
        {
            if (owner == menuBar)
                return;

            owner = menuBar;
            label.text = entry;

            snap = entry.Contains("Snap");
            DemoUtils.Show(toggle, snap ? owner.Snap : owner.ShowGrid);
            toggle.onValueChanged.AddListener(SetValue);
        }

        void SetValue(bool value)
        {
            if (snap) { owner.Snap = value; }
            else { owner.ShowGrid = value; }
        }
    }
}
