using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation Container"), HelpURL(Constants.HelpUrl + "navigation/ui-navigation")]
    public class UINavigationContainer : MonoBehaviour
    {
        // Elements
        [Tooltip("Collect child selectables, including inactive controls. Otherwise, use the manual list below.")]
        public bool autoFetchOnEnable = true;
        [Tooltip("Controls managed by this container. Manual entries must belong to this transform or its children.")]
        public List<Selectable> interactiveElements = new();

        // Navigation
        [Tooltip("Keep directional navigation inside this container. Restrictions from active parents also apply.")]
        public bool restrictNavigation = true;
        [Tooltip("Handle Tab and Shift+Tab while this container owns the current selection.")]
        public bool tabNavigation = true;
        public TabOrder tabOrder = TabOrder.Hierarchy;
        public bool wrapAround = true;
        [Tooltip("Controls omitted from Tab traversal. Directional navigation is unaffected.")]
        public List<Selectable> excludedFromTabOrder = new();

        // Selection
        public EnableSelection selectOnEnable = EnableSelection.Default;
        [Tooltip("Recover missing, disabled, non-interactable, or non-navigable selection while this container owns focus.")]
        public bool ensureSelection = false;
        public RecoveryMode recoveryMode = RecoveryMode.RestoreLast;
        [Tooltip("Return to the fallback or the previously selected control when this container loses focus on disable.")]
        public bool restoreSelectionOnDisable = true;
        public Selectable defaultSelection;
        public Selectable fallbackSelection;

        public enum TabOrder { Hierarchy, Position }
        public enum EnableSelection { None, Default, First, RestoreLast }
        public enum RecoveryMode { RestoreLast, Default, First, Nearest }

        // Cache
        readonly List<ElementState> elementStates = new();
        readonly List<Selectable> tabElements = new();
        readonly HashSet<Selectable> members = new();
        Selectable lastSelection;
        Selectable previousSelection;
        Vector3 lastPosition;
        bool hasLastPosition;
        bool fetchPending;
        bool selectionPending;
        bool membershipReady;
        bool cachedAutoFetch;
        bool cachedRestriction;
        TabOrder cachedTabOrder;

        // Shared ownership prevents nested containers from saving each other's temporary links
        static readonly List<UINavigationContainer> containers = new();
        static readonly Dictionary<Selectable, NavigationState> originalNavigations = new();
        static UINavigationContainer focusOwner;
        static EventSystem selectionSystem;
        static Selectable returnSelection;
        static EventSystem returnSystem;
        static GameObject returnOrigin;
        static Transform returnRoot;
        static bool returnPending;
        static bool navigationDirty;
        static bool rebuilding;
        static bool canvasRegistered;
        static int selectableCount = -1;
        static int inputFrame = -1;
        static int updateFrame = -1;

        struct ElementState
        {
            public Selectable selectable;
            public Transform parent;
            public Matrix4x4 matrix;
            public Rect rect;
            public Navigation navigation;
            public bool usable;
        }

        struct NavigationState
        {
            public Navigation original;
            public Navigation applied;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            RestoreLinks();
            containers.Clear();
            focusOwner = null;
            selectionSystem = null;
            returnSelection = null;
            returnSystem = null;
            returnOrigin = null;
            returnRoot = null;
            returnPending = false;
            navigationDirty = false;
            rebuilding = false;
            selectableCount = -1;
            inputFrame = -1;
            updateFrame = -1;
            Canvas.willRenderCanvases -= OnCanvasRender;
            canvasRegistered = false;
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            // Not from ResetState: in the editor, uGUI 2.7 replaces its registry after that runs.
            // Also covers an editor script reload during play.
            RegisterCanvas();

            EventSystem system = EventSystem.current;
            GameObject current = system != null ? system.currentSelectedGameObject : null;
            previousSelection = current != null ? current.GetComponent<Selectable>() : null;
            selectionPending = true;

            if (!containers.Contains(this))
                containers.Add(this);

            RefreshNavigation();
        }

        void OnDisable()
        {
            if (!containers.Contains(this))
                return;

            EventSystem system = EventSystem.current;
            GameObject current = system != null ? system.currentSelectedGameObject : null;
            Selectable selected = current != null ? current.GetComponent<Selectable>() : null;
            bool ownedFocus = current == null ? focusOwner == this : Contains(selected) && !HasActiveChildOwner(selected);

            containers.Remove(this);
            selectionPending = false;
            navigationDirty = true;
            RebuildNavigation();

            // Disabled owners are ignored anyway; do not keep them or a stale EventSystem alive statically
            if (containers.Count == 0)
                selectionSystem = null;

            if (!ownedFocus)
            {
                if (focusOwner == this)
                    focusOwner = null;

                return;
            }

            focusOwner = current != null ? FindOwner(current.GetComponent<Selectable>()) : null;
            if (system == null || !restoreSelectionOnDisable)
                return;

            Selectable target = IsReturnTarget(fallbackSelection) ? fallbackSelection : previousSelection;
            if (!IsReturnTarget(target))
            {
                UINavigationContainer parent = FindParent();
                target = parent != null ? parent.FindRecovery() : null;
            }

            returnSelection = target;
            returnSystem = system;
            returnOrigin = current;
            returnRoot = transform;
            returnPending = true;
            ProcessReturn();
        }

        void Update()
        {
            if (updateFrame == Time.frameCount)
                return;

            updateFrame = Time.frameCount;
            UpdateNavigation();
        }

        void LateUpdate()
        {
            if (inputFrame == Time.frameCount)
                return;

            inputFrame = Time.frameCount;
            UpdateNavigation();
            ProcessSelection();
        }

        void OnTransformChildrenChanged() => RefreshNavigation();
      
        void OnTransformParentChanged() => RefreshAll();

        static void RegisterCanvas()
        {
            if (canvasRegistered)
                return;

            // Subscribe after uGUI's layout callback, including when the first Canvas is created later
            _ = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases += OnCanvasRender;
            canvasRegistered = true;
        }

        static void OnCanvasRender()
        {
            // Canvas layout has settled here; refresh links before the next input update.
            UpdateNavigation();
            ProcessReturn();
        }

        /// <summary>
        /// Call after adding, removing, or reparenting controls, changing manual lists, or changing layout from code.
        /// Existing controls' activity, interactability, navigation, and geometry are also monitored automatically.
        /// </summary>
        public void RefreshNavigation()
        {
            fetchPending = true;
            membershipReady = false;
            navigationDirty = true;
        }

        /// <summary>
        /// Populate the manual list in hierarchy order, including inactive and non-interactable controls.
        /// </summary>
        public void FetchInteractables()
        {
            GetComponentsInChildren(true, interactiveElements);
            RefreshNavigation();
        }

        public void ApplyNavigationRestrictions()
        {
            restrictNavigation = true;
            RefreshNavigation();

            if (Application.isPlaying)
                UpdateNavigation();
        }

        public void RestoreNavigation()
        {
            restrictNavigation = false;
            navigationDirty = true;

            if (Application.isPlaying)
                UpdateNavigation();
        }

        public bool SelectDefault() => Select(defaultSelection);

        public bool RestoreSelection() => Select(FindRecovery());

        /// <summary>
        /// Move once through the current order. Unavailable controls are skipped with a bounded search.
        /// Only the innermost active container handles keyboard Tab; this method can also be called explicitly.
        /// </summary>
        public void TabToNext(bool reverse = false)
        {
            if (!isActiveAndEnabled)
                return;

            UpdateNavigation();
            EventSystem system = EventSystem.current;
            if (system == null || system.alreadySelecting)
                return;

            GameObject current = system.currentSelectedGameObject;
            int index = -1;
            for (int i = 0; i < tabElements.Count; i++)
            {
                if (tabElements[i] != null && tabElements[i].gameObject == current)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0) { index = reverse ? tabElements.Count : -1; }
            for (int i = 0; i < tabElements.Count; i++)
            {
                index += reverse ? -1 : 1;
                if (index < 0 || index >= tabElements.Count)
                {
                    if (!wrapAround)
                        return;

                    index = reverse ? tabElements.Count - 1 : 0;
                }

                Selectable candidate = tabElements[index];
                if (CanSelect(candidate) && !excludedFromTabOrder.Contains(candidate))
                {
                    Select(candidate);
                    return;
                }
            }
        }

        static void RefreshAll()
        {
            for (int i = 0; i < containers.Count; i++)
                containers[i].fetchPending = true;

            navigationDirty = true;
        }

        static void UpdateNavigation()
        {
            if (rebuilding || containers.Count == 0)
                return;

            if (selectableCount != Selectable.allSelectableCount)
            {
                selectableCount = Selectable.allSelectableCount;
                RefreshAll();
            }

            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer container = containers[i];
                if (container.cachedAutoFetch != container.autoFetchOnEnable)
                    container.fetchPending = true;

                if (container.fetchPending)
                {
                    if (container.autoFetchOnEnable)
                        container.GetComponentsInChildren(true, container.interactiveElements);

                    container.fetchPending = false;
                    navigationDirty = true;
                }

                if (container.HasChanges())
                    navigationDirty = true;
            }

            if (navigationDirty)
                RebuildNavigation();
        }

        bool HasChanges()
        {
            if (cachedRestriction != restrictNavigation || cachedTabOrder != tabOrder || cachedAutoFetch != autoFetchOnEnable
                || elementStates.Count != interactiveElements.Count)
                return true;

            for (int i = 0; i < elementStates.Count; i++)
            {
                ElementState state = elementStates[i];
                Selectable selectable = interactiveElements[i];
                if (state.selectable != selectable)
                    return true;
               
                if (selectable == null)
                {
                    if (state.usable)
                        return true;

                    continue;
                }

                Transform elementTransform = selectable.transform;
                Rect rect = elementTransform is RectTransform rt ? rt.rect : default;
                if (state.parent != elementTransform.parent || state.matrix != elementTransform.localToWorldMatrix
                    || state.rect != rect || state.usable != IsUsable(selectable)
                    || !SameNavigation(state.navigation, selectable.navigation))
                    return true;
            }

            return false;
        }

        static void RebuildNavigation()
        {
            if (rebuilding)
                return;

            rebuilding = true;
            navigationDirty = false;
            RestoreLinks();

            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer container = containers[i];
                container.members.Clear();
                for (int j = 0; j < container.interactiveElements.Count; j++)
                {
                    Selectable selectable = container.interactiveElements[j];
                    if (selectable != null)
                        container.members.Add(selectable);
                }
                container.membershipReady = true;
            }

            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer container = containers[i];
                if (!container.isActiveAndEnabled || !container.restrictNavigation)
                    continue;

                for (int j = 0; j < container.interactiveElements.Count; j++)
                {
                    Selectable source = container.interactiveElements[j];
                    if (!container.Contains(source) || !IsUsable(source) || originalNavigations.ContainsKey(source))
                        continue;

                    UINavigationContainer boundary = FindOwner(source, true);
                    if (boundary == null)
                        continue;

                    Navigation original = source.navigation;
                    Navigation navigation = original;
                    navigation.mode = Navigation.Mode.Explicit;
                    navigation.selectOnLeft = boundary.FindNeighbor(source, original, Vector3.left);
                    navigation.selectOnRight = boundary.FindNeighbor(source, original, Vector3.right);
                    navigation.selectOnUp = boundary.FindNeighbor(source, original, Vector3.up);
                    navigation.selectOnDown = boundary.FindNeighbor(source, original, Vector3.down);
                    source.navigation = navigation;
                    originalNavigations.Add(source, new NavigationState { original = original, applied = navigation });
                }
            }

            for (int i = 0; i < containers.Count; i++)
                containers[i].CacheElements();

            if (containers.Count == 0)
                RestoreLinks();

            rebuilding = false;
        }

        static void RestoreLinks()
        {
            foreach (var entry in originalNavigations)
            {
                // Respect navigation replaced by another system while the container was active
                if (entry.Key != null && SameNavigation(entry.Key.navigation, entry.Value.applied))
                    entry.Key.navigation = entry.Value.original;
            }
            originalNavigations.Clear();
        }

        void CacheElements()
        {
            elementStates.Clear();
            tabElements.Clear();
            cachedAutoFetch = autoFetchOnEnable;
            cachedRestriction = restrictNavigation;
            cachedTabOrder = tabOrder;

            for (int i = 0; i < interactiveElements.Count; i++)
            {
                Selectable selectable = interactiveElements[i];
                ElementState state = new() { selectable = selectable };
                if (selectable != null)
                {
                    Transform elementTransform = selectable.transform;
                    state.parent = elementTransform.parent;
                    state.matrix = elementTransform.localToWorldMatrix;
                    state.rect = elementTransform is RectTransform rt ? rt.rect : default;
                    state.navigation = selectable.navigation;
                    state.usable = IsUsable(selectable);
                    if (Contains(selectable) && !tabElements.Contains(selectable))
                        tabElements.Add(selectable);
                }
                elementStates.Add(state);
            }

            if (tabOrder != TabOrder.Position)
                return;

            for (int i = 1; i < tabElements.Count; i++)
            {
                Selectable value = tabElements[i];
                Vector3 position = transform.InverseTransformPoint(GetCenter(value));
                int j = i - 1;
                while (j >= 0)
                {
                    Vector3 previous = transform.InverseTransformPoint(GetCenter(tabElements[j]));
                    if (previous.y > position.y || (previous.y == position.y && previous.x <= position.x))
                        break;

                    tabElements[j + 1] = tabElements[j];
                    j--;
                }
                tabElements[j + 1] = value;
            }
        }

        Selectable FindNeighbor(Selectable source, Navigation original, Vector3 direction)
        {
            bool horizontal = direction.x != 0;
            if (original.mode == Navigation.Mode.Explicit)
            {
                Selectable target = horizontal
                    ? (direction.x < 0 ? original.selectOnLeft : original.selectOnRight)
                    : (direction.y < 0 ? original.selectOnDown : original.selectOnUp);
                return CanSelect(target) ? target : null;
            }

            Navigation.Mode axis = horizontal ? Navigation.Mode.Horizontal : Navigation.Mode.Vertical;
            if ((original.mode & axis) == 0)
                return null;

            Transform sourceTransform = source.transform;
            Vector3 worldDirection = sourceTransform.rotation * direction;
            Vector3 edge = Vector3.zero;
            if (sourceTransform is RectTransform rect)
                edge = rect.rect.center + Vector2.Scale(rect.rect.size, (Vector2)direction * 0.5f);

            Vector3 origin = sourceTransform.TransformPoint(edge);
            Selectable best = null;
            Selectable wrapped = null;
            float bestScore = float.NegativeInfinity;
            float wrapScore = float.NegativeInfinity;
            bool canWrap = original.wrapAround && (original.mode == Navigation.Mode.Horizontal || original.mode == Navigation.Mode.Vertical);

            for (int i = 0; i < interactiveElements.Count; i++)
            {
                Selectable candidate = interactiveElements[i];
                if (candidate == source || !CanSelect(candidate))
                    continue;

                Vector3 delta = GetCenter(candidate) - origin;
                float dot = Vector3.Dot(worldDirection, delta);
                if (dot > 0)
                {
                    float score = dot / delta.sqrMagnitude;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
                else if (canWrap && dot < 0)
                {
                    float score = -dot * delta.sqrMagnitude;
                    if (score > wrapScore)
                    {
                        wrapScore = score;
                        wrapped = candidate;
                    }
                }
            }

            return best != null ? best : wrapped;
        }

        static void ProcessSelection()
        {
            ProcessReturn();
            EventSystem system = EventSystem.current;
            if (system == null || !system.isActiveAndEnabled || system.alreadySelecting)
                return;

            if (selectionSystem != system)
            {
                selectionSystem = system;
                focusOwner = null;
            }

            // Resolve all enable requests before sending one selection event
            UINavigationContainer request = null;
            Selectable requestTarget = null;
            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer container = containers[i];
                if (!container.isActiveAndEnabled || !container.selectionPending)
                    continue;

                container.selectionPending = false;
                if (container.selectOnEnable == EnableSelection.None)
                    continue;

                Selectable target = container.selectOnEnable switch
                {
                    EnableSelection.Default => container.defaultSelection,
                    EnableSelection.First => container.FindFirst(),
                    _ => container.CanSelect(container.lastSelection) ? container.lastSelection : container.FindRecovery(),
                };

                if (container.CanSelect(target) && (request == null || !request.transform.IsChildOf(container.transform)))
                {
                    request = container;
                    requestTarget = target;
                }
            }

            if (request != null)
                request.Select(requestTarget);

            GameObject current = system.currentSelectedGameObject;
            Selectable selected = current != null ? current.GetComponent<Selectable>() : null;
            if (current != null && current.activeInHierarchy && (selected == null || IsUsable(selected)))
            {
                focusOwner = FindOwner(selected);
                RememberSelection(selected);
            }
            else
            {
                UINavigationContainer owner = selected != null ? FindOwner(selected) : focusOwner;
                if (owner == null || !owner.isActiveAndEnabled)
                {
                    owner = null;
                    for (int i = containers.Count - 1; i >= 0; i--)
                    {
                        if (containers[i].isActiveAndEnabled && containers[i].ensureSelection)
                        {
                            owner = containers[i];
                            break;
                        }
                    }
                }

                if (owner != null && owner.ensureSelection)
                    owner.RestoreSelection();
            }

            if (system.sendNavigationEvents && system.isFocused && Utilities.HandleTabNavigation(out bool reverse))
            {
                current = system.currentSelectedGameObject;
                UINavigationContainer owner = current != null ? FindOwner(current.GetComponent<Selectable>()) : focusOwner;
                if (current == null && (owner == null || !owner.isActiveAndEnabled))
                {
                    for (int i = containers.Count - 1; i >= 0; i--)
                    {
                        UINavigationContainer candidate = containers[i];
                        if (candidate.isActiveAndEnabled && candidate.tabNavigation && candidate.FindFirst() != null)
                        {
                            owner = candidate;
                            break;
                        }
                    }
                }

                if (owner != null && owner.tabNavigation)
                    owner.TabToNext(reverse);
            }
        }

        static void ProcessReturn()
        {
            if (!returnPending)
                return;

            EventSystem system = returnSystem;
            if (system != null && system.alreadySelecting)
                return;

            Selectable target = returnSelection;
            GameObject origin = returnOrigin;
            Transform root = returnRoot;
            returnPending = false;
            returnSelection = null;
            returnSystem = null;
            returnOrigin = null;
            returnRoot = null;

            if (system == null || system != EventSystem.current)
                return;

            GameObject current = system.currentSelectedGameObject;
            if (current != null && current != origin && current.activeInHierarchy && (root == null || !current.transform.IsChildOf(root)))
                return;

            if (!IsUsable(target))
                target = null;

            system.SetSelectedGameObject(target != null ? target.gameObject : null);
            focusOwner = FindOwner(target);
            RememberSelection(target);
        }

        static void RememberSelection(Selectable selectable)
        {
            if (!IsUsable(selectable))
                return;

            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer container = containers[i];
                if (!container.Contains(selectable))
                    continue;

                container.lastSelection = selectable;
                container.lastPosition = GetCenter(selectable);
                container.hasLastPosition = true;
            }
        }

        Selectable FindRecovery()
        {
            if (recoveryMode == RecoveryMode.RestoreLast && CanSelect(lastSelection))
                return lastSelection;
            if (recoveryMode != RecoveryMode.First && recoveryMode != RecoveryMode.Nearest && CanSelect(defaultSelection))
                return defaultSelection;
            if (recoveryMode == RecoveryMode.First || recoveryMode == RecoveryMode.Default || !hasLastPosition)
                return FindFirst();

            Selectable nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < interactiveElements.Count; i++)
            {
                Selectable candidate = interactiveElements[i];
                if (!CanSelect(candidate))
                    continue;

                float distance = (GetCenter(candidate) - lastPosition).sqrMagnitude;
                if (distance < nearestDistance) 
                {
                    nearestDistance = distance; 
                    nearest = candidate; 
                }
            }
            return nearest;
        }

        Selectable FindFirst()
        {
            for (int i = 0; i < tabElements.Count; i++)
            {
                if (CanSelect(tabElements[i]))
                    return tabElements[i];
            }
            return null;
        }

        bool Select(Selectable selectable)
        {
            EventSystem system = EventSystem.current;
            if (!isActiveAndEnabled || system == null || system.alreadySelecting || !CanSelect(selectable))
                return false;

            system.SetSelectedGameObject(selectable.gameObject);
            // Selection callbacks may disable this container or redirect focus
            GameObject current = system.currentSelectedGameObject;
            Selectable selected = current != null ? current.GetComponent<Selectable>() : null;
            focusOwner = FindOwner(selected);
            RememberSelection(selected);
            return selected == selectable;
        }

        bool Contains(Selectable selectable) => selectable != null && selectable.transform.IsChildOf(transform)
            && (membershipReady ? members.Contains(selectable) : interactiveElements.Contains(selectable));

        bool CanSelect(Selectable selectable)
        {
            if (!IsUsable(selectable) || !Contains(selectable))
                return false;

            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer parent = containers[i];
                if (parent != this && parent.isActiveAndEnabled && parent.restrictNavigation
                    && transform.IsChildOf(parent.transform) && !parent.Contains(selectable))
                    return false;
            }
            return true;
        }

        bool IsReturnTarget(Selectable selectable)
        {
            if (!IsUsable(selectable) || selectable.transform.IsChildOf(transform))
                return false;

            UINavigationContainer parent = FindParent();
            return parent == null || parent.CanSelect(selectable);
        }

        bool HasActiveChildOwner(Selectable selected)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer child = containers[i];
                if (child != this && child.isActiveAndEnabled && child.transform.IsChildOf(transform) && child.Contains(selected))
                    return true;
            }
            return false;
        }

        UINavigationContainer FindParent()
        {
            UINavigationContainer result = null;
            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer candidate = containers[i];
                if (candidate != this && candidate.isActiveAndEnabled && transform.IsChildOf(candidate.transform)
                    && (result == null || candidate.transform.IsChildOf(result.transform)))
                    result = candidate;
            }
            return result;
        }

        static UINavigationContainer FindOwner(Selectable selectable, bool boundaryOnly = false)
        {
            UINavigationContainer result = null;
            for (int i = 0; i < containers.Count; i++)
            {
                UINavigationContainer candidate = containers[i];
                if (!candidate.isActiveAndEnabled || (boundaryOnly && !candidate.restrictNavigation) || !candidate.Contains(selectable))
                    continue;
                if (result == null || candidate.transform.IsChildOf(result.transform))
                    result = candidate;
            }
            return result;
        }

        static bool IsUsable(Selectable selectable) => selectable != null && selectable.isActiveAndEnabled
            && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None;

        static Vector3 GetCenter(Selectable selectable)
        {
            Transform elementTransform = selectable.transform;
            return elementTransform.TransformPoint(elementTransform is RectTransform rect ? (Vector3)rect.rect.center : Vector3.zero);
        }

        static bool SameNavigation(Navigation a, Navigation b) => a.mode == b.mode && a.wrapAround == b.wrapAround
            && a.selectOnLeft == b.selectOnLeft && a.selectOnRight == b.selectOnRight
            && a.selectOnUp == b.selectOnUp && a.selectOnDown == b.selectOnDown;

#if UNITY_EDITOR
        void OnValidate() => RefreshNavigation();
#endif
    }
}