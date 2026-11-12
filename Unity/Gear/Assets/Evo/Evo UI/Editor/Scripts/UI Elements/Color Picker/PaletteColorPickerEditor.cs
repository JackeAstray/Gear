using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(PaletteColorPicker))]
    public class PaletteColorPickerEditor : UnityEditor.Editor
    {
        // Cache
        SerializedProperty selectedIndex, rings;
        ReorderableListOptions ringsListOptions;
        SerializedProperty graphicColor, material, raycastTarget, maskable;
        SerializedProperty interactable, inputNavigation, allowDeselection, clearOnRightClick;
        SerializedProperty backgroundColor, swatchRadius, borderWidth, glowWidth;
        SerializedProperty idleGlow, hoverGlow, hoverScale, selectedScale;
        SerializedProperty useUnscaledTime, animationDuration, pushDistance, pushRadius, selectedBorderWidth;
        SerializedProperty onColorChanged, onSelectionChanged;

        void OnEnable()
        {
            selectedIndex = serializedObject.FindProperty(nameof(selectedIndex));
            rings = serializedObject.FindProperty(nameof(rings));
            ringsListOptions = ReorderableListOptions.WithControlName("PaletteColorPicker.Rings")
                .WithTitle("Palette")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add a ring to populate this palette.")
                .WithAddLabel("Add Ring")
                .WithItemInitializer(InitializeRing)
                .WithDeleteTooltip("Delete ring")
                .WithContentHeader(DrawPaletteHeader);

            allowDeselection = serializedObject.FindProperty(nameof(allowDeselection));
            clearOnRightClick = serializedObject.FindProperty(nameof(clearOnRightClick));
            interactable = serializedObject.FindProperty(nameof(interactable));
            inputNavigation = serializedObject.FindProperty(nameof(inputNavigation));
              
            backgroundColor = serializedObject.FindProperty(nameof(backgroundColor));
            swatchRadius = serializedObject.FindProperty(nameof(swatchRadius));
            borderWidth = serializedObject.FindProperty(nameof(borderWidth));
            glowWidth = serializedObject.FindProperty(nameof(glowWidth));
            idleGlow = serializedObject.FindProperty(nameof(idleGlow));
            hoverGlow = serializedObject.FindProperty(nameof(hoverGlow));
            hoverScale = serializedObject.FindProperty(nameof(hoverScale));
            selectedScale = serializedObject.FindProperty(nameof(selectedScale));

            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            animationDuration = serializedObject.FindProperty(nameof(animationDuration));
            pushDistance = serializedObject.FindProperty(nameof(pushDistance));
            pushRadius = serializedObject.FindProperty(nameof(pushRadius));
            selectedBorderWidth = serializedObject.FindProperty(nameof(selectedBorderWidth));

            graphicColor = serializedObject.FindProperty("m_Color");
            material = serializedObject.FindProperty("m_Material");
            raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            maskable = serializedObject.FindProperty("m_Maskable");

            onColorChanged = serializedObject.FindProperty(nameof(onColorChanged));
            onSelectionChanged = serializedObject.FindProperty(nameof(onSelectionChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();
            EditorGUI.BeginChangeCheck();

            PaletteFoldout();
            SettingsFoldout();
            AppearanceFoldout();
            MotionFoldout();
            GraphicFoldout();
            EventsFoldout();

            EvoEditorGUI.Callout("Inspired by Spatial (@tbsrnstrm)", spaceAfter: 0);

            bool controlsChanged = EditorGUI.EndChangeCheck();
            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void PaletteFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, rings, GetRingTitle, DrawRing, ringsListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(inputNavigation, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(allowDeselection, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(clearOnRightClick, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AppearanceFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Appearance", false, FoldoutOptions.WithIcon("UI_Effect")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(backgroundColor, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(swatchRadius, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(borderWidth, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(glowWidth, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(idleGlow, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(hoverGlow, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(hoverScale, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(selectedScale, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void MotionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Motion", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(animationDuration, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(selectedBorderWidth, "Selected Width", FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(pushDistance, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(pushRadius, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void GraphicFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Graphic", false, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(graphicColor, "Tint", FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(material, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(raycastTarget, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(maskable, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onColorChanged, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onSelectionChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void DrawPaletteHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.Property(selectedIndex, FieldOptions.WithoutBackground().WithoutSpace());
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        static string GetRingTitle(SerializedProperty ring, int index) => $"Ring {index + 1}";

        static void DrawRing(SerializedProperty ring)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true);
            EvoEditorGUI.Property(ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.radius)));
            EvoEditorGUI.Property(ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.rotation)));
            EvoEditorGUI.ArrayProperty(ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.colors)), FieldOptions.WithoutSpace());
            EvoEditorGUI.EndContainer();
        }

        static void InitializeRing(SerializedProperty ring)
        {
            ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.radius)).floatValue = 0.6f;
            ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.rotation)).floatValue = 0f;
            ring.FindPropertyRelative(nameof(PaletteColorPicker.Ring.colors)).ClearArray();
            ring.isExpanded = true;
        }
    }
}