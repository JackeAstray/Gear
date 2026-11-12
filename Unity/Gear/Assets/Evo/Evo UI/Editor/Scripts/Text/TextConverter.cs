using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_6000_3_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Evo.UI.Editor
{
    /// <summary>
    /// Converts text components and input fields to another text provider without losing references.
    /// GameObjects are kept, so every reference is repointed to the new component. Input fields move with their text,
    /// as each provider keeps the field on a different object of the same hierarchy.
    /// Text that cannot follow, such as text used by provider-only effects or provider-typed fields, is skipped and reported.
    /// </summary>
    public static class TextConverter
    {
        /// <summary>
        /// Selects what a conversion run touches.
        /// </summary>
        public class Options
        {
            [Tooltip("Convert text in every loaded scene and the open prefab stage.")]
            public bool openScenes = true;
            [Tooltip("Convert prefab assets inside the prefab folder.")]
            public bool prefabs = true;
            public string prefabFolder = "Assets";
            [Tooltip("Also convert Evo UI's own prefabs, which are templates that package updates replace.")]
            public bool includePackagePrefabs;
            [Tooltip("Convert every text and input field, not only those referenced by Evo UI fields.")]
            public bool allTexts;
        }

        /// <summary>
        /// Outcome of a conversion run.
        /// </summary>
        public class Report
        {
            public int converted;
            public int inputs;
            public int fonts;
            public readonly List<(Object context, string message)> skipped = new();
        }

        const Styler.TextStyleFlags AllStyleFlags = Styler.TextStyleFlags.Size | Styler.TextStyleFlags.Alignment |
            Styler.TextStyleFlags.WrappingAndOverflow | Styler.TextStyleFlags.Spacing |
            Styler.TextStyleFlags.Margin | Styler.TextStyleFlags.FontStyle;

        sealed class Context
        {
            public TextBackend target;
            public bool allTexts;
            public bool undo;
            public Report report;
            public Dictionary<Object, Object> fontPairs;
            public HashSet<string> convertedPrefabs = new();
            public readonly HashSet<Object> missingFonts = new();
            public readonly Dictionary<string, int> instanceTexts = new();
        }

        /// <summary>
        /// A serialized reference to a component on a GameObject with text or an input field.
        /// </summary>
        sealed class Reference
        {
            public Object owner;
            public ObjectId ownerId;
            public string path;
            public string fieldType;
            public Component target;
            public ObjectId targetId;
            public bool marked;
        }

        /// <summary>
        /// A reference to text or an input field inside a nested prefab, repaired after that prefab is converted.
        /// Live references are kept for loaded scenes; prefab contents are located by hierarchy path.
        /// An input field is located through its text, as the converted field may sit on another object.
        /// </summary>
        sealed class PendingReference
        {
            public Object owner;
            public GameObject target;
            public string ownerPath;
            public string ownerType;
            public int ownerIndex;
            public string targetPath;
            public string propertyPath;
            public bool input;
        }

        #region Public API
        /// <summary>
        /// Converts text in open scenes and prefab assets to the target backend.
        /// </summary>
        public static Report Convert(TextBackend target, Options options)
        {
            var context = CreateContext(target, options.allTexts);
            var prefabPaths = options.prefabs ? FindPrefabs(options.prefabFolder, options.includePackagePrefabs) : new List<string>();
            var scenePending = new List<PendingReference>();
            var prefabPending = new Dictionary<string, List<PendingReference>>();
            context.convertedPrefabs = new HashSet<string>(prefabPaths, StringComparer.Ordinal);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Convert Text to {target.DisplayName}");

            try
            {
                // Record references to nested text before its prefab changes underneath them.
                // Open scenes are always repaired, even when their own text is not converted.
                foreach (Scene scene in GetOpenScenes())
                    RecordPending(scene.GetRootGameObjects(), null, context, scenePending);

                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    if (!HasNestedPrefabs(prefabPaths[i], context.convertedPrefabs))
                        continue;

                    EditorUtility.DisplayProgressBar("Converting Text", $"Scanning {prefabPaths[i]}", (float)i / prefabPaths.Count * 0.5f);
                    GameObject root = PrefabUtility.LoadPrefabContents(prefabPaths[i]);
                    var pending = new List<PendingReference>();
                    // Loaded prefab contents are not garbage collected, so unload them even on failure
                    try { RecordPending(new[] { root }, root.transform, context, pending); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }

                    if (pending.Count > 0)
                        prefabPending[prefabPaths[i]] = pending;
                }

                // Inner prefabs convert first, so outer prefabs can repair their references right away
                context.undo = false;
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    string path = prefabPaths[i];
                    EditorUtility.DisplayProgressBar("Converting Text", $"Converting {path}", 0.5f + (float)i / prefabPaths.Count * 0.5f);

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        bool changed = prefabPending.TryGetValue(path, out var pending) && ApplyPending(pending, root.transform, context);
                        changed |= ConvertObjects(new[] { root }, context);
                        if (changed)
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }

                context.undo = true;
                ApplyPending(scenePending, null, context);

                if (options.openScenes)
                {
                    foreach (Scene scene in GetOpenScenes())
                    {
                        if (ConvertObjects(scene.GetRootGameObjects(), context))
                            EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            ReportInstanceTexts(context);
            return context.report;
        }

        /// <summary>
        /// Converts text under a new, unsaved hierarchy, such as an object created from a prefab template.
        /// </summary>
        public static Report ConvertHierarchy(GameObject root, TextBackend target, bool allTexts = false)
        {
            var context = CreateContext(target, allTexts);
            ConvertObjects(new[] { root }, context);
            return context.report;
        }

        /// <summary>
        /// Logs skipped text to the Console with its object as context.
        /// </summary>
        public static void Log(Report report, TextBackend target)
        {
            foreach (var (context, message) in report.skipped)
                Debug.LogWarning($"[Evo UI] {message}", context);

            Debug.Log($"[Evo UI] Converted {report.converted} text component(s), {report.inputs} input field(s) and {report.fonts} font reference(s) to {target.DisplayName}." +
                (report.skipped.Count > 0 ? $" {report.skipped.Count} item(s) were skipped, see the warnings above." : ""));
        }
        #endregion

        #region Conversion
        static Context CreateContext(TextBackend target, bool allTexts)
        {
            return new Context
            {
                target = target,
                allTexts = allTexts,
                report = new Report(),
                fontPairs = BuildFontPairs(target)
            };
        }

        static bool ConvertObjects(IReadOnlyList<GameObject> roots, Context context)
        {
            var texts = new List<Graphic>();
            var inputs = new List<MonoBehaviour>();
            var nested = new List<Component>();
            var convertibleObjects = new HashSet<GameObject>();
            var references = new List<Reference>();
            var components = Gather(roots);

            foreach (Component component in components)
            {
                if (component is Graphic graphic && IsSourceText(graphic, context.target))
                {
                    if (IsOwned(graphic)) { texts.Add(graphic); }
                    else { nested.Add(graphic); }
                }
                else if (component is MonoBehaviour behaviour && IsSourceInput(behaviour, context.target))
                {
                    if (IsOwned(behaviour)) { inputs.Add(behaviour); }
                    else { nested.Add(behaviour); }
                }
                else
                    continue;

                convertibleObjects.Add(component.gameObject);
            }

            bool changed = ScanReferences(components, convertibleObjects, context, references);

            foreach (Component component in nested)
            {
                if (context.allTexts || IsMarked(component, references))
                    CountInstanceText(component, context);
            }

            var replacements = new Dictionary<ObjectId, Component>();
            var parts = new List<Component>();

            // Fields first, as they take their text and placeholder with them
            foreach (MonoBehaviour input in inputs)
            {
                if (!context.allTexts && !IsMarked(input, references))
                    continue;

                string blocker = FindInputBlocker(input, context.target, references, parts, out Graphic text, out Graphic placeholder, out GameObject host);
                if (blocker != null)
                {
                    context.report.skipped.Add((input, $"Skipped input field '{input.name}': {blocker}"));
                    continue;
                }

                SwapInput(input, text, placeholder, host, parts, context, replacements);
                changed = true;
            }

            foreach (Graphic text in texts)
            {
                // A destroyed text was replaced with its input field
                if (text == null || (!context.allTexts && !IsMarked(text, references)))
                    continue;

                string blocker = FindBlocker(text, context.target, references, null);
                if (blocker != null)
                {
                    context.report.skipped.Add((text, $"Skipped '{text.name}': {blocker}"));
                    continue;
                }

                Swap(text, context, replacements);
                changed = true;
            }

            foreach (Reference reference in references)
            {
                if (!replacements.TryGetValue(reference.targetId, out Component result))
                    continue;

                // The owner may be a restored dependent, which still points at the old text
                Object owner = replacements.TryGetValue(reference.ownerId, out Component restored) ? restored : reference.owner;
                Assign(owner, reference.path, result, context.undo);
            }

            return changed;
        }

        static Graphic Swap(Graphic source, Context context, Dictionary<ObjectId, Component> replacements)
        {
            TextBackend backend = TextProvider.Find(source);
            TextBackend target = context.target;
            GameObject gameObject = source.gameObject;

            string text = backend.GetText(source);
            Object font = MapFont(backend.GetFont(source), context);
            var style = new Styler.TextStyleItem { flags = AllStyleFlags };
            backend.CaptureTextStyle(source, style);

            Color color = source.color;
            bool raycastTarget = source.raycastTarget;
            bool enabled = source.enabled;
            bool maskable = source is not MaskableGraphic maskableSource || maskableSource.maskable;

            // Siblings that need any Graphic would block removal, so they are restored after the swap
            var dependents = new List<(Component component, Type type, ObjectId id, string json)>();
            foreach (Component sibling in gameObject.GetComponents<Component>())
            {
                if (sibling != null && sibling != source && GetRequirement(sibling.GetType(), source.GetType(), target.ComponentType) == Requirement.Any)
                    dependents.Add((sibling, sibling.GetType(), GetId(sibling), EditorJsonUtility.ToJson(sibling)));
            }

            foreach (var (component, _, _, _) in dependents)
                Destroy(component, context.undo);

            ObjectId sourceId = GetId(source);
            backend.Cleanup(source);
            Destroy(source, context.undo);

            var result = (Graphic)(context.undo ? Undo.AddComponent(gameObject, target.ComponentType) : gameObject.AddComponent(target.ComponentType));
            replacements[sourceId] = result;

            foreach (var (_, type, id, json) in dependents)
            {
                Component restored = context.undo ? Undo.AddComponent(gameObject, type) : gameObject.AddComponent(type);
                EditorJsonUtility.FromJsonOverwrite(json, restored);
                replacements[id] = restored;
            }

            target.SetText(result, text);
            target.SetFont(result, font != null ? font : target.DefaultFont);
            target.ApplyTextStyle(result, style);
            result.color = color;
            result.raycastTarget = raycastTarget;
            result.enabled = enabled;

            if (result is MaskableGraphic maskableResult)
                maskableResult.maskable = maskable;

            EditorUtility.SetDirty(result);
            context.report.converted++;
            return result;
        }

        /// <summary>
        /// Replaces an input field and its texts. The field is removed first, as it holds its texts,
        /// then added where the target provider keeps it and configured from the captured settings.
        /// </summary>
        static void SwapInput(MonoBehaviour input, Graphic text, Graphic placeholder, GameObject host, List<Component> parts,
            Context context, Dictionary<ObjectId, Component> replacements)
        {
            TextBackend target = context.target;
            var settings = new TextInputSettings();
            TextProvider.FindInput(input).CaptureInput(input, settings);
            ObjectId inputId = GetId(input);

            foreach (Component part in parts)
                Destroy(part, context.undo);

            Graphic resultText = Swap(text, context, replacements);
            Graphic resultPlaceholder = placeholder != null ? Swap(placeholder, context, replacements) : null;

            // The source field may have disabled its placeholder, while the target shows and hides it on its own
            if (resultPlaceholder != null)
                resultPlaceholder.enabled = true;

            settings.placeholder = resultPlaceholder;
            var result = (MonoBehaviour)(context.undo ? Undo.AddComponent(host, target.InputType) : host.AddComponent(target.InputType));
            target.ApplyInput(result, resultText, settings);
            EditorUtility.SetDirty(result);

            replacements[inputId] = result;
            context.report.inputs++;
        }

        static void Destroy(Component component, bool undo)
        {
            if (undo) { Undo.DestroyObjectImmediate(component); }
            else { Object.DestroyImmediate(component); }
        }

        static void Assign(Object owner, string path, Object value, bool undo)
        {
            if (owner == null)
                return;

#if EVO_LOCALIZATION && EVO_UNITEXT
            if (RedirectLocalization(owner, path, value, undo))
                return;
#endif

            using var serializedObject = new SerializedObject(owner);
            SerializedProperty property = serializedObject.FindProperty(path);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return;

            property.objectReferenceValue = value;
            Apply(serializedObject, undo);
        }

        static void Apply(SerializedObject serializedObject, bool undo)
        {
            if (undo) { serializedObject.ApplyModifiedProperties(); }
            else { serializedObject.ApplyModifiedPropertiesWithoutUndo(); }
        }
        #endregion

        #region References
        static bool ScanReferences(List<Component> components, HashSet<GameObject> convertibleObjects, Context context, List<Reference> references)
        {
            bool changed = false;

            foreach (Component component in components)
            {
                if (component == null || component is Transform)
                    continue;

                using var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool remapped = false;

                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    Object value = property.objectReferenceValue;
                    if (value == null)
                        continue;

                    if (value is Component target && convertibleObjects.Contains(target.gameObject))
                    {
                        references.Add(new Reference
                        {
                            owner = component,
                            ownerId = GetId(component),
                            path = property.propertyPath,
                            fieldType = property.type,
                            target = target,
                            targetId = GetId(target),
                            marked = target is Graphic
                                ? HasAttribute<TextComponentAttribute>(component, property.propertyPath)
                                : HasAttribute<TextInputAttribute>(component, property.propertyPath)
                        });
                    }
                    else if (context.fontPairs.TryGetValue(value, out Object font) && HasAttribute<TextFontAttribute>(component, property.propertyPath))
                    {
                        property.objectReferenceValue = font;
                        context.report.fonts++;
                        remapped = true;
                    }
                }

                if (remapped)
                {
                    Apply(serializedObject, context.undo);
                    changed = true;
                }
            }

            return changed;
        }

        static void RecordPending(IReadOnlyList<GameObject> roots, Transform root, Context context, List<PendingReference> pending)
        {
            foreach (Component component in Gather(roots))
            {
                if (component == null || component is Transform)
                    continue;

                using var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue is not Component target)
                        continue;

                    bool input = target is MonoBehaviour behaviour && IsSourceInput(behaviour, context.target);
                    if ((!input && !(target is Graphic graphic && IsSourceText(graphic, context.target))) || IsOwned(target) || IsInherited(component, target))
                        continue;

                    // A field is found again through its text, which stays on the same object
                    Graphic text = input ? TextProvider.FindInput(target).GetTextComponent((MonoBehaviour)target) : (Graphic)target;
                    if (text == null)
                        continue;

                    var reference = new PendingReference { propertyPath = property.propertyPath, input = input };
                    if (root == null)
                    {
                        reference.owner = component;
                        reference.target = text.gameObject;
                    }
                    else
                    {
                        reference.ownerPath = GetPath(component.transform, root);
                        reference.ownerType = component.GetType().AssemblyQualifiedName;
                        reference.ownerIndex = Array.IndexOf(component.GetComponents(component.GetType()), component);
                        reference.targetPath = GetPath(text.transform, root);
                    }

                    pending.Add(reference);
                }
            }
        }

        static bool ApplyPending(List<PendingReference> pending, Transform root, Context context)
        {
            bool changed = false;

            foreach (PendingReference reference in pending)
            {
                Object owner = reference.owner;
                GameObject target = reference.target;

                if (root != null)
                {
                    Transform ownerTransform = FindPath(root, reference.ownerPath);
                    Transform targetTransform = FindPath(root, reference.targetPath);
                    Type ownerType = Type.GetType(reference.ownerType);
                    Component[] owners = ownerTransform != null && ownerType != null ? ownerTransform.GetComponents(ownerType) : null;

                    owner = owners != null && reference.ownerIndex >= 0 && reference.ownerIndex < owners.Length ? owners[reference.ownerIndex] : null;
                    target = targetTransform != null ? targetTransform.gameObject : null;
                }

                if (owner == null || target == null || !target.TryGetComponent(out Graphic text) || !context.target.IsText(text))
                    continue;

                // The converted field sits on the text or one of its parents
                Component value = reference.input ? text.GetComponentInParent(context.target.InputType, true) : text;
                if (value == null)
                    continue;

                Assign(owner, reference.propertyPath, value, context.undo);
                changed = true;
            }

            return changed;
        }

        static bool IsMarked(Component target, List<Reference> references)
        {
            foreach (Reference reference in references)
            {
                if (reference.marked && ReferenceEquals(reference.target, target))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns why the text cannot be converted, or null when it can.
        /// References and requirements of the ignored components do not count, as they are removed with an input field.
        /// </summary>
        static string FindBlocker(Graphic text, TextBackend target, List<Reference> references, List<Component> ignored)
        {
            foreach (Reference reference in references)
            {
                if (reference.owner == null || !ReferenceEquals(reference.target, text) || Accepts(reference.fieldType, target.ComponentType) || IsIgnored(reference.owner, ignored))
                    continue;
#if EVO_LOCALIZATION && EVO_UNITEXT
                if (IsLocalizationText(reference.owner, reference.path))
                    continue;
#endif

                return $"{reference.owner.GetType().Name} ('{reference.owner.name}') keeps it in a field that only accepts {GetTypeName(reference.fieldType)}.";
            }

            foreach (Component sibling in text.GetComponents<Component>())
            {
                if (sibling != null && sibling != text && !IsIgnored(sibling, ignored)
                    && GetRequirement(sibling.GetType(), text.GetType(), target.ComponentType) == Requirement.Source)
                    return $"{sibling.GetType().Name} on the same object requires {TextProvider.Find(text).DisplayName}.";
            }

            return null;
        }

        /// <summary>
        /// Returns why the input field cannot be converted, or null when it can.
        /// Fills the field's components and the texts that are converted with it.
        /// </summary>
        static string FindInputBlocker(MonoBehaviour input, TextBackend target, List<Reference> references, List<Component> parts,
            out Graphic text, out Graphic placeholder, out GameObject host)
        {
            TextBackend source = TextProvider.FindInput(input);
            text = source.GetTextComponent(input);
            placeholder = source.GetPlaceholder(input);
            host = null;
            parts.Clear();
            source.CollectInputComponents(input, parts);

            if (text == null || !IsSourceText(text, target) || !IsOwned(text))
                return "its text cannot be converted with it.";
            if (placeholder != null && (!IsSourceText(placeholder, target) || !IsOwned(placeholder)))
                return "its placeholder cannot be converted with it.";

            host = target.GetInputHost(text);
            if (host == null)
                return $"no object around its text can hold a {target.DisplayName} input field.";

            foreach (Reference reference in references)
            {
                if (reference.owner != null && ReferenceEquals(reference.target, input) && !Accepts(reference.fieldType, target.InputType) && !IsIgnored(reference.owner, parts))
                    return $"{reference.owner.GetType().Name} ('{reference.owner.name}') keeps it in a field that only accepts {GetTypeName(reference.fieldType)}.";
            }

            foreach (Component sibling in input.GetComponents<Component>())
            {
                if (sibling != null && !IsIgnored(sibling, parts) && GetRequirement(sibling.GetType(), input.GetType(), target.InputType) == Requirement.Source)
                    return $"{sibling.GetType().Name} on the same object requires {source.DisplayName}.";
            }

            return FindBlocker(text, target, references, parts) ?? (placeholder != null ? FindBlocker(placeholder, target, references, parts) : null);
        }

        static bool IsIgnored(Object owner, List<Component> ignored) => ignored != null && owner is Component component && ignored.Contains(component);

        enum Requirement { None, Any, Source }

        static Requirement GetRequirement(Type type, Type sourceType, Type targetType)
        {
            Requirement requirement = Requirement.None;

            foreach (RequireComponent attribute in type.GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>())
            {
                foreach (Type required in new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
                {
                    if (required == null || !required.IsAssignableFrom(sourceType))
                        continue;
                    if (!required.IsAssignableFrom(targetType))
                        return Requirement.Source;

                    requirement = Requirement.Any;
                }
            }

            return requirement;
        }

        // Serialized types read like PPtr<$TMP_Text> for scripts and PPtr<Component> for built-in types
        static bool Accepts(string fieldType, Type componentType)
        {
            string name = GetTypeName(fieldType);
            for (Type type = componentType; type != null; type = type.BaseType)
            {
                if (type.Name == name)
                    return true;
            }
            return false;
        }

        static string GetTypeName(string fieldType)
        {
            int start = fieldType.IndexOf('<');
            int end = fieldType.LastIndexOf('>');
            return start >= 0 && end > start ? fieldType[(start + 1)..end].TrimStart('$') : fieldType;
        }

        static bool HasAttribute<T>(Object owner, string propertyPath) where T : Attribute
        {
            Type type = owner.GetType();
            System.Reflection.FieldInfo field = null;
            string[] parts = propertyPath.Split('.');

            for (int i = 0; i < parts.Length && type != null; i++)
            {
                // Array elements read as "name.Array.data[i]"
                if (parts[i] == "Array" && i + 1 < parts.Length && parts[i + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    type = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments()[0] : null;
                    i++;
                    continue;
                }

                field = FindField(type, parts[i]);
                type = field?.FieldType;
            }

            return field != null && field.IsDefined(typeof(T), true);
        }

        static System.Reflection.FieldInfo FindField(Type type, string name)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;

            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, flags);
                if (field != null)
                    return field;
            }

            return null;
        }

#if EVO_LOCALIZATION && EVO_UNITEXT
        // LocalizedObject keeps one text field per provider, so its reference moves to the matching field
        static bool IsLocalizationText(Object owner, string path) => owner is Localization.LocalizedObject &&
            (path == nameof(Localization.LocalizedObject.textObj) || path == nameof(Localization.LocalizedObject.uniTextObj));

        static bool RedirectLocalization(Object owner, string path, Object value, bool undo)
        {
            if (!IsLocalizationText(owner, path))
                return false;

            using var serializedObject = new SerializedObject(owner);
            SerializedProperty type = serializedObject.FindProperty(nameof(Localization.LocalizedObject.objectType));
            SerializedProperty tmpText = serializedObject.FindProperty(nameof(Localization.LocalizedObject.textObj));
            SerializedProperty uniText = serializedObject.FindProperty(nameof(Localization.LocalizedObject.uniTextObj));
            bool isUniText = value is LightSide.UniTextBase;
            var current = (Localization.LocalizedObject.ObjectType)type.intValue;

            tmpText.objectReferenceValue = isUniText ? null : value;
            uniText.objectReferenceValue = isUniText ? value : null;

            if (current == Localization.LocalizedObject.ObjectType.TMPText || current == Localization.LocalizedObject.ObjectType.UniText)
                type.intValue = (int)(isUniText ? Localization.LocalizedObject.ObjectType.UniText : Localization.LocalizedObject.ObjectType.TMPText);

            Apply(serializedObject, undo);
            return true;
        }
#endif
        #endregion

        #region Fonts
        /// <summary>
        /// Pairs fonts from every Styler font item that defines a font for the target backend.
        /// </summary>
        static Dictionary<Object, Object> BuildFontPairs(TextBackend target)
        {
            var pairs = new Dictionary<Object, Object>();

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(StylerPreset)))
            {
                var preset = AssetDatabase.LoadAssetAtPath<StylerPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset == null)
                    continue;

                foreach (Styler.FontItem item in preset.fontItems)
                {
                    Object targetFont = item?.GetFont(target);
                    if (targetFont == null)
                        continue;

                    foreach (TextBackend backend in TextProvider.Backends)
                    {
                        Object sourceFont = backend != target ? item.GetFont(backend) : null;
                        if (sourceFont != null)
                            pairs.TryAdd(sourceFont, targetFont);
                    }
                }
            }

            return pairs;
        }

        static Object MapFont(Object font, Context context)
        {
            if (font == null || context.target.IsFont(font))
                return font;
            if (context.fontPairs.TryGetValue(font, out Object pair))
                return pair;

            if (context.missingFonts.Add(font))
            {
                context.report.skipped.Add((font, $"No {context.target.DisplayName} font is paired with '{font.name}', so its text uses the default font. " +
                    "Add one to the Styler font item that uses this font, then convert again or assign it manually."));
            }

            return null;
        }
        #endregion

        #region Scope
        static List<string> FindPrefabs(string folder, bool includePackage)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                folder = "Assets";

            // Evo UI's prefabs are templates; the folder is resolved from Styler.cs so a moved package is still found
            string packageRoot = GetPackageRoot();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            string stagePath = stage != null ? stage.assetPath : null;
            var paths = new List<string>();

            // The prefab open in Prefab Mode converts with the open scenes instead
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path == stagePath)
                    continue;
                if (!includePackage && packageRoot != null && path.StartsWith(packageRoot, StringComparison.Ordinal))
                    continue;

                paths.Add(path);
            }

            var set = new HashSet<string>(paths, StringComparer.Ordinal);
            var depths = new Dictionary<string, int>(StringComparer.Ordinal);
            paths.Sort((a, b) => GetDepth(a, set, depths).CompareTo(GetDepth(b, set, depths)));
            return paths;
        }

        static int GetDepth(string path, HashSet<string> set, Dictionary<string, int> depths)
        {
            if (depths.TryGetValue(path, out int depth))
                return depth;

            depths[path] = 0;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (dependency != path && set.Contains(dependency))
                    depth = Math.Max(depth, GetDepth(dependency, set, depths) + 1);
            }

            depths[path] = depth;
            return depth;
        }

        static bool HasNestedPrefabs(string path, HashSet<string> set)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (dependency != path && set.Contains(dependency))
                    return true;
            }
            return false;
        }

        static string GetPackageRoot()
        {
            string[] guids = AssetDatabase.FindAssets("Styler t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != "Styler.cs")
                    continue;

                int scripts = path.LastIndexOf("/Scripts/", StringComparison.Ordinal);
                if (scripts > 0)
                    return path[..(scripts + 1)];
            }
            return null;
        }

        static IEnumerable<Scene> GetOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                yield return stage.scene;
        }

        static void CountInstanceText(Component component, Context context)
        {
            Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (!string.IsNullOrEmpty(path) && !context.convertedPrefabs.Contains(path))
                context.instanceTexts[path] = context.instanceTexts.TryGetValue(path, out int count) ? count + 1 : 1;
        }

        static void ReportInstanceTexts(Context context)
        {
            foreach (var (path, count) in context.instanceTexts)
            {
                context.report.skipped.Add((AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    $"{count} text component(s) or input field(s) come from '{path}', which was not converted. Convert that prefab to update its instances."));
            }
        }
        #endregion

        #region Helpers
        static List<Component> Gather(IReadOnlyList<GameObject> roots)
        {
            var components = new List<Component>();
            var buffer = new List<Component>();
            foreach (GameObject root in roots)
            {
                if (root == null)
                    continue;

                root.GetComponentsInChildren(true, buffer);
                components.AddRange(buffer);
            }
            return components;
        }

        static bool IsSourceText(Graphic graphic, TextBackend target)
        {
            TextBackend backend = TextProvider.Find(graphic);
            return backend != null && backend != target;
        }

        // A field can only follow when the target provider has input fields of its own
        static bool IsSourceInput(MonoBehaviour input, TextBackend target)
        {
            TextBackend backend = TextProvider.FindInput(input);
            return backend != null && backend != target && target.InputType != null;
        }

        // Identifies components that are destroyed and replaced during a swap
        static ObjectId GetId(Object obj)
        {
#if UNITY_6000_3_OR_NEWER
            return obj.GetEntityId();
#else
            return obj.GetInstanceID();
#endif
        }

        // Components from a nested prefab belong to that prefab and are converted there
        static bool IsOwned(Component component) => PrefabUtility.GetCorrespondingObjectFromSource(component) == null;

        // References inside the same nested prefab follow that prefab's own conversion
        static bool IsInherited(Component owner, Component target)
        {
            GameObject ownerRoot = PrefabUtility.GetNearestPrefabInstanceRoot(owner);
            return ownerRoot != null && ownerRoot == PrefabUtility.GetNearestPrefabInstanceRoot(target);
        }

        static string GetPath(Transform transform, Transform root)
        {
            var builder = new StringBuilder();
            for (Transform current = transform; current != null && current != root; current = current.parent)
                builder.Insert(0, current.GetSiblingIndex()).Insert(0, '/');

            return builder.ToString();
        }

        static Transform FindPath(Transform root, string path)
        {
            Transform current = root;
            foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                int index = int.Parse(part);
                if (current == null || index >= current.childCount)
                    return null;

                current = current.GetChild(index);
            }
            return current;
        }
        #endregion
    }
}