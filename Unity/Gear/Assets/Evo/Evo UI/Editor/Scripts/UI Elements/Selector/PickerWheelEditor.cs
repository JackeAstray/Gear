using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PickerWheel))]
    public class PickerWheelEditor : UnityEditor.Editor
    {
        // Items
        SerializedProperty items;
        SerializedProperty selectedIndex;
        SerializedProperty loop;
        SerializedProperty itemTemplate;

        // Settings
        SerializedProperty interactable;
        SerializedProperty itemHeight;
        SerializedProperty unselectedAlpha;
        SerializedProperty decelerationRate;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onValueChanged;

        void OnEnable()
        {
            // An inspector restored after a domain reload can briefly hold a destroyed target
            if (target == null)
                return;

            items = serializedObject.FindProperty(nameof(items));
            selectedIndex = serializedObject.FindProperty(nameof(selectedIndex));
            loop = serializedObject.FindProperty(nameof(loop));
            itemTemplate = serializedObject.FindProperty(nameof(itemTemplate));

            interactable = serializedObject.FindProperty("m_Interactable");
            itemHeight = serializedObject.FindProperty(nameof(itemHeight));
            unselectedAlpha = serializedObject.FindProperty(nameof(unselectedAlpha));
            decelerationRate = serializedObject.FindProperty(nameof(decelerationRate));

            navigation = serializedObject.FindProperty("m_Navigation");
            onValueChanged = serializedObject.FindProperty(nameof(onValueChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            bool managed = IsManagedByTimePicker();
            ItemsFoldout(managed);
            SettingsFoldout(managed);
            InteractiveEditor.NavigationFoldout(navigation);
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        bool IsManagedByTimePicker()
        {
            foreach (Object item in targets)
            {
                var wheel = (PickerWheel)item;
                TimePicker picker = wheel.GetComponentInParent<TimePicker>(true);
                if (picker == null || (picker.hourWheel != wheel && picker.minuteWheel != wheel && picker.periodWheel != wheel))
                    return false;
            }

            return true;
        }

        void ItemsFoldout(bool managed)
        {
            if (EvoEditorGUI.BeginFoldout(target, "Items", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (managed)
                    {
                        EvoEditorGUI.Callout("Items, looping and row height are set by the Time Picker.", invertColor: true, spaceBefore: 0, spaceAfter: 0);
                    }
                    else
                    {
                        EvoEditorGUI.Property(items, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Slider(selectedIndex, 0, Mathf.Max(0, items.arraySize - 1), "Selected Index", FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Toggle(loop, FieldOptions.WithInvertedColor().WithoutSpace());
                    }

                    EvoEditorGUI.AddLayoutSpace();
                    TemplateField();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void TemplateField()
        {
            EvoEditorGUI.Property(itemTemplate, FieldOptions.WithInvertedColor().WithoutSpace());
            if (targets.Length != 1)
                return;

            RectTransform template = ((PickerWheel)target).ItemTemplate;
            if (template == null)
            {
                EvoEditorGUI.Callout("Add a child with a text component. The wheel copies it for every row.",
                    CalloutType.Warning, true, spaceAfter: 0);
                return;
            }

            EvoEditorGUI.Callout($"Every row copies \"{template.name}\". Edit it to change the font, color or size of the whole wheel.",
                invertColor: true);
            if (EvoEditorGUI.Button("Select Template", options: ButtonOptions.WithInvertedColor()))
                Selection.activeGameObject = template.gameObject;
        }

        void SettingsFoldout(bool managed)
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, "Interactable", FieldOptions.WithInvertedColor());
                  
                    if (!managed)
                        EvoEditorGUI.Property(itemHeight, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Property(unselectedAlpha, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(decelerationRate, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}