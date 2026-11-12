using System.Linq;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(ComparisonSlider))]
    public class ComparisonSliderEditor : SelectableEditor
    {
        // Content
        SerializedProperty firstSprite;
        SerializedProperty secondSprite;
        SerializedProperty value;

        // Sizing
        SerializedProperty autoHeight;
        SerializedProperty padding;
        SerializedProperty orientation;
        SerializedProperty reverse;
        SerializedProperty clickToMove;
        SerializedProperty dragOnHover;
        SerializedProperty inputStep;
        SerializedProperty interactable;

        // References
        SerializedProperty firstImage;
        SerializedProperty secondImage;
        SerializedProperty handleRect;
        SerializedProperty dividerRect;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            base.OnEnable();
            InitializeEditorState();
            UINavigationEditor.PrepareForVisualize(this);
            Undo.undoRedoPerformed += RefreshTargets;
        }

        void InitializeEditorState()
        {
            firstSprite = serializedObject.FindProperty(nameof(firstSprite));
            secondSprite = serializedObject.FindProperty(nameof(secondSprite));
            value = serializedObject.FindProperty(nameof(value));

            autoHeight = serializedObject.FindProperty(nameof(autoHeight));
            padding = serializedObject.FindProperty(nameof(padding));
            orientation = serializedObject.FindProperty(nameof(orientation));
            reverse = serializedObject.FindProperty(nameof(reverse));
            clickToMove = serializedObject.FindProperty(nameof(clickToMove));
            dragOnHover = serializedObject.FindProperty(nameof(dragOnHover));
            inputStep = serializedObject.FindProperty(nameof(inputStep));
            interactable = serializedObject.FindProperty("m_Interactable");

            firstImage = serializedObject.FindProperty(nameof(firstImage));
            secondImage = serializedObject.FindProperty(nameof(secondImage));
            handleRect = serializedObject.FindProperty(nameof(handleRect));
            dividerRect = serializedObject.FindProperty(nameof(dividerRect));

            navigation = serializedObject.FindProperty("m_Navigation");
            onValueChanged = serializedObject.FindProperty(nameof(onValueChanged));
        }

        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshTargets;
            UINavigationEditor.RemoveFromVisualize(this);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            ContentFoldout();
            SettingsFoldout();
            InteractiveEditor.EffectsFoldout(serializedObject);
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();

            if (serializedObject.hasModifiedProperties)
            {
                foreach (ComparisonSlider slider in targets.Cast<ComparisonSlider>())
                    RecordVisuals(slider);

                serializedObject.ApplyModifiedProperties();

                // References may have changed; record new visuals before refreshing them
                foreach (ComparisonSlider slider in targets.Cast<ComparisonSlider>())
                    RecordVisuals(slider);

                RefreshTargets();
            }

            DrawSetup();
            EvoEditorGUI.EndCenteredInspector();
        }

        void ContentFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Content", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(firstSprite, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(secondSprite, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(value, FieldOptions.WithInvertedColor().WithoutSpace());

                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Interaction");
                    {
                        EvoEditorGUI.Toggle(interactable);
                        EvoEditorGUI.Toggle(clickToMove);
                        EvoEditorGUI.Toggle(dragOnHover);
                        EvoEditorGUI.Property(inputStep, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Layout");
                    {
                        EvoEditorGUI.Toggle(autoHeight);
                        EvoEditorGUI.Toggle(reverse);
                        EvoEditorGUI.Property(padding);
                        EvoEditorGUI.Property(orientation, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(firstImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(secondImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(handleRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dividerRect, FieldOptions.WithInvertedColor().WithoutSpace());
                    DrawWarnings();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void DrawSetup()
        {
            bool needsSetup = false;
            foreach (ComparisonSlider slider in targets.Cast<ComparisonSlider>())
            {
                if (HasMissingCoreReferences(slider))
                {
                    needsSetup = true;
                    break;
                }
            }

            if (!needsSetup)
                return;

            EvoEditorGUI.Callout("Assign both Image references or create the missing visuals below.",
                CalloutType.Warning, spaceBefore: 3, spaceAfter: 3);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (EvoEditorGUI.Button("Create Missing Visuals"))
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Set Up Comparison Slider");

                    foreach (ComparisonSlider slider in targets.Cast<ComparisonSlider>())
                    {
                        if (HasMissingCoreReferences(slider))
                            CreateMissingVisuals(slider);
                    }

                    Undo.CollapseUndoOperations(group);
                    serializedObject.Update();
                }
            }
        }

        void DrawWarnings()
        {
            foreach (ComparisonSlider slider in targets.Cast<ComparisonSlider>())
            {
                if (!ValidReferences(slider))
                    EvoEditorGUI.Callout(slider.name + ": Comparison visuals must be distinct direct children of this object.", CalloutType.Error);

                if (slider.TryGetComponent<Graphic>(out var graphic) && !graphic.raycastTarget)
                    EvoEditorGUI.Callout(slider.name + ": Enable Raycast Target on the root Graphic for pointer input.", CalloutType.Warning);

            }
        }

        static bool HasMissingCoreReferences(ComparisonSlider slider) => slider.firstImage == null || slider.secondImage == null;

        static bool ValidReferences(ComparisonSlider slider)
        {
            Transform root = slider.transform;
            ProceduralShape first = slider.firstImage;
            ProceduralShape second = slider.secondImage;
            RectTransform handle = slider.handleRect;
            RectTransform divider = slider.dividerRect;

            return (first == null || first.transform.parent == root) && (second == null || second.transform.parent == root)
                && (first == null || first != second)
                && (handle == null || (handle.parent == root && (first == null || handle != first.rectTransform)
                && (second == null || handle != second.rectTransform)))
                && (divider == null || (divider.transform.parent == root && divider != first && divider != second && divider != handle));
        }

        void RefreshTargets()
        {
            foreach (var obj in targets)
            {
                if (obj is not ComparisonSlider slider || slider == null)
                    continue;

                slider.Refresh();

                if (!Application.isPlaying)
                    RecordPrefabVisuals(slider);
            }

            SceneView.RepaintAll();
        }

        static Object[] Visuals(ComparisonSlider slider) => new Object[]
        {
            slider.transform,
            slider.firstImage,
            slider.firstImage != null ? slider.firstImage.rectTransform : null,
            slider.secondImage,
            slider.secondImage != null ? slider.secondImage.rectTransform : null,
            slider.handleRect,
            slider.dividerRect
        };

        static void RecordVisuals(ComparisonSlider slider)
        {
            foreach (var visual in Visuals(slider))
            {
                if (visual != null)
                    Undo.RecordObject(visual, "Edit Comparison Slider");
            }
        }

        static void RecordPrefabVisuals(ComparisonSlider slider)
        {
            foreach (var visual in Visuals(slider))
            {
                if (visual == null)
                    continue;

                EditorUtility.SetDirty(visual);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            }
        }

        static void CreateMissingVisuals(ComparisonSlider slider)
        {
            if (!HasMissingCoreReferences(slider) || !ValidReferences(slider))
                return;

            Undo.RecordObject(slider, "Set Up Comparison Slider");
            RecordVisuals(slider);

            ProceduralShape first = slider.firstImage != null ? slider.firstImage : CreateImage("First Image", slider.transform);
            ProceduralShape second = slider.secondImage != null ? slider.secondImage : CreateImage("Second Image", slider.transform);
            RectTransform divider = slider.dividerRect;
            RectTransform handle = slider.handleRect;

            if (divider == null)
            {
                ProceduralShape temp = CreateImage("Divider", slider.transform);
                divider = temp.rectTransform;
                divider.sizeDelta = new Vector2(2f, 2f);
            }

            if (handle == null)
            {
                ProceduralShape grip = CreateImage("Handle", slider.transform);
                handle = grip.rectTransform;
                handle.sizeDelta = new Vector2(32f, 32f);
            }

            Undo.RecordObject(divider.transform, "Order Comparison Visuals");
            Undo.RecordObject(handle, "Order Comparison Visuals");
            divider.transform.SetAsLastSibling();
            handle.SetAsLastSibling();
            slider.SetReferences(first, second, handle, divider);
            EditorUtility.SetDirty(slider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(slider);
            RecordPrefabVisuals(slider);
        }

        static ProceduralShape CreateImage(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ProceduralShape));
            Undo.RegisterCreatedObjectUndo(obj, "Create Comparison Visual");
            Undo.SetTransformParent(obj.transform, parent, "Parent Comparison Visual");
            obj.layer = parent.gameObject.layer;
            obj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            obj.transform.localScale = Vector3.one;

            ProceduralShape image = obj.GetComponent<ProceduralShape>();
            image.raycastTarget = false;
            return image;
        }
    }
}