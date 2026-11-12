using Evo.EditorTools;
using UnityEditor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(BackdropBlur)), CanEditMultipleObjects]
    public class BackdropBlurEditor : ImageEditorBase
    {
        SerializedProperty settings;

        protected override void OnEnable()
        {
            base.OnEnable();
            settings = serializedObject.FindProperty("settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            GraphicFoldout();
            SettingsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool customMaterial = m_Material.hasMultipleDifferentValues || m_Material.objectReferenceValue != null;
                    using (new EditorGUI.DisabledScope(customMaterial))
                        DrawSettings(settings);

                    if (!serializedObject.isEditingMultipleObjects)
                    {
                        BackdropBlur blur = (BackdropBlur)target;
                        if (blur.ResolvedCamera == null)
                            EvoEditorGUI.Callout("Assign a source camera, or tag your camera MainCamera.");
                        if (blur.CaptureBeforeTransparents)
                            EvoEditorGUI.Callout("Camera and World Space canvases capture before transparents. " +
                                "Use Screen Space Overlay for the complete camera image.");
                        if (blur.ResolvedCamera != null && blur.ResolvedCamera.stereoEnabled)
                            EvoEditorGUI.Callout("Stereo/XR camera captures are not supported.", CalloutType.Warning);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        internal static void DrawSettings(SerializedProperty settings)
        {
            SerializedProperty enabled = settings.FindPropertyRelative("enabled");
            SerializedProperty sourceCamera = settings.FindPropertyRelative("sourceCamera");
            SerializedProperty resolution = settings.FindPropertyRelative("resolution");
            SerializedProperty updateInterval = settings.FindPropertyRelative("updateInterval");
            SerializedProperty iterations = settings.FindPropertyRelative("iterations");
            SerializedProperty radius = settings.FindPropertyRelative("radius");
            SerializedProperty saturation = settings.FindPropertyRelative("saturation");
            SerializedProperty brightness = settings.FindPropertyRelative("brightness");
            SerializedProperty noise = settings.FindPropertyRelative("noise");

            // The standalone component starts expanded, since the blur is all it shows
            if (EvoEditorGUI.BeginToggleFoldout(enabled, "Backdrop Blur", settings.serializedObject.targetObject is BackdropBlur,
                FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    EvoEditorGUI.Property(sourceCamera);
                    EvoEditorGUI.Property(resolution);
                    EvoEditorGUI.Property(updateInterval);
                    EvoEditorGUI.Property(iterations);
                    EvoEditorGUI.Property(radius);
                    EvoEditorGUI.Property(saturation);
                    EvoEditorGUI.Property(brightness);
                    EvoEditorGUI.Property(noise, FieldOptions.WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }
    }
}