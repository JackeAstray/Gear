using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(VirtualCursor)), CanEditMultipleObjects]
    public class VirtualCursorEditor : UnityEditor.Editor
    {
        SerializedProperty inputModule, movementMode, inputDevices, movementSpeed, smoothing, deadZone;
        SerializedProperty movementBounds, hideWithoutSelection, enableRaycast, fitToTarget, targetPadding, transitionDuration;
        SerializedProperty preset, customVisual, outlineWidth, cornerLength, centerSize, edgeSoftness;
        SerializedProperty styleProvider, useDefaultStylerPreset, stylerPreset, normalColor, targetColor, pressedColor;
        SerializedProperty normalStyle, targetStyle, pressedStyle, cornerRadius, radiusSyncMode;
        SerializedProperty selectionDuration, transitionEase, customEase;
        SerializedProperty dashMode, dashLength, dashGap, dashOffset, roundedDashes, dashAnimation, dashSpeed;

        void OnEnable()
        {
            inputModule = serializedObject.FindProperty(nameof(inputModule));
            movementMode = serializedObject.FindProperty(nameof(movementMode));
            inputDevices = serializedObject.FindProperty(nameof(inputDevices));
            movementSpeed = serializedObject.FindProperty(nameof(movementSpeed));
            smoothing = serializedObject.FindProperty(nameof(smoothing));
            deadZone = serializedObject.FindProperty(nameof(deadZone));
            movementBounds = serializedObject.FindProperty(nameof(movementBounds));
            hideWithoutSelection = serializedObject.FindProperty(nameof(hideWithoutSelection));
            enableRaycast = serializedObject.FindProperty(nameof(enableRaycast));
            fitToTarget = serializedObject.FindProperty(nameof(fitToTarget));
            targetPadding = serializedObject.FindProperty(nameof(targetPadding));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            preset = serializedObject.FindProperty(nameof(preset));
            customVisual = serializedObject.FindProperty(nameof(customVisual));
            outlineWidth = serializedObject.FindProperty(nameof(outlineWidth));
            cornerLength = serializedObject.FindProperty(nameof(cornerLength));
            centerSize = serializedObject.FindProperty(nameof(centerSize));
            edgeSoftness = serializedObject.FindProperty(nameof(edgeSoftness));
            styleProvider = serializedObject.FindProperty(nameof(styleProvider));
            useDefaultStylerPreset = serializedObject.FindProperty(nameof(useDefaultStylerPreset));
            stylerPreset = serializedObject.FindProperty(nameof(stylerPreset));
            normalColor = serializedObject.FindProperty(nameof(normalColor));
            targetColor = serializedObject.FindProperty(nameof(targetColor));
            pressedColor = serializedObject.FindProperty(nameof(pressedColor));
            normalStyle = serializedObject.FindProperty(nameof(normalStyle));
            targetStyle = serializedObject.FindProperty(nameof(targetStyle));
            pressedStyle = serializedObject.FindProperty(nameof(pressedStyle));
            cornerRadius = serializedObject.FindProperty(nameof(cornerRadius));
            radiusSyncMode = serializedObject.FindProperty(nameof(radiusSyncMode));
            selectionDuration = serializedObject.FindProperty(nameof(selectionDuration));
            transitionEase = serializedObject.FindProperty(nameof(transitionEase));
            customEase = serializedObject.FindProperty(nameof(customEase));
            dashMode = serializedObject.FindProperty(nameof(dashMode));
            dashLength = serializedObject.FindProperty(nameof(dashLength));
            dashGap = serializedObject.FindProperty(nameof(dashGap));
            dashOffset = serializedObject.FindProperty(nameof(dashOffset));
            roundedDashes = serializedObject.FindProperty(nameof(roundedDashes));
            dashAnimation = serializedObject.FindProperty(nameof(dashAnimation));
            dashSpeed = serializedObject.FindProperty(nameof(dashSpeed));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            InteractionFoldout();
            NavigationFoldout();
            AnimationFoldout();
            StyleFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();

            if (targets.Length != 1)
                return;

            VirtualCursor cursor = (VirtualCursor)target;
            if (cursor.GetComponentInParent<Canvas>() == null)
                EvoEditorGUI.Callout("Place the cursor under a Canvas, outside layout groups. Its visual automatically uses the highest canvas sort order.", CalloutType.Warning);
            if (cursor.inputModule != null && !Application.isPlaying && (cursor.inputModule.move == null || cursor.inputModule.submit == null))
                EvoEditorGUI.Callout("Assign Move (Vector2) and Submit (Button) actions on the UI Input Module. The cursor uses these bindings automatically.", CalloutType.Warning);
            if (cursor.preset == VirtualCursor.CursorPreset.Custom && cursor.customVisual == null)
                EvoEditorGUI.Callout("Assign a RectTransform prefab or scene object. A ring is shown until a custom visual is assigned.");
            if (cursor.customVisual != null && cursor.customVisual.GetComponentInChildren<VirtualCursor>(true) != null)
                EvoEditorGUI.Callout("The custom visual must not contain a Virtual Cursor component. Assign only its visual hierarchy.", CalloutType.Warning);
        }

        void InteractionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Interaction", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(enableRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(fitToTarget, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (fitToTarget.boolValue || fitToTarget.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(targetPadding, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void NavigationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Navigation", true, FoldoutOptions.WithIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(inputModule, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(inputDevices, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(movementMode, FieldOptions.WithoutBackground().WithoutSpace());
                        if (movementMode.hasMultipleDifferentValues || movementMode.intValue == (int)VirtualCursor.MovementMode.Free)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(movementSpeed);
                            EvoEditorGUI.Property(smoothing);
                            EvoEditorGUI.Property(deadZone);
                            EvoEditorGUI.Property(movementBounds, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
                    EvoEditorGUI.Toggle(hideWithoutSelection, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animation", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (movementMode.hasMultipleDifferentValues || movementMode.intValue == (int)VirtualCursor.MovementMode.FollowSelection)
                        EvoEditorGUI.Property(selectionDuration, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Property(transitionDuration, "Transition Duration", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(transitionEase, "Ease", FieldOptions.WithoutBackground().WithoutSpace());
                        if (transitionEase.hasMultipleDifferentValues || transitionEase.intValue == (int)Ease.Custom)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(customEase, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void StyleFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Style", false, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Center", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(centerSize, "Size");
                        EvoEditorGUI.Property(edgeSoftness, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(preset, "Frame", FieldOptions.WithoutBackground().WithoutSpace());
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        {
                            bool custom = !preset.hasMultipleDifferentValues && preset.intValue == (int)VirtualCursor.CursorPreset.Custom;
                            bool corners = !preset.hasMultipleDifferentValues && preset.intValue == (int)VirtualCursor.CursorPreset.Corners;
                          
                            if (custom || preset.hasMultipleDifferentValues)
                                EvoEditorGUI.Property(customVisual, FieldOptions.WithSpace(!custom));

                            if (!custom)
                            {
                                EvoEditorGUI.Property(outlineWidth);
                               
                                if (corners || preset.hasMultipleDifferentValues)
                                    EvoEditorGUI.Property(cornerLength, FieldOptions.WithSpace(!corners));
                                
                                if (!corners)
                                {
                                    EvoEditorGUI.Property(radiusSyncMode);
                                    EvoEditorGUI.Property(cornerRadius, "Fallback Radius", FieldOptions.WithoutSpace());
                                }
                            }
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();

                    if (preset.hasMultipleDifferentValues || preset.intValue == (int)VirtualCursor.CursorPreset.Ring
                        || preset.intValue == (int)VirtualCursor.CursorPreset.Outline)
                    {
                        EvoEditorGUI.AddPropertySpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            EvoEditorGUI.Property(dashMode, "Dashes", FieldOptions.WithoutBackground().WithoutSpace());
                            if (dashMode.hasMultipleDifferentValues || dashMode.intValue != (int)VirtualCursor.DashMode.None)
                            {
                                EvoEditorGUI.BeginContainer(nestDepth: 1);
                                {
                                    EvoEditorGUI.Toggle(roundedDashes);
                                    EvoEditorGUI.Property(dashLength);
                                    EvoEditorGUI.Property(dashGap);
                                    EvoEditorGUI.Property(dashOffset);
                                    bool animate = dashAnimation.hasMultipleDifferentValues || dashAnimation.intValue != (int)VirtualCursor.DashMode.None;
                                    EvoEditorGUI.Property(dashAnimation, "Animation", FieldOptions.WithSpace(animate));
                                    if (animate)
                                        EvoEditorGUI.Property(dashSpeed, FieldOptions.WithoutSpace());
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndColumn();
                    }

                    EvoEditorGUI.AddPropertySpace();
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(styleProvider, FieldOptions.WithoutBackground().WithoutSpace());
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    {
                        if (styleProvider.hasMultipleDifferentValues || styleProvider.intValue == (int)StyleProvider.Styler)
                            StylerEditor.PresetFields(useDefaultStylerPreset, stylerPreset, "Preset", addSpace: true);

                        DrawColor(normalColor, normalStyle, "Normal", true);
                        DrawColor(targetColor, targetStyle, "On Target", true);
                        DrawColor(pressedColor, pressedStyle, "Pressed", false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawColor(SerializedProperty color, SerializedProperty mapping, string label, bool addSpace)
        {
            bool styler = styleProvider.hasMultipleDifferentValues || styleProvider.intValue == (int)StyleProvider.Styler;
            if (styler)
                StylerEditor.ColorDropdown(stylerPreset, mapping, label, addSpace, invertColor: false);
            if (styleProvider.hasMultipleDifferentValues || !styler || stylerPreset.objectReferenceValue == null)
                EvoEditorGUI.Property(color, styler ? label + " Fallback" : label, FieldOptions.WithSpace(addSpace));
        }
    }
}