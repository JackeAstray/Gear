using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Shared controls for standalone effects and every Interactive inspector.
    /// </summary>
    public static class InteractiveEffectEditor
    {
        /// <summary>
        /// Begins the effect's toggle foldout. A standalone effect starts expanded, since the effect is all it shows.
        /// </summary>
        internal static bool BeginToggleFoldout(SerializedProperty property, string title)
        {
            bool visible = EvoEditorGUI.BeginToggleFoldout(property.FindPropertyRelative(nameof(InteractiveEffectSettings.enabled)),
                title, property.serializedObject.targetObject is InteractiveEffect, FoldoutOptions.WithInvertedColor().WithoutBoldText());
            SyncShader(property);
            return visible;
        }

        internal static void DrawTarget(SerializedProperty property)
        {
            SerializedProperty reference = property.FindPropertyRelative(nameof(InteractiveEffectSettings.targetGraphic));
            EvoEditorGUI.Property(reference, "Target Graphic");
            if (reference.hasMultipleDifferentValues || property.serializedObject.isEditingMultipleObjects)
                return;

            Graphic graphic = reference.objectReferenceValue as Graphic;
            if (graphic == null && property.serializedObject.targetObject is Component host)
                graphic = host is Interactive interactive ? interactive.targetGraphic : host.GetComponent<Graphic>();
            if (graphic == null)
                EvoEditorGUI.Callout("Assign an Image, RawImage or Procedural Shape to receive the effect.", spaceBefore: 0, spaceAfter: 3);
            else if (!InteractiveEffectRenderer.IsSupported(graphic))
                EvoEditorGUI.Callout("This target needs a default UI material or an Evo shader with Interactive Effects support.", CalloutType.Warning, spaceBefore: 0, spaceAfter: 3);
        }

        /// <summary>
        /// Custom color or Styler Color ID, then Ignore Graphic Alpha. Interactive's Style Provider and preset choose
        /// the source for all of its effects; standalone effects draw their own Color Mode and preset.
        /// </summary>
        internal static void DrawColor(SerializedProperty property)
        {
            SerializedObject host = property.serializedObject;
            if (host.targetObject is not InteractiveEffect)
            {
                DrawColorSource(property, host.FindProperty(nameof(Interactive.styleProvider)),
                    host.FindProperty(nameof(Interactive.stylerPreset)), addSpace: true, invertColor: false);
            }
            else
            {
                SerializedProperty colorMode = host.FindProperty(nameof(InteractiveEffect.colorMode));
                SerializedProperty preset = host.FindProperty(nameof(InteractiveEffect.stylerPreset));

                EvoEditorGUI.BeginColumn();
                EvoEditorGUI.Property(colorMode, "Color Mode", FieldOptions.WithoutSpace().WithoutBackground());
                {
                    if (!colorMode.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.BeginContainer();
                        if (colorMode.intValue == (int)StyleProvider.Styler)
                        {
                            StylerEditor.PresetFields(host.FindProperty(nameof(InteractiveEffect.useDefaultStylerPreset)), preset,
                                addSpace: true, invertColor: true);
                        }
                        DrawColorSource(property, colorMode, preset, addSpace: false, invertColor: true);
                        EvoEditorGUI.EndContainer();
                    }
                }
                EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
            }

            EvoEditorGUI.Toggle(property.FindPropertyRelative(nameof(InteractiveEffectSettings.ignoreGraphicAlpha)));
        }

        static void DrawColorSource(SerializedProperty property, SerializedProperty provider, SerializedProperty preset,
            bool addSpace, bool invertColor)
        {
            if (!provider.hasMultipleDifferentValues && provider.intValue == (int)StyleProvider.Styler)
            {
                StylerEditor.ColorDropdown(preset, property.FindPropertyRelative(nameof(InteractiveEffectSettings.stylerColor)),
                    "Color ID", addSpace, invertColor: invertColor);
            }
            else
            {
                EvoEditorGUI.Property(property.FindPropertyRelative(nameof(InteractiveEffectSettings.color)), "Color",
                    FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor));
            }
        }

        internal static void SyncShader(SerializedProperty property)
        {
            SerializedProperty enabled = property.FindPropertyRelative(nameof(InteractiveEffectSettings.enabled));
            if (enabled.hasMultipleDifferentValues || Application.isPlaying)
                return;

            SerializedProperty shader = property.FindPropertyRelative("shader");
            Shader next = enabled.boolValue ? Shader.Find(InteractiveEffectSettings.ShaderName) : null;
            if (shader.hasMultipleDifferentValues || shader.objectReferenceValue != next)
                shader.objectReferenceValue = next;
        }
    }
}