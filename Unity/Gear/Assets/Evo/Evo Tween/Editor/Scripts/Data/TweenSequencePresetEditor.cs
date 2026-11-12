using UnityEditor;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    [CustomEditor(typeof(TweenSequencePreset))]
    public sealed class TweenSequencePresetEditor : UnityEditor.Editor
    {
        SerializedProperty clips;
        TweenClipGUI clipListGUI;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            clips = serializedObject.FindProperty("clips");
            clipListGUI ??= new TweenClipGUI();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();
            clipListGUI.DrawPreset(serializedObject, clips);
            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}