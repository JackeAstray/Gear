using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Shared Graphic foldout and Image controls for image-based component editors.
    /// </summary>
    public abstract class ImageEditorBase : ImageEditor
    {
        SerializedProperty sprite;
        SerializedProperty imageType;
        SerializedProperty preserveAspect;
        SerializedProperty fillCenter;
        SerializedProperty pixelsPerUnit;
        SerializedProperty fillMethod;
        SerializedProperty fillOrigin;
        SerializedProperty fillAmount;
        SerializedProperty fillClockwise;

        static readonly string[] HorizontalOrigins = { "Left", "Right" };
        static readonly string[] VerticalOrigins = { "Bottom", "Top" };
        static readonly string[] Radial90Origins = { "Bottom Left", "Top Left", "Top Right", "Bottom Right" };
        static readonly string[] Radial180Origins = { "Bottom", "Left", "Top", "Right" };
        static readonly string[] Radial360Origins = { "Bottom", "Right", "Top", "Left" };


        protected override void OnEnable()
        {
            base.OnEnable();

            sprite = serializedObject.FindProperty("m_Sprite");
            imageType = serializedObject.FindProperty("m_Type");
            preserveAspect = serializedObject.FindProperty("m_PreserveAspect");
            fillCenter = serializedObject.FindProperty("m_FillCenter");
            pixelsPerUnit = serializedObject.FindProperty("m_PixelsPerUnitMultiplier");
            fillMethod = serializedObject.FindProperty("m_FillMethod");
            fillOrigin = serializedObject.FindProperty("m_FillOrigin");
            fillAmount = serializedObject.FindProperty("m_FillAmount");
            fillClockwise = serializedObject.FindProperty("m_FillClockwise");
        }

        protected void GraphicFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Graphic", true, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                GraphicControls();
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void GraphicControls()
        {
            EditorGUI.BeginChangeCheck();
            EvoEditorGUI.Property(sprite, "Source Image", FieldOptions.WithInvertedColor());
            if (EditorGUI.EndChangeCheck())
            {
                var selectedSprite = sprite.objectReferenceValue as Sprite;
                if (selectedSprite != null)
                {
                    if (selectedSprite.border.sqrMagnitude > 0f)
                        imageType.intValue = (int)Image.Type.Sliced;
                    else if (!imageType.hasMultipleDifferentValues && imageType.intValue == (int)Image.Type.Sliced)
                        imageType.intValue = (int)Image.Type.Simple;
                }

                foreach (var selectedTarget in targets)
                    ((Image)selectedTarget).DisableSpriteOptimizations();
            }

            EvoEditorGUI.Property(m_Color, "Color", FieldOptions.WithInvertedColor());
            EvoEditorGUI.Toggle(m_Maskable, "Maskable", FieldOptions.WithInvertedColor());

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(m_RaycastTarget, "Raycast Target", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (m_RaycastTarget.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.ArrayProperty(m_RaycastPadding, "Raycast Padding", FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn();

            if (sprite.objectReferenceValue == null && !sprite.hasMultipleDifferentValues)
                return;

            EvoEditorGUI.AddLayoutSpace();
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Property(imageType, FieldOptions.WithoutBackground().WithoutSpace());
                if (!imageType.hasMultipleDifferentValues)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    {
                        var type = (Image.Type)imageType.intValue;
                        if (type == Image.Type.Sliced || type == Image.Type.Tiled)
                        {
                            EvoEditorGUI.Toggle(fillCenter);
                            EvoEditorGUI.Property(pixelsPerUnit, "PPU Multiplier", FieldOptions.WithoutSpace());
                        }
                        else
                        {
                            if (type == Image.Type.Filled)
                                FillControls();

                            EvoEditorGUI.Toggle(preserveAspect);
                            if (EvoEditorGUI.Button("Set Native Size"))
                            {
                                serializedObject.ApplyModifiedProperties();
                                foreach (var selectedTarget in targets)
                                {
                                    var image = (Image)selectedTarget;
                                    Undo.RecordObject(image.rectTransform, "Set Native Size");
                                    image.SetNativeSize();
                                    PrefabUtility.RecordPrefabInstancePropertyModifications(image.rectTransform);
                                }
                                serializedObject.Update();
                            }
                        }
                    }
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn();
        }

        void FillControls()
        {
            EditorGUI.BeginChangeCheck();
            EvoEditorGUI.Property(fillMethod);
            if (EditorGUI.EndChangeCheck())
                fillOrigin.intValue = 0;

            if (!fillMethod.hasMultipleDifferentValues)
            {
                var method = (Image.FillMethod)fillMethod.intValue;
                string[] origins = method switch
                {
                    Image.FillMethod.Horizontal => HorizontalOrigins,
                    Image.FillMethod.Vertical => VerticalOrigins,
                    Image.FillMethod.Radial90 => Radial90Origins,
                    Image.FillMethod.Radial180 => Radial180Origins,
                    _ => Radial360Origins
                };
                EvoEditorGUI.Dropdown(fillOrigin, origins);
                if (method > Image.FillMethod.Vertical)
                    EvoEditorGUI.Toggle(fillClockwise, "Clockwise");
            }

            bool drivenBySlider = false;
            foreach (var selectedTarget in targets)
            {
                var image = (Image)selectedTarget;
                if (image.rectTransform.drivenByObject is Slider slider && slider.fillRect == image.rectTransform)
                {
                    drivenBySlider = true;
                    break;
                }
            }

            using (new EditorGUI.DisabledScope(drivenBySlider))
                EvoEditorGUI.Property(fillAmount);
        }
    }
}