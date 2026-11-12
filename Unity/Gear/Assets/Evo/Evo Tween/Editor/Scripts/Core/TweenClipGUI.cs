using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween.Modules;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Shared clip/settings GUI used by TweenAnimator, preset assets, and property drawers.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed class TweenClipGUI
    {
        internal readonly struct ModuleTypeInfo
        {
            public readonly Type Type;
            public readonly ModuleMenuAttribute Menu;
            public readonly string MenuPath;
            public readonly string DisplayName;
            public readonly Type TargetType;

            public ModuleTypeInfo(Type type, ModuleMenuAttribute menu)
            {
                Type = type;
                Menu = menu;
                MenuPath = GetModulePath(menu, "/");
                DisplayName = GetModulePath(menu, " / ");
                TargetType = GetTargetType(type);
            }

            // Modules declare their target through an instance property, so one throwaway instance answers it
            static Type GetTargetType(Type moduleType)
            {
                try { return (Activator.CreateInstance(moduleType) as TweenModule)?.TargetType; }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return null;
                }
            }
        }

        internal sealed class FieldLayout
        {
            public readonly string Name;
            public readonly ModuleFieldRole Role;
            public readonly int Order;
            public readonly int Sequence;
            public readonly string Label;
            public readonly ModuleShowIfAttribute ShowIf;
            public readonly ModuleRangeAttribute Range;
            public readonly RangeAttribute UnityRange;
            public readonly MinAttribute Minimum;

            public FieldLayout(string name, ModuleFieldRole role, int order, int sequence, string label,
                ModuleShowIfAttribute showIf, ModuleRangeAttribute range, RangeAttribute unityRange, MinAttribute minimum)
            {
                Name = name;
                Role = role;
                Order = order;
                Sequence = sequence;
                Label = label;
                ShowIf = showIf;
                Range = range;
                UnityRange = unityRange;
                Minimum = minimum;
            }
        }

        internal sealed class ModuleLayout
        {
            public readonly List<FieldLayout> Settings = new();
            public readonly List<FieldLayout> FromValues = new();
            public readonly List<FieldLayout> ToValues = new();
            public readonly List<FieldLayout> OtherValues = new();
        }

        sealed class SettingsSnapshot
        {
            public TweenModule Module;
            public float Delay;
            public bool HoldBeforeStart;
            public float Duration;
            public LoopMode Loop;
            public int Iterations;
            public Ease Ease;
            public AnimationCurve CustomEase;

            public SettingsSnapshot Clone()
            {
                return new SettingsSnapshot
                {
                    Module = Module?.Clone(),
                    Delay = Delay,
                    HoldBeforeStart = HoldBeforeStart,
                    Duration = Duration,
                    Loop = Loop,
                    Iterations = Iterations,
                    Ease = Ease,
                    CustomEase = CustomEase != null ? new AnimationCurve(CustomEase.keys) : null
                };
            }
        }

        sealed class ClipSnapshot
        {
            public Component Target;
            public ClipFlow Flow;
            public SettingsSnapshot InlineSettings;

            public ClipSnapshot Clone()
            {
                return new ClipSnapshot
                {
                    Target = Target,
                    Flow = Flow,
                    InlineSettings = InlineSettings?.Clone()
                };
            }
        }

        static readonly Dictionary<Type, ModuleLayout> LayoutCache = new();
        static readonly Func<SerializedProperty, int, string> ClipTitleGetter = GetClipTitle;
        static readonly Func<SerializedProperty, Texture2D> ClipIconGetter = GetFlowIcon;
        static readonly Action<SerializedProperty> CopyClipAction = CopyClip;
        static readonly Action<SerializedProperty> PasteClipAction = PasteClip;
        static readonly Func<SerializedProperty, bool> CanPasteClipGetter = CanPasteClipItem;
        static readonly List<ModuleTypeInfo> ModuleTypes = BuildModuleTypes();
        static readonly string[] ModuleTypeNames = BuildModuleTypeNames();
        static readonly string[] MissingModuleTypeNames = BuildMissingModuleTypeNames();

        static readonly List<ClipSnapshot> clipClipboard = new();
        static SettingsSnapshot settingsClipboard;

        readonly Action<SerializedProperty> drawClipAction;
        readonly Action<ReorderableListAddContext> showAddMenuAction;
        readonly Action<ReorderableListAddContext> editPresetAction;
        readonly Action drawSequenceHeaderAction;
        readonly ReorderableListOptions reorderableListOptions;
        readonly bool compactFoldouts;
        readonly bool compactItems;
        readonly bool invertColors;
        readonly bool boldHeader;
        ReorderableListAddContext addContext;
        Component addOwner;
        SerializedProperty sequenceTimeMode;
        SerializedProperty sequencePreset;
        SerializedProperty targetBindings;
        TweenSequencePreset activePreset;
        TweenSequencePreset cachedPreset;
        SerializedObject cachedPresetOwner;
        Action beforeChange;
        Action afterChange;
        Action drawContentHeader;
        bool definitionsReadOnly;
        bool drawTargets;
        float detailAvailableWidth;

        public bool IsDragging { get; private set; }

        /// <summary>
        /// Creates a reusable sequence inspector.
        /// </summary>
        public TweenClipGUI(string title = "Clips", bool compactFoldouts = false, bool showHeaderIcon = true,
            bool showItemIcons = true, bool invertColors = false, bool boldHeader = true, bool compactItems = true)
        {
            drawClipAction = DrawClip;
            showAddMenuAction = ShowAddMenu;
            editPresetAction = EditPreset;
            drawSequenceHeaderAction = DrawSequenceHeader;
            this.compactFoldouts = compactFoldouts;
            this.compactItems = compactItems;
            this.invertColors = invertColors;
            this.boldHeader = boldHeader;

            ReorderableListOptions options = ReorderableListOptions
                .WithControlName($"TweenClipGUI.{title}")
                .WithTitle(title)
                .WithEmptyMessage("Add a clip to build this sequence.")
                .WithAddLabel("Add Clip")
                .WithAddAction(showAddMenuAction)
                .WithDeleteTooltip("Delete clip")
                .WithClipboard(CopyClipAction, CanPasteClipGetter, PasteClipAction);

            if (showHeaderIcon)
                options = options.WithIcon(EvoEditorGUI.GetIcon("Tween_Clips"));
            if (showItemIcons)
                options = options.WithItemIcon(ClipIconGetter);

            reorderableListOptions = options;
        }

        public void DrawSequence(SerializedObject owner, SerializedProperty sequence, Action beforeStructuralChange = null,
            Action afterStructuralChange = null, Action contentHeader = null)
        {
            if (sequence == null)
                return;

            sequenceTimeMode = sequence.FindPropertyRelative("timeMode");
            sequencePreset = sequence.FindPropertyRelative("preset");
            targetBindings = sequence.FindPropertyRelative("presetTargets");
            SerializedProperty inlineClips = sequence.FindPropertyRelative("clips");
            activePreset = sequencePreset?.objectReferenceValue as TweenSequencePreset;
            beforeChange = beforeStructuralChange;
            afterChange = afterStructuralChange;
            drawContentHeader = contentHeader;

            SerializedObject clipOwner = owner;
            SerializedProperty clips;
            ReorderableListOptions options = reorderableListOptions
                .WithChangeCallbacks(beforeStructuralChange, afterStructuralChange)
                .WithContentHeader(drawSequenceHeaderAction)
                .WithListClipboard(sequence);

            addOwner = owner.targetObject as Component;
            drawTargets = true;
            definitionsReadOnly = activePreset != null;

            if (activePreset == null) { clips = inlineClips; }
            else
            {
                clipOwner = GetPresetOwner(activePreset);
                clips = clipOwner.FindProperty("clips");
                SynchronizeTargetBindings(targetBindings, inlineClips, clips?.arraySize ?? 0);
                options = options
                    .WithEmptyMessage("The attached preset contains no clips.")
                    .WithAddLabel("Edit Preset")
                    .WithAddIcon(IconLibrary.Preset)
                    .WithAddAction(editPresetAction)
                    .WithReadOnlyItems();
            }

            DrawList(clipOwner, clips, options);
        }

        public void DrawPreset(SerializedObject owner, SerializedProperty clips)
        {
            sequenceTimeMode = null;
            sequencePreset = null;
            targetBindings = null;
            activePreset = null;
            addOwner = null;
            beforeChange = null;
            afterChange = null;
            drawContentHeader = null;
            definitionsReadOnly = false;
            drawTargets = false;

            DrawList(owner, clips, reorderableListOptions.WithChangeCallbacks(null, null).WithContentHeader(null));
        }

        /// <summary>
        /// Draws the polished editor for one clip without wrapping it in a reorderable list.
        /// This is used by focused editing surfaces such as the Tween Timeline window.
        /// </summary>
        public void DrawClipDetails(SerializedProperty clip, SerializedProperty target = null, Component owner = null,
            bool readOnly = false, Action beforeValueChange = null, Action afterValueChange = null, float availableWidth = 0f)
        {
            if (clip == null)
                return;

            Component previousOwner = addOwner;
            Action previousBeforeChange = beforeChange;
            Action previousAfterChange = afterChange;
            bool previousReadOnly = definitionsReadOnly;
            float previousAvailableWidth = detailAvailableWidth;

            addOwner = owner;
            beforeChange = beforeValueChange;
            afterChange = afterValueChange;
            definitionsReadOnly = readOnly;
            detailAvailableWidth = availableWidth;

            try { DrawClip(clip, target); }
            finally
            {
                addOwner = previousOwner;
                beforeChange = previousBeforeChange;
                afterChange = previousAfterChange;
                definitionsReadOnly = previousReadOnly;
                detailAvailableWidth = previousAvailableWidth;
            }
        }

        /// <summary>
        /// Draws the Clip section for several clips at once. A field whose values differ shows a dash, and an edit
        /// applies to every clip. An optional extra section is drawn below it in the same style; its callback
        /// receives whether its fields use inverted colors.
        /// </summary>
        public void DrawSharedClipDetails(IReadOnlyList<SerializedProperty> clips, float availableWidth = 0f,
            string sectionTitle = null, Action<bool> drawSection = null)
        {
            if (clips == null || clips.Count == 0 || clips[0] == null)
                return;

            float previousAvailableWidth = detailAvailableWidth;
            detailAvailableWidth = availableWidth;

            try
            {
                EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: invertColors ? 0 : 1);
                DrawCommonClipSection(clips[0], clips, settingsReadOnly: false);

                if (drawSection != null)
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
                    EvoEditorGUI.BeginContainer(sectionTitle);
                    drawSection(!invertColors);
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }

                EvoEditorGUI.EndContainer();
            }
            finally { detailAvailableWidth = previousAvailableWidth; }
        }

        void DrawList(SerializedObject owner, SerializedProperty clips, ReorderableListOptions options)
        {
            if (owner == null || clips == null)
                return;

            IsDragging = EvoEditorGUI.ReorderableList(owner, clips, ClipTitleGetter, drawClipAction,
                options, compactItems, compactFoldouts, invertColors, boldHeader, maxHeight: 502f);
        }

        SerializedObject GetPresetOwner(TweenSequencePreset preset)
        {
            if (cachedPreset != preset || cachedPresetOwner == null || cachedPresetOwner.targetObject == null)
            {
                cachedPreset = preset;
                cachedPresetOwner = new SerializedObject(preset);
            }

            cachedPresetOwner.UpdateIfRequiredOrScript();
            return cachedPresetOwner;
        }

        void DrawSequenceHeader()
        {
            if (sequenceTimeMode == null && sequencePreset == null && drawContentHeader == null)
                return;

            drawContentHeader?.Invoke();

            if (sequencePreset != null)
                EvoEditorGUI.Property(sequencePreset, "Preset", FieldOptions.WithInvertedColor(!invertColors));
            if (sequenceTimeMode != null)
                EvoEditorGUI.Property(sequenceTimeMode, "Time Mode", FieldOptions.WithInvertedColor(!invertColors).WithoutSpace());

            EvoEditorGUI.AddPropertySpace();
        }

        void EditPreset(ReorderableListAddContext _)
        {
            if (activePreset == null)
                return;

            Selection.activeObject = activePreset;
            EditorGUIUtility.PingObject(activePreset);
        }

        SerializedProperty GetTargetProperty(SerializedProperty clip)
        {
            if (!drawTargets)
                return null;
            if (!definitionsReadOnly)
                return clip.FindPropertyRelative("target");

            int index = GetArrayIndex(clip.propertyPath);
            return targetBindings != null && index >= 0 && index < targetBindings.arraySize
                ? targetBindings.GetArrayElementAtIndex(index)
                : null;
        }

        /// <summary>
        /// Resizes a sequence's preset target bindings to the preset's clip count. New bindings start from the inline
        /// clip targets at the same index. Returns true when the bindings changed.
        /// </summary>
        internal static bool SynchronizeTargetBindings(SerializedProperty bindings, SerializedProperty inlineClips, int count)
        {
            if (bindings == null || !bindings.isArray || bindings.arraySize == count)
                return false;

            int previousCount = bindings.arraySize;
            bindings.arraySize = count;
            for (int i = previousCount; i < count; i++)
            {
                SerializedProperty inlineTarget = inlineClips != null && i < inlineClips.arraySize
                    ? inlineClips.GetArrayElementAtIndex(i).FindPropertyRelative("target")
                    : null;
                bindings.GetArrayElementAtIndex(i).objectReferenceValue = inlineTarget?.objectReferenceValue;
            }
            return true;
        }

        static int GetArrayIndex(string propertyPath)
        {
            const string token = ".Array.data[";
            int start = propertyPath.LastIndexOf(token, StringComparison.Ordinal);
            if (start < 0)
                return -1;

            start += token.Length;
            int end = propertyPath.IndexOf(']', start);
            return end > start && int.TryParse(propertyPath[start..end], out int index) ? index : -1;
        }

        void DrawClip(SerializedProperty clip) => DrawClip(clip, GetTargetProperty(clip));

        void DrawClip(SerializedProperty clip, SerializedProperty target)
        {
            SerializedProperty effectiveSettings = clip.FindPropertyRelative("settings");

            if (effectiveSettings == null)
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Callout("This clip has no settings payload.", type: CalloutType.Error);
                EvoEditorGUI.EndContainer();
                return;
            }

            SerializedProperty type = effectiveSettings.FindPropertyRelative("module");
            ModuleLayout layout = type?.managedReferenceValue is TweenModule module ? GetModuleLayout(module.GetType()) : null;

            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: invertColors ? 0 : 1);
            {
                DrawFieldsSection(target, effectiveSettings, definitionsReadOnly);
                DrawCommonClipSection(clip, null, definitionsReadOnly);
                using (new EditorGUI.DisabledScope(definitionsReadOnly))
                {
                    if (layout != null)
                    {
                        DrawValuesSection(type, effectiveSettings, layout);
                        DrawModuleSettingsSection(type, effectiveSettings, layout.Settings);
                    }
                }
            }
            EvoEditorGUI.EndContainer();
        }

        void DrawFieldsSection(SerializedProperty target, SerializedProperty settings, bool settingsReadOnly)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
            EvoEditorGUI.BeginContainer("Fields", nestDepth: invertColors ? 1 : 0);

            using (new EditorGUI.DisabledScope(settingsReadOnly))
                DrawTypeDropdown(settings, target, addOwner, !invertColors, beforeChange, afterChange);

            TweenModule module = settings.FindPropertyRelative("module")?.managedReferenceValue as TweenModule;
            if (target != null)
                DrawTargetField(target, module?.TargetType, !invertColors);

            if (target != null && module?.TargetType != null && target.objectReferenceValue != null &&
                !module.TargetType.IsInstanceOfType(target.objectReferenceValue))
            {
                EvoEditorGUI.Callout($"The selected target must be a {module.TargetType.Name}.",
                    type: CalloutType.Warning, invertColor: !invertColors, spaceAfter: 0);
            }

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        static void DrawTargetField(SerializedProperty target, Type targetType, bool invertColor)
        {
            EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor(invertColor));
            Rect rect = EditorGUILayout.GetControlRect();
            DrawTargetField(rect, target, targetType);
            EvoEditorGUI.EndRow();
        }

        internal static void DrawTargetField(Rect rect, SerializedProperty target, Type targetType)
        {
            if (target == null)
                return;

            Type acceptedType = GetAcceptedTargetType(targetType);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = target.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object value = EditorGUI.ObjectField(rect, new GUIContent("Target"), target.objectReferenceValue, acceptedType, true);
            if (EditorGUI.EndChangeCheck())
                target.objectReferenceValue = value;
            EditorGUI.showMixedValue = previousMixedValue;
        }

        internal static Type GetEffectiveTargetType(SerializedProperty clip) => GetAcceptedTargetType(GetEffectiveModule(clip)?.TargetType);

        static Type GetAcceptedTargetType(Type targetType) => targetType != null && typeof(UnityEngine.Object).IsAssignableFrom(targetType)
            ? targetType : typeof(Component);

        /// <summary>
        /// Draws the timing and easing fields of <paramref name="clip"/>, or the values it shares with the other clips in
        /// <paramref name="sharedWith"/> when several clips are edited together.
        /// </summary>
        void DrawCommonClipSection(SerializedProperty clip, IReadOnlyList<SerializedProperty> sharedWith, bool settingsReadOnly)
        {
            SerializedProperty loop = FindClipField(clip, "loop");
            SerializedProperty ease = FindClipField(clip, "ease");
            SerializedProperty customEase = FindClipField(clip, "customEase");
            bool single = sharedWith == null || sharedWith.Count < 2;

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
            EvoEditorGUI.BeginContainer("Clip");

            using (new EditorGUI.DisabledScope(settingsReadOnly))
            {
                DrawClipField(clip, sharedWith, "flow", "Flow", FieldOptions.WithInvertedColor(!invertColors));
                DrawClipField(clip, sharedWith, "ease", "Ease", FieldOptions.WithInvertedColor(!invertColors));
                if (single && ease != null && (Ease)ease.enumValueIndex == Ease.Custom && customEase != null)
                    EvoEditorGUI.Property(customEase, "Custom Ease", FieldOptions.WithInvertedColor(!invertColors));

                float availableWidth = detailAvailableWidth > 0f ? detailAvailableWidth : EditorGUIUtility.currentViewWidth;
                if (availableWidth >= 390f)
                {
                    GUILayout.BeginHorizontal();
                    DrawClipField(clip, sharedWith, "duration", "Duration", FieldOptions.WithInvertedColor(!invertColors).WithLabelWidth(69));
                    DrawClipField(clip, sharedWith, "delay", "Delay", FieldOptions.WithoutSpace().WithInvertedColor(!invertColors).WithLabelWidth(69));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    DrawClipField(clip, sharedWith, "duration", "Duration", FieldOptions.WithInvertedColor(!invertColors));
                    DrawClipField(clip, sharedWith, "delay", "Delay", FieldOptions.WithoutSpace().WithInvertedColor(!invertColors));
                }

                EvoEditorGUI.AddLayoutSpace();
                EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(!invertColors));
                DrawClipField(clip, sharedWith, "loop", "Loop", FieldOptions.WithoutSpace().WithoutBackground());
                if (loop != null && (LoopMode)loop.enumValueIndex != LoopMode.None && !IsMixed(loop, sharedWith, "loop"))
                {
                    EvoEditorGUI.BeginContainer();
                    DrawClipField(clip, sharedWith, "iterations", "Iterations", FieldOptions.WithoutSpace().WithInvertedColor(invertColors));
                    EvoEditorGUI.EndContainer();
                }
                EvoEditorGUI.EndColumn();
            }

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        // Flow lives on the clip itself, the other common fields on its settings
        static SerializedProperty FindClipField(SerializedProperty clip, string name)
        {
            return name == "flow" ? clip?.FindPropertyRelative(name) : clip?.FindPropertyRelative("settings")?.FindPropertyRelative(name);
        }

        static bool IsMixed(SerializedProperty property, IReadOnlyList<SerializedProperty> sharedWith, string name)
        {
            if (sharedWith == null)
                return false;

            for (int i = 0; i < sharedWith.Count; i++)
            {
                SerializedProperty other = FindClipField(sharedWith[i], name);
                if (other != null && !SerializedProperty.DataEquals(property, other))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// One clip draws its property as usual. Several clips draw a shared value instead, since a PropertyField
        /// would take its mixed state from the serialized object rather than from the other clips.
        /// </summary>
        static void DrawClipField(SerializedProperty clip, IReadOnlyList<SerializedProperty> sharedWith, string name, string label, FieldOptions options)
        {
            SerializedProperty property = FindClipField(clip, name);
            if (property == null)
                return;

            if (sharedWith == null || sharedWith.Count < 2)
            {
                EvoEditorGUI.Property(property, label, options);
                return;
            }

            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = IsMixed(property, sharedWith, name);
            EditorGUI.BeginChangeCheck();

            switch (property.propertyType)
            {
                case SerializedPropertyType.Enum:
                {
                    int index = EvoEditorGUI.Dropdown(property.enumValueIndex, property.enumDisplayNames, label, options);
                    if (EditorGUI.EndChangeCheck())
                    {
                        for (int i = 0; i < sharedWith.Count; i++)
                            FindClipField(sharedWith[i], name).enumValueIndex = index;
                    }
                    break;
                }
                case SerializedPropertyType.Integer:
                {
                    int value = EvoEditorGUI.IntField(property.intValue, label, options);
                    if (EditorGUI.EndChangeCheck())
                    {
                        float minimum = GetSettingsMinimum(name);
                        if (minimum > int.MinValue)
                            value = Mathf.Max(value, Mathf.CeilToInt(minimum));

                        for (int i = 0; i < sharedWith.Count; i++)
                            FindClipField(sharedWith[i], name).intValue = value;
                    }
                    break;
                }
                case SerializedPropertyType.Float:
                {
                    float value = EvoEditorGUI.FloatField(property.floatValue, label, options);
                    if (EditorGUI.EndChangeCheck())
                    {
                        value = Mathf.Max(SanitizeFinite(value), GetSettingsMinimum(name));
                        for (int i = 0; i < sharedWith.Count; i++)
                            FindClipField(sharedWith[i], name).floatValue = value;
                    }
                    break;
                }
                default:
                    EditorGUI.EndChangeCheck();
                    break;
            }

            EditorGUI.showMixedValue = previousMixedValue;
        }

        // Shared edits skip PropertyField and its MinDrawer, so they apply the settings field's [Min] themselves
        static float GetSettingsMinimum(string name)
        {
            FieldInfo field = typeof(TweenClipSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetCustomAttribute<MinAttribute>()?.min ?? float.MinValue;
        }

        void DrawValuesSection(SerializedProperty module, SerializedProperty settings, ModuleLayout layout)
        {
            if (module == null || layout.FromValues.Count + layout.ToValues.Count + layout.OtherValues.Count == 0)
                return;

            bool showFrom = ShouldShowFrom(module, layout.FromValues);
            int fromCount = CountVisibleFields(module, settings, layout.FromValues, showFrom);
            int toCount = CountVisibleFields(module, settings, layout.ToValues, true);
            int otherCount = CountVisibleFields(module, settings, layout.OtherValues, true);

            if (fromCount + toCount + otherCount == 0)
                return;

            bool showSwap = showFrom && CanSwap(module, layout.FromValues, layout.ToValues);

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
            EvoEditorGUI.BeginContainer();

            if (fromCount > 0)
            {
                EvoEditorGUI.BeginContainer("From", padding: 0);

                if (showSwap)
                    DrawSwapButton(module, layout.FromValues, layout.ToValues);

                SerializedProperty holdBeforeStart = ShowsHoldBeforeStart(module, layout)
                    ? settings.FindPropertyRelative("holdBeforeStart")
                    : null;

                DrawModuleFields(module, settings, layout.FromValues, showFrom, holdBeforeStart != null);
                if (holdBeforeStart != null)
                    EvoEditorGUI.Toggle(holdBeforeStart, "Hold Before Start", FieldOptions.WithoutSpace().WithInvertedColor(!invertColors));

                EvoEditorGUI.EndContainer();
            }

            if (toCount > 0)
            {
                if (fromCount > 0)
                    EvoEditorGUI.AddPropertySpace();

                EvoEditorGUI.BeginContainer("To", padding: 0);
                DrawModuleFields(module, settings, layout.ToValues, true);
                EvoEditorGUI.EndContainer();
            }

            if (otherCount > 0)
            {
                if (fromCount > 0 || toCount > 0)
                    EvoEditorGUI.AddPropertySpace();

                DrawModuleFields(module, settings, layout.OtherValues, true);
            }

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        void DrawModuleFields(SerializedProperty module, SerializedProperty settings, List<FieldLayout> fields, bool showFrom,
            bool spaceAfterLast = false)
        {
            int visibleCount = CountVisibleFields(module, settings, fields, showFrom);
            int visibleIndex = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                FieldLayout field = fields[i];
                if (!IsVisibleField(module, settings, field, showFrom))
                    continue;

                DrawModuleField(module, settings, field, ++visibleIndex < visibleCount || spaceAfterLast);
            }
        }

        static void DrawSwapButton(SerializedProperty module, List<FieldLayout> fromFields, List<FieldLayout> toFields)
        {
            GUILayout.Space(-23);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (EvoEditorGUI.Button("Swap", EvoEditorGUI.GetIcon("Tween_ValueSwap"),
                ButtonOptions.WithTooltip("Swap all compatible From and To values.").WithIconSize(10).WithoutIdleBackground()))
            {
                int toIndex = 0;
                for (int i = 0; i < fromFields.Count; i++)
                {
                    if (fromFields[i].Role != ModuleFieldRole.From)
                        continue;

                    SerializedProperty from = FindProperty(module, fromFields[i]);
                    SerializedProperty to = FindProperty(module, toFields[toIndex++]);
                    (to.boxedValue, from.boxedValue) = (from.boxedValue, to.boxedValue);
                }
            }

            GUILayout.Space(-5);
            GUILayout.EndHorizontal();
            GUILayout.Space(1);
        }

        void DrawModuleSettingsSection(SerializedProperty module, SerializedProperty settings, List<FieldLayout> fields)
        {
            if (module == null || fields.Count == 0 || CountVisibleFields(module, settings, fields, true) == 0)
                return;

            EvoEditorGUI.AddPropertySpace();
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
            EvoEditorGUI.BeginContainer("Settings");

            DrawModuleFields(module, settings, fields, true);

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        static int CountVisibleFields(SerializedProperty module, SerializedProperty settings, List<FieldLayout> fields, bool showFrom)
        {
            int count = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                if (IsVisibleField(module, settings, fields[i], showFrom))
                    count++;
            }
            return count;
        }

        internal static bool ShouldShowFrom(SerializedProperty module, List<FieldLayout> fields)
        {
            SerializedProperty fromCurrent = FindProperty(module, FindRole(fields, ModuleFieldRole.FromCurrent));
            return fromCurrent == null || !fromCurrent.boolValue;
        }

        // Holding needs an explicit From value, so the option hides with From Current like the value itself
        internal static bool ShowsHoldBeforeStart(SerializedProperty module, ModuleLayout layout)
        {
            return FindRole(layout.FromValues, ModuleFieldRole.From) != null && ShouldShowFrom(module, layout.FromValues);
        }

        internal static bool IsVisibleField(SerializedProperty module, SerializedProperty settings, FieldLayout field, bool showFrom)
        {
            if (field.Role == ModuleFieldRole.From && !showFrom)
                return false;

            return MatchesCondition(module, settings, field.ShowIf);
        }

        internal static bool HasVisibleFields(SerializedProperty module, SerializedProperty settings, List<FieldLayout> fields, bool showFrom)
        {
            return CountVisibleFields(module, settings, fields, showFrom) > 0;
        }

        void DrawModuleField(SerializedProperty module, SerializedProperty settings, FieldLayout layout, bool addSpace)
        {
            SerializedProperty property = module.FindPropertyRelative(layout.Name);
            if (property == null)
                return;

            FieldOptions options = FieldOptions.WithSpace(addSpace).WithInvertedColor(!invertColors);
            if (TryApplyFieldRange(module, settings, layout, property, out float minimum, out float maximum))
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    EvoEditorGUI.Slider(property, Mathf.CeilToInt(minimum), Mathf.FloorToInt(maximum), layout.Label,
                        options.WithTooltip(property.tooltip));
                }
                else
                {
                    EvoEditorGUI.Slider(property, minimum, maximum, layout.Label, options.WithTooltip(property.tooltip));
                }
            }
            else if (property.propertyType == SerializedPropertyType.Boolean)
            {
                EvoEditorGUI.Toggle(property, layout.Label, options);
            }
            else
            {
                ApplyFieldLimits(module, layout, property);
                EvoEditorGUI.Property(property, layout.Label, options);
                ApplyFieldLimits(module, layout, property);
            }
        }

        static bool MatchesCondition(SerializedProperty module, SerializedProperty settings, ModuleShowIfAttribute condition)
        {
            if (condition == null)
                return true;

            SerializedProperty reference = FindReferencedProperty(module, settings, condition.ReferenceName);
            if (reference == null || reference.hasMultipleDifferentValues)
                return true;

            object[] expectedValues = condition.ExpectedValues;
            bool matches = expectedValues == null || expectedValues.Length == 0
                ? MatchesConditionValue(reference, true)
                : MatchesAnyConditionValue(reference, expectedValues);

            return condition.Invert ? !matches : matches;
        }

        static bool MatchesAnyConditionValue(SerializedProperty property, object[] expectedValues)
        {
            for (int i = 0; i < expectedValues.Length; i++)
            {
                if (MatchesConditionValue(property, expectedValues[i]))
                    return true;
            }
            return false;
        }

        static bool MatchesConditionValue(SerializedProperty property, object expectedValue)
        {
            try
            {
                return property.propertyType switch
                {
                    SerializedPropertyType.Boolean => property.boolValue == Convert.ToBoolean(expectedValue),
                    SerializedPropertyType.Enum or SerializedPropertyType.Integer => property.intValue == Convert.ToInt32(expectedValue),
                    SerializedPropertyType.Float => Mathf.Approximately(property.floatValue, Convert.ToSingle(expectedValue)),
                    _ => true
                };
            }
            catch
            {
                // Invalid metadata must not make a serialized field inaccessible
                return true;
            }
        }

        internal static bool TryApplyFieldRange(SerializedProperty module, SerializedProperty settings, FieldLayout layout,
            SerializedProperty property, out float minimum, out float maximum)
        {
            minimum = 0f;
            maximum = 0f;

            if (property == null || property.propertyType != SerializedPropertyType.Float
                && property.propertyType != SerializedPropertyType.Integer)
                return false;

            ModuleRangeAttribute range = layout.Range;
            if (range != null)
            {
                if (!TryGetBound(module, settings, range.MinimumReference, range.Minimum, out minimum)
                    || !TryGetBound(module, settings, range.MaximumReference, range.Maximum, out maximum))
                    return false;
            }
            else if (layout.UnityRange != null)
            {
                minimum = layout.UnityRange.min;
                maximum = layout.UnityRange.max;
            }
            else if (!TryGetModuleBounds(module, layout.Role, out IModuleValueBounds bounds) || !bounds.HasMinimum || !bounds.HasMaximum)
            {
                return false;
            }
            else
            {
                minimum = bounds.Minimum;
                maximum = bounds.Maximum;
            }

            minimum = SanitizeFinite(minimum);
            maximum = Mathf.Max(minimum, SanitizeFinite(maximum));

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int integerMinimum = Mathf.CeilToInt(minimum);
                int integerMaximum = Mathf.Max(integerMinimum, Mathf.FloorToInt(maximum));
                property.intValue = Mathf.Clamp(property.intValue, integerMinimum, integerMaximum);
                minimum = integerMinimum;
                maximum = integerMaximum;
            }
            else
            {
                property.floatValue = Mathf.Clamp(SanitizeFinite(property.floatValue), minimum, maximum);
            }

            return true;
        }

        static void ApplyFieldLimits(SerializedProperty module, FieldLayout layout, SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Float
                && property.propertyType != SerializedPropertyType.Integer)
                return;

            bool hasMinimum = false;
            bool hasMaximum = false;
            float minimum = 0f;
            float maximum = 0f;

            if (layout.Minimum != null)
            {
                hasMinimum = true;
                minimum = layout.Minimum.min;
            }

            if (TryGetModuleBounds(module, layout.Role, out IModuleValueBounds bounds))
            {
                if (bounds.HasMinimum)
                {
                    minimum = hasMinimum ? Mathf.Max(minimum, bounds.Minimum) : bounds.Minimum;
                    hasMinimum = true;
                }

                if (bounds.HasMaximum)
                {
                    maximum = bounds.Maximum;
                    hasMaximum = true;
                }
            }

            if (!hasMinimum && !hasMaximum)
                return;

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = property.intValue;
                if (hasMinimum)
                    value = Mathf.Max(value, Mathf.CeilToInt(SanitizeFinite(minimum)));
                if (hasMaximum)
                    value = Mathf.Min(value, Mathf.FloorToInt(SanitizeFinite(maximum)));
                property.intValue = value;
                return;
            }

            float floatValue = SanitizeFinite(property.floatValue);
            if (hasMinimum)
                floatValue = Mathf.Max(floatValue, SanitizeFinite(minimum));
            if (hasMaximum)
                floatValue = Mathf.Min(floatValue, SanitizeFinite(maximum));
            property.floatValue = floatValue;
        }

        static bool TryGetModuleBounds(SerializedProperty module, ModuleFieldRole role, out IModuleValueBounds bounds)
        {
            bounds = null;

            if (module == null || role != ModuleFieldRole.From && role != ModuleFieldRole.To)
                return false;

            try
            {
                bounds = module.managedReferenceValue as IModuleValueBounds;
                return bounds != null;
            }
            catch (InvalidOperationException) { return false; }
        }

        static bool TryGetBound(SerializedProperty module, SerializedProperty settings, string referenceName, float constant, out float value)
        {
            if (string.IsNullOrEmpty(referenceName))
            {
                value = constant;
                return true;
            }

            SerializedProperty reference = FindReferencedProperty(module, settings, referenceName);
            if (reference == null || reference.hasMultipleDifferentValues)
            {
                value = 0f;
                return false;
            }

            switch (reference.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = reference.floatValue;
                    return true;
                case SerializedPropertyType.Integer:
                    value = reference.intValue;
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }

        static SerializedProperty FindReferencedProperty(SerializedProperty module, SerializedProperty settings, string referenceName)
        {
            if (string.IsNullOrEmpty(referenceName))
                return null;

            if (referenceName.StartsWith(ModulePropertyReference.SettingsPrefix, StringComparison.Ordinal))
            {
                return settings?.FindPropertyRelative(
                    referenceName[ModulePropertyReference.SettingsPrefix.Length..]);
            }

            return module?.FindPropertyRelative(referenceName);
        }

        static float SanitizeFinite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        internal static bool DrawTypePopup(Rect rect, SerializedProperty settings, SerializedProperty target = null, Component owner = null)
        {
            SerializedProperty type = settings?.FindPropertyRelative("module");
            Type currentType = (type?.managedReferenceValue as TweenModule)?.GetType();
            int currentIndex = GetModuleTypeIndex(currentType);
            bool missingType = currentIndex < 0;
            int displayedIndex = missingType ? 0 : currentIndex;
            string[] displayedNames = missingType ? MissingModuleTypeNames : ModuleTypeNames;
            Rect popupRect = EditorGUI.PrefixLabel(rect, new GUIContent("Module"));
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            int selectedIndex = EditorGUI.Popup(popupRect, displayedIndex, displayedNames);
            EditorGUI.indentLevel = previousIndent;

            if (missingType)
                selectedIndex--;
            if (selectedIndex == currentIndex || selectedIndex < 0 || selectedIndex >= ModuleTypes.Count)
                return false;

            SetModuleType(type, target, owner, ModuleTypes[selectedIndex].Type);
            return true;
        }

        static void DrawTypeDropdown(SerializedProperty settings, SerializedProperty target, Component owner, bool inverted,
            Action beforeChange, Action afterChange)
        {
            SerializedProperty type = settings.FindPropertyRelative("module");
            Type currentType = (type?.managedReferenceValue as TweenModule)?.GetType();
            int currentIndex = GetModuleTypeIndex(currentType);
            bool missingType = currentIndex < 0;
            int displayedIndex = missingType ? 0 : currentIndex;
            string[] displayedNames = missingType ? MissingModuleTypeNames : ModuleTypeNames;
            int selectedIndex = EvoEditorGUI.Dropdown(displayedIndex, displayedNames, "Module", FieldOptions.WithInvertedColor(inverted));

            Rect typeRowRect = GUILayoutUtility.GetLastRect();
            HandleSettingsContextClick(typeRowRect, settings, beforeChange, afterChange);

            if (missingType)
                selectedIndex--;
            if (selectedIndex != currentIndex && selectedIndex >= 0 && selectedIndex < ModuleTypes.Count)
                SetModuleType(type, target, owner, ModuleTypes[selectedIndex].Type);
        }

        static void SetModuleType(SerializedProperty type, SerializedProperty target, Component owner, Type moduleType)
        {
            if (type == null || Activator.CreateInstance(moduleType) is not TweenModule module)
                return;

            type.managedReferenceValue = module;

            if (target == null)
                return;

            Component currentTarget = target.objectReferenceValue as Component;
            if (currentTarget != null && module.TargetType.IsInstanceOfType(currentTarget))
                return;

            // Redirected owners keep targets implicit, so clips follow whichever GameObject the sequence plays on
            target.objectReferenceValue = owner != null && !TweenPreviewRoot.IsRedirected(owner) ? owner.GetComponent(module.TargetType) : null;
        }

        void ShowAddMenu(ReorderableListAddContext context)
        {
            addContext = context;
            GenericMenu menu = new();

            for (int i = 0; i < ModuleTypes.Count; i++)
            {
                ModuleTypeInfo info = ModuleTypes[i];
                menu.AddItem(new GUIContent(info.MenuPath), false, () => AddClip(info.Type, addOwner));
            }

            if (ModuleTypes.Count == 0)
                menu.AddDisabledItem(new GUIContent("No TweenModule types found"));

            menu.ShowAsContext();
        }

        void AddClip(Type moduleType, Component owner)
        {
            addContext.AddItem(item =>
            {
                item.FindPropertyRelative("target").objectReferenceValue = null;
                item.FindPropertyRelative("flow").enumValueIndex = (int)ClipFlow.Parallel;

                SerializedProperty settings = item.FindPropertyRelative("settings");
                InitializeClipSettings(settings, 0f, 0.3f);

                SerializedProperty target = item.FindPropertyRelative("target");
                SerializedProperty type = settings.FindPropertyRelative("module");
                SetModuleType(type, target, owner, moduleType);
                item.isExpanded = true;
            });
        }

        /// <summary>
        /// Writes the timing and easing every new clip starts from. Callers set the module, target, and flow.
        /// </summary>
        internal static void InitializeClipSettings(SerializedProperty settings, float delay, float duration,
            Ease ease = Ease.OutCubic, AnimationCurve customEase = null)
        {
            settings.FindPropertyRelative("delay").floatValue = Mathf.Max(0f, delay);
            settings.FindPropertyRelative("holdBeforeStart").boolValue = true;
            settings.FindPropertyRelative("duration").floatValue = Mathf.Max(0f, duration);
            settings.FindPropertyRelative("loop").enumValueIndex = (int)LoopMode.None;
            settings.FindPropertyRelative("iterations").intValue = 1;
            settings.FindPropertyRelative("ease").enumValueIndex = (int)ease;
            settings.FindPropertyRelative("customEase").animationCurveValue = customEase ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        internal static ModuleLayout GetModuleLayout(Type moduleType)
        {
            if (moduleType == null)
                return null;
            if (LayoutCache.TryGetValue(moduleType, out ModuleLayout layout))
                return layout;

            layout = BuildModuleLayout(moduleType);
            LayoutCache.Add(moduleType, layout);
            return layout;
        }

        internal static IReadOnlyList<ModuleTypeInfo> GetModuleTypes() => ModuleTypes;

        internal static string[] GetModuleTypeNames() => ModuleTypeNames;

        internal static void CopySettings(SerializedProperty settings)
        {
            if (settings == null)
                return;

            settingsClipboard = CaptureSettings(settings);
        }

        internal static void PasteSettings(SerializedProperty settings) => PasteSettings(settings, settingsClipboard);

        internal static bool CanPasteSettings => settingsClipboard != null;

        internal static void CopyClip(SerializedProperty clip)
        {
            if (clip != null)
                CopyClips(new[] { clip });
        }

        /// <summary>
        /// Copies clips in order. Targets, when given, replace each clip's own target, as for preset clips whose
        /// targets are stored on the sequence.
        /// </summary>
        internal static void CopyClips(IReadOnlyList<SerializedProperty> clips, IReadOnlyList<Component> targets = null)
        {
            clipClipboard.Clear();
            for (int i = 0; i < clips.Count; i++)
            {
                ClipSnapshot snapshot = CaptureClip(clips[i]);
                if (snapshot == null)
                    continue;

                if (targets != null)
                    snapshot.Target = targets[i];
                clipClipboard.Add(snapshot);
            }
        }

        internal static void PasteClip(SerializedProperty clip) => PasteClip(clip, 0);

        internal static void PasteClip(SerializedProperty clip, int index)
        {
            if (clip == null || index < 0 || index >= clipClipboard.Count)
                return;

            PasteClip(clip, clipClipboard[index]);
        }

        internal static int ClipboardCount => clipClipboard.Count;

        // A preset asset can't hold the scene target it pastes, so preset-backed sequences bind it from here
        internal static Component GetClipboardTarget(int index) => index >= 0 && index < clipClipboard.Count ? clipClipboard[index].Target : null;

        static ClipSnapshot CaptureClip(SerializedProperty clip)
        {
            if (clip == null)
                return null;

            return new ClipSnapshot
            {
                Target = clip.FindPropertyRelative("target")?.objectReferenceValue as Component,
                Flow = (ClipFlow)(clip.FindPropertyRelative("flow")?.enumValueIndex ?? 0),
                InlineSettings = CaptureSettings(clip.FindPropertyRelative("settings"))
            };
        }

        static void PasteClip(SerializedProperty clip, ClipSnapshot snapshot)
        {
            if (clip == null || snapshot == null)
                return;

            ClipSnapshot copy = snapshot.Clone();
            clip.FindPropertyRelative("target").objectReferenceValue = copy.Target;
            clip.FindPropertyRelative("flow").enumValueIndex = (int)copy.Flow;
            PasteSettings(clip.FindPropertyRelative("settings"), copy.InlineSettings);
        }

        internal static bool CanPasteClip => clipClipboard.Count > 0;

        static void HandleSettingsContextClick(Rect rect, SerializedProperty settings, Action beforeChange, Action afterChange)
        {
            Event current = Event.current;
            if (!GUI.enabled || current.type != EventType.ContextClick || !rect.Contains(current.mousePosition))
                return;

            SerializedObject owner = settings.serializedObject;
            string propertyPath = settings.propertyPath;
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                owner.UpdateIfRequiredOrScript();
                CopySettings(owner.FindProperty(propertyPath));
            });

            if (CanPasteSettings)
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    beforeChange?.Invoke();
                    owner.UpdateIfRequiredOrScript();
                    Undo.RecordObject(owner.targetObject, "Paste Tween Settings");
                    PasteSettings(owner.FindProperty(propertyPath));
                    owner.ApplyModifiedProperties();
                    EditorUtility.SetDirty(owner.targetObject);
                    afterChange?.Invoke();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.ShowAsContext();
            current.Use();
        }

        internal static string GetDefaultInspectorTitle(SerializedProperty clip)
        {
            SerializedProperty flow = clip?.FindPropertyRelative("flow");
            string flowName = flow != null
                ? ObjectNames.NicifyVariableName(((ClipFlow)flow.enumValueIndex).ToString())
                : ObjectNames.NicifyVariableName(ClipFlow.Parallel.ToString());
            string typeName = GetModuleTitle(GetEffectiveModule(clip));
            return $"{flowName} - {typeName}";
        }

        internal static string GetDefaultSettingsTitle(SerializedProperty settings)
        {
            TweenModule module = settings?.FindPropertyRelative("module")?.managedReferenceValue as TweenModule;
            return GetModuleTitle(module);
        }

        /// <summary>
        /// Finds the values a clip animates between. <paramref name="from"/> is null when the clip starts from the target's
        /// current value. Returns false for modules without a visible To value.
        /// </summary>
        internal static bool TryGetValueRange(SerializedProperty settings, Type moduleType, out SerializedProperty from, out SerializedProperty to)
        {
            from = null;
            to = null;

            SerializedProperty module = settings?.FindPropertyRelative("module");
            ModuleLayout layout = GetModuleLayout(moduleType);
            FieldLayout toField = layout != null ? FindRole(layout.ToValues, ModuleFieldRole.To) : null;
            if (module == null || toField == null || !IsVisibleField(module, settings, toField, true))
                return false;

            to = FindProperty(module, toField);
            FieldLayout fromField = FindRole(layout.FromValues, ModuleFieldRole.From);
            if (fromField != null && IsVisibleField(module, settings, fromField, ShouldShowFrom(module, layout.FromValues)))
                from = FindProperty(module, fromField);

            return to != null;
        }

        internal static string GetModuleTitle(TweenModule module)
        {
            return module != null &&
                Attribute.GetCustomAttribute(module.GetType(), typeof(ModuleMenuAttribute)) is ModuleMenuAttribute menu
                ? GetModulePath(menu, " / ")
                : module != null ? ObjectNames.NicifyVariableName(module.GetType().Name) : "Missing Type";
        }

        static SettingsSnapshot CaptureSettings(SerializedProperty settings)
        {
            if (settings == null)
                return null;

            SerializedProperty type = settings.FindPropertyRelative("module");
            SerializedProperty delay = settings.FindPropertyRelative("delay");
            SerializedProperty holdBeforeStart = settings.FindPropertyRelative("holdBeforeStart");
            SerializedProperty duration = settings.FindPropertyRelative("duration");
            SerializedProperty loop = settings.FindPropertyRelative("loop");
            SerializedProperty iterations = settings.FindPropertyRelative("iterations");
            SerializedProperty ease = settings.FindPropertyRelative("ease");
            SerializedProperty customEase = settings.FindPropertyRelative("customEase");

            return new SettingsSnapshot
            {
                Module = (type?.managedReferenceValue as TweenModule)?.Clone(),
                Delay = delay?.floatValue ?? 0f,
                HoldBeforeStart = holdBeforeStart?.boolValue ?? false,
                Duration = duration?.floatValue ?? 0.3f,
                Loop = (LoopMode)(loop?.enumValueIndex ?? (int)LoopMode.None),
                Iterations = iterations?.intValue ?? 1,
                Ease = (Ease)(ease?.enumValueIndex ?? (int)Ease.OutCubic),
                CustomEase = customEase?.animationCurveValue != null ? new AnimationCurve(customEase.animationCurveValue.keys) : null
            };
        }

        static void PasteSettings(SerializedProperty settings, SettingsSnapshot snapshot)
        {
            if (settings == null || snapshot == null)
                return;

            SettingsSnapshot copy = snapshot.Clone();
            settings.FindPropertyRelative("module").managedReferenceValue = copy.Module;
            settings.FindPropertyRelative("delay").floatValue = copy.Delay;
            settings.FindPropertyRelative("holdBeforeStart").boolValue = copy.HoldBeforeStart;
            settings.FindPropertyRelative("duration").floatValue = copy.Duration;
            settings.FindPropertyRelative("loop").enumValueIndex = (int)copy.Loop;
            settings.FindPropertyRelative("iterations").intValue = copy.Iterations;
            settings.FindPropertyRelative("ease").enumValueIndex = (int)copy.Ease;
            settings.FindPropertyRelative("customEase").animationCurveValue = copy.CustomEase;
        }

        static List<ModuleTypeInfo> BuildModuleTypes()
        {
            List<ModuleTypeInfo> types = new();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<TweenModule>())
            {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                if (Attribute.GetCustomAttribute(type, typeof(ModuleMenuAttribute)) is ModuleMenuAttribute menu)
                    types.Add(new ModuleTypeInfo(type, menu));
            }

            types.Sort((a, b) =>
            {
                int order = a.Menu.Order.CompareTo(b.Menu.Order);
                if (order != 0)
                    return order;

                return string.Compare(a.Menu.Path, b.Menu.Path, StringComparison.Ordinal);
            });

            return types;
        }

        static string[] BuildModuleTypeNames()
        {
            string[] names = new string[ModuleTypes.Count];
            for (int i = 0; i < ModuleTypes.Count; i++)
                names[i] = ModuleTypes[i].DisplayName;
            
            return names;
        }

        static string GetModulePath(ModuleMenuAttribute menu, string separator) => menu.Path.Replace("/", separator);

        static string[] BuildMissingModuleTypeNames()
        {
            string[] names = new string[ModuleTypeNames.Length + 1];
            names[0] = "Missing Type";
            Array.Copy(ModuleTypeNames, 0, names, 1, ModuleTypeNames.Length);
            return names;
        }

        static int GetModuleTypeIndex(Type type)
        {
            for (int i = 0; i < ModuleTypes.Count; i++)
            {
                if (ModuleTypes[i].Type == type)
                    return i;
            }
            return -1;
        }

        static ModuleLayout BuildModuleLayout(Type moduleType)
        {
            ModuleLayout layout = new();
            Stack<Type> hierarchy = new();
            for (Type type = moduleType; type != null && type != typeof(TweenModule); type = type.BaseType)
                hierarchy.Push(type);

            int sequence = 0;
            while (hierarchy.Count > 0)
            {
                FieldInfo[] fields = hierarchy.Pop().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!IsSerializedField(field))
                        continue;

                    ModuleFieldAttribute attribute = field.GetCustomAttribute<ModuleFieldAttribute>(true);
                    ModuleFieldGroup group = attribute?.Group ?? ModuleFieldGroup.Settings;
                    ModuleFieldRole role = attribute?.Role ?? ModuleFieldRole.None;
                    int order = attribute?.Order ?? sequence;
                    string label = string.IsNullOrEmpty(attribute?.Label) ? ObjectNames.NicifyVariableName(field.Name) : attribute.Label;
                    ModuleShowIfAttribute showIf = field.GetCustomAttribute<ModuleShowIfAttribute>(true);
                    ModuleRangeAttribute range = field.GetCustomAttribute<ModuleRangeAttribute>(true);
                    RangeAttribute unityRange = field.GetCustomAttribute<RangeAttribute>(true);
                    MinAttribute minimum = field.GetCustomAttribute<MinAttribute>(true);

                    FieldLayout fieldLayout = new(field.Name, role, order, sequence++, label, showIf, range, unityRange, minimum);
                    if (group == ModuleFieldGroup.Values) { AddValueField(layout, fieldLayout); }
                    else { layout.Settings.Add(fieldLayout); }
                }
            }

            SortFields(layout.FromValues);
            SortFields(layout.ToValues);
            SortFields(layout.OtherValues);
            SortFields(layout.Settings);
            return layout;
        }

        static void AddValueField(ModuleLayout layout, FieldLayout field)
        {
            switch (field.Role)
            {
                case ModuleFieldRole.FromCurrent:
                case ModuleFieldRole.From:
                    layout.FromValues.Add(field);
                    break;
                case ModuleFieldRole.To:
                    layout.ToValues.Add(field);
                    break;
                default:
                    layout.OtherValues.Add(field);
                    break;
            }
        }

        static bool IsSerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized || field.IsDefined(typeof(HideInInspector), true))
                return false;

            return field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true);
        }

        static void SortFields(List<FieldLayout> fields)
        {
            fields.Sort((a, b) =>
            {
                int order = a.Order.CompareTo(b.Order);
                return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
            });
        }

        static FieldLayout FindRole(List<FieldLayout> fields, ModuleFieldRole role)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Role == role)
                    return fields[i];
            }
            return null;
        }

        static SerializedProperty FindProperty(SerializedProperty module, FieldLayout layout)
        {
            return layout != null ? module.FindPropertyRelative(layout.Name) : null;
        }

        static bool CanSwap(SerializedProperty module, List<FieldLayout> fromFields, List<FieldLayout> toFields)
        {
            int fromCount = 0;
            for (int i = 0; i < fromFields.Count; i++)
            {
                if (fromFields[i].Role == ModuleFieldRole.From)
                    fromCount++;
            }

            if (fromCount == 0 || fromCount != toFields.Count)
                return false;

            int toIndex = 0;
            for (int i = 0; i < fromFields.Count; i++)
            {
                if (fromFields[i].Role != ModuleFieldRole.From)
                    continue;

                SerializedProperty from = FindProperty(module, fromFields[i]);
                SerializedProperty to = FindProperty(module, toFields[toIndex++]);
                if (from == null || to == null || from.propertyType != to.propertyType)
                    return false;
            }
            return true;
        }

        static string GetClipTitle(SerializedProperty clip, int index)
        {
            TweenModule module = GetEffectiveModule(clip);
            return module != null ? GetModuleTitle(module) : $"Missing Clip {index + 1}";
        }

        internal static TweenModule GetEffectiveModule(SerializedProperty clip)
        {
            return clip.FindPropertyRelative("settings")?.FindPropertyRelative("module")?.managedReferenceValue as TweenModule;
        }

        static Texture2D GetFlowIcon(SerializedProperty clip)
        {
            SerializedProperty flow = clip.FindPropertyRelative("flow");
            ClipFlow value = flow != null ? (ClipFlow)flow.enumValueIndex : ClipFlow.Parallel;
            return value switch
            {
                ClipFlow.Parallel => EvoEditorGUI.GetIcon("Tween_FlowParallel"),
                ClipFlow.Sequential => EvoEditorGUI.GetIcon("Tween_FlowSequential"),
                _ => null
            };
        }

        static bool CanPasteClipItem(SerializedProperty _) => CanPasteClip;
    }
}