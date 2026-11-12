using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Lays out a generated ListView cell's icon and label.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ListViewCellLayout : HorizontalLayoutGroup
    {
#if UNITY_EDITOR
        // From uGUI 2.6 (Unity 6.6), adding a layout group in the editor records every child rect for undo, which also
        // clears the redo history, so a refresh that builds cells after an undo or redo would cost the user their redo.
        // The cell sets every layout value itself, so the reset defaults aren't needed.
        protected override void Reset() { }
#endif
    }
}
