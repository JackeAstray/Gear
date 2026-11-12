using UnityEditor;
using UnityEditor.UI;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(RaycastArea))]
    public sealed class RaycastAreaEditor : GraphicEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RaycastControlsGUI();
            serializedObject.ApplyModifiedProperties();
        }
    }
}