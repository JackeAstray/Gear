using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Hidden helper that Soft Mask keeps on every object below it. It masks the object's Graphic and
    /// reports hierarchy changes, so new or moved children are found without scanning every frame.
    /// Added and removed automatically; never saved with the scene or prefab.
    /// </summary>
    [DisallowMultipleComponent, ExecuteAlways, NoAutoStaticsCleanup]
    [AddComponentMenu("")]
    public class SoftMaskable : MonoBehaviour, IMaterialModifier
    {
        const HideFlags HelperHideFlags = HideFlags.HideInInspector | HideFlags.NotEditable
            | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        /// <summary>
        /// The nearest active Soft Mask above this object, or null.
        /// </summary>
        public SoftMask AssignedMask => ResolveMask();

        // Runtime state only. Domain reloads must start fresh, since the materials are destroyed before them.
        [NonSerialized] Graphic graphic;
        [NonSerialized] SoftMask mask;
        [NonSerialized] Transform knownParent;
        [NonSerialized] SoftMask.MaterialEntry materialEntry;
        [NonSerialized] internal int EntryIndex = -1;
        internal Graphic Graphic => graphic;
        [NonSerialized] int maskVersion;
        [NonSerialized] bool underMask;

        // Hierarchy tracking
        static readonly HashSet<Transform> pendingSubtrees = new();
        static readonly HashSet<Transform> pendingChildren = new();
        static readonly HashSet<SoftMaskable> pendingReplacements = new();
        static readonly HashSet<SoftMaskable> pendingDirty = new();
        static readonly List<Transform> processSubtrees = new();
        static readonly HashSet<Transform> processSubtreeSet = new();
        static readonly List<Transform> processChildren = new();
        static readonly List<SoftMaskable> processHelpers = new();
        static readonly List<MonoBehaviour> behaviours = new();
        static CanvasUpdateRegistry knownRegistry;
        static bool callbacksRegistered;
        static bool processing;
        static bool rebuildAfterAssign;

        void OnEnable()
        {
            NormalizeHideFlags();

            // Instantiated copies and helpers restored after a domain reload start without an assignment
            if (maskVersion == 0 && !processing)
                QueueSubtree(transform);
        }

        void OnDisable()
        {
            ReleaseMaterial();
        }

        void OnDestroy()
        {
            ReleaseMaterial();
            pendingReplacements.Remove(this);
            pendingDirty.Remove(this);

            if (graphic != null && graphic.IsActive() && !CanvasUpdateRegistry.IsRebuildingGraphics())
                graphic.SetMaterialDirty();
        }

        void OnTransformParentChanged()
        {
            maskVersion = 0;

            // Unity sends this to the whole moved branch. Only its root needs a pass.
            if (transform.parent == knownParent)
                return;

            knownParent = transform.parent;
            QueueSubtree(transform);
        }

        void OnTransformChildrenChanged() => QueueChildren(transform);

        void OnCanvasHierarchyChanged() => SetMaterialDirty();

        void NormalizeHideFlags()
        {
            if (hideFlags == HelperHideFlags)
                return;
#if UNITY_EDITOR
            // Helpers saved by older versions inside prefab assets stay as they are
            if (UnityEditor.EditorUtility.IsPersistent(this) || UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this) != null)
                return;
#endif
            hideFlags = HelperHideFlags;
        }

        SoftMask ResolveMask()
        {
            if (maskVersion == SoftMask.TopologyVersion)
                return mask;

            FindMaskContext(transform.parent, out bool hasMask, out SoftMask activeMask);
            maskVersion = SoftMask.TopologyVersion;
            underMask = hasMask;

            if (mask != activeMask)
            {
                ReleaseMaterial();
                mask = activeMask;
            }

            // Moved out of every mask without a hierarchy message reaching us
            if (!hasMask && !processing)
                QueueSubtree(transform);

            return mask;
        }

        void Assign(SoftMask activeMask, bool hasMask)
        {
            maskVersion = SoftMask.TopologyVersion;
            underMask = hasMask;
            knownParent = transform.parent;

            if (mask == activeMask)
                return;

            ReleaseMaterial();
            mask = activeMask;
            SetMaterialDirty();
        }

        internal void OnMaskReleased()
        {
            // The owning mask cleared its cache. Its user list is reset by the caller.
            materialEntry = null;
            EntryIndex = -1;
            maskVersion = 0;
            SetMaterialDirty();
        }

        void ReleaseMaterial()
        {
            if (materialEntry == null)
                return;

            materialEntry.RemoveUser(this);
            materialEntry = null;
        }

        void SetMaterialDirty()
        {
            if (graphic == null && !TryGetComponent(out graphic))
                return;
            if (!graphic.IsActive())
                return;

            if (CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                pendingDirty.Add(this);
                RequestProcessing();
                return;
            }

            graphic.SetMaterialDirty();

            // New graphics found after the canvas rebuild still render masked this frame
            if (rebuildAfterAssign)
                graphic.Rebuild(CanvasUpdate.PreRender);
        }

        bool IsBeforeMaterialModifier()
        {
            // A Graphic or stencil Mask after this helper would copy the soft-masked material and
            // freeze its mask data. This happens when a Graphic is added to a tracked object later.
            GetComponents(behaviours);
            bool foundSelf = false;

            for (int i = 0; i < behaviours.Count; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == this) { foundSelf = true; }
                else if (foundSelf && (behaviour is Graphic || behaviour is Mask))
                {
                    behaviours.Clear();
                    return true;
                }
            }

            behaviours.Clear();
            return false;
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || baseMaterial == null)
            {
                ReleaseMaterial();
                return baseMaterial;
            }

            if (graphic == null && !TryGetComponent(out graphic))
                return baseMaterial;

            if (graphic is MaskableGraphic maskableGraphic && !maskableGraphic.maskable)
            {
                ReleaseMaterial();
                return baseMaterial;
            }

            if (IsBeforeMaterialModifier() && pendingReplacements.Add(this))
                RequestProcessing();

            SoftMask activeMask = ResolveMask();
            SoftMask.MaterialEntry entry = activeMask != null ? activeMask.GetMaterialEntry(baseMaterial, graphic.canvas) : null;

            if (materialEntry != entry)
            {
                ReleaseMaterial();
                materialEntry = entry;
                materialEntry?.AddUser(this);
            }

            return materialEntry != null ? materialEntry.ModifiedMaterial : baseMaterial;
        }

        #region Hierarchy Tracking
        internal static void QueueSubtree(Transform root)
        {
            if (root != null && pendingSubtrees.Add(root))
                RequestProcessing();
        }

        internal static void QueueChildren(Transform parent)
        {
            if (parent != null && pendingChildren.Add(parent))
                RequestProcessing();
        }

        internal static void EnsureCallbacks()
        {
            if (callbacksRegistered)
                return;

            callbacksRegistered = true;
            Canvas.preWillRenderCanvases += OnPreWillRenderCanvases;
            SubscribeAfterRegistry();
        }

        static void SubscribeAfterRegistry()
        {
            // The post-rebuild pass must follow the registry's graphic rebuild. uGUI 2.7 recreates the
            // registry when entering play mode without a domain reload, so line up behind the new one.
            knownRegistry = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        // Unity 6.6 clears the Canvas render events when entering play mode without a domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RestoreCallbacks()
        {
            if (!callbacksRegistered)
                return;

            Canvas.preWillRenderCanvases -= OnPreWillRenderCanvases;
            Canvas.preWillRenderCanvases += OnPreWillRenderCanvases;
            SubscribeAfterRegistry();
        }

        static void OnPreWillRenderCanvases()
        {
            if (!ReferenceEquals(knownRegistry, CanvasUpdateRegistry.instance))
                SubscribeAfterRegistry();

            ProcessPending(false);
        }

        static void OnWillRenderCanvases()
        {
            // Catches objects created during the rebuild, such as TextMeshPro sub meshes
            ProcessPending(true);
            SoftMask.UpdateActiveMasks();
        }

        static void RequestProcessing()
        {
            EnsureCallbacks();
#if UNITY_EDITOR
            // Edit mode only renders canvases on repaint
            if (!Application.isPlaying && !editorUpdateQueued)
            {
                editorUpdateQueued = true;
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }
#endif
        }

        static bool HasPendingWork => pendingSubtrees.Count > 0 || pendingChildren.Count > 0
            || pendingReplacements.Count > 0 || pendingDirty.Count > 0;

        /// <summary>
        /// Applies queued hierarchy changes. Returns false if they have to wait for a safe point.
        /// </summary>
        internal static bool ProcessPending(bool afterRebuild = false)
        {
            if (processing || !HasPendingWork)
                return true;
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout())
                return false;
#if UNITY_EDITOR
            if (UnityEditor.Undo.isProcessing)
                return false;
#endif
            processing = true;
            rebuildAfterAssign = afterRebuild;

            try
            {
                // Passes can queue follow-up work, such as an orphan found while resolving a material
                for (int pass = 0; pass < 4 && HasPendingWork; pass++)
                {
                    ProcessReplacements();
                    ProcessSubtrees();
                    ProcessChildren();
                    ProcessDirty();
                }
            }
            finally
            {
                processing = false;
                rebuildAfterAssign = false;
            }

            return true;
        }

        static void ProcessSubtrees()
        {
            if (pendingSubtrees.Count == 0)
                return;

            processSubtrees.AddRange(pendingSubtrees);
            pendingSubtrees.Clear();
            for (int i = 0; i < processSubtrees.Count; i++)
                processSubtreeSet.Add(processSubtrees[i]);

            for (int i = 0; i < processSubtrees.Count; i++)
            {
                Transform root = processSubtrees[i];
                if (root == null || HasQueuedAncestor(root))
                    continue;

                FindMaskContext(root.parent, out bool hasMask, out SoftMask activeMask);
                Visit(root, hasMask, activeMask);
            }

            processSubtrees.Clear();
            processSubtreeSet.Clear();
        }

        static void ProcessChildren()
        {
            if (pendingChildren.Count == 0)
                return;

            processChildren.AddRange(pendingChildren);
            pendingChildren.Clear();

            for (int i = 0; i < processChildren.Count; i++)
            {
                Transform parent = processChildren[i];
                if (parent == null)
                    continue;

                FindMaskContext(parent, out bool hasMask, out SoftMask activeMask);
                if (!hasMask)
                    continue;

                // Only new children, or ones whose helpers lost their assignment, need a full pass
                for (int c = 0, count = parent.childCount; c < count; c++)
                {
                    Transform child = parent.GetChild(c);
                    if (!child.TryGetComponent(out SoftMaskable helper) || helper.maskVersion != SoftMask.TopologyVersion || !helper.underMask)
                        Visit(child, true, activeMask);
                }
            }

            processChildren.Clear();
        }

        static void ProcessReplacements()
        {
            if (pendingReplacements.Count == 0)
                return;

            processHelpers.AddRange(pendingReplacements);
            pendingReplacements.Clear();

            for (int i = 0; i < processHelpers.Count; i++)
            {
                SoftMaskable helper = processHelpers[i];
                if (helper == null || !helper.IsBeforeMaterialModifier())
                    continue;

                // Re-adding puts the helper after the Graphic and any Mask
                Transform target = helper.transform;
                if (Remove(helper))
                    QueueSubtree(target);
            }

            processHelpers.Clear();
        }

        static void ProcessDirty()
        {
            if (pendingDirty.Count == 0)
                return;

            processHelpers.AddRange(pendingDirty);
            pendingDirty.Clear();

            for (int i = 0; i < processHelpers.Count; i++)
            {
                if (processHelpers[i] != null)
                    processHelpers[i].SetMaterialDirty();
            }

            processHelpers.Clear();
        }

        static bool HasQueuedAncestor(Transform target)
        {
            // A moved branch or a domain reload queues many nested roots. Visit the top one only.
            if (processSubtreeSet.Count < 2)
                return false;

            for (Transform current = target.parent; current != null; current = current.parent)
            {
                if (processSubtreeSet.Contains(current))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds whether any Soft Mask sits on or above <paramref name="current"/>, and the nearest active one.
        /// </summary>
        static void FindMaskContext(Transform current, out bool hasMask, out SoftMask activeMask)
        {
            hasMask = false;
            activeMask = null;

            while (current != null)
            {
                if (current.TryGetComponent(out SoftMask currentMask))
                {
                    hasMask = true;
                    if (currentMask.isActiveAndEnabled)
                    {
                        activeMask = currentMask;
                        return;
                    }
                }

                // An assigned helper already knows the answer for everything above it
                if (current.TryGetComponent(out SoftMaskable helper) && helper.maskVersion == SoftMask.TopologyVersion)
                {
                    hasMask |= helper.underMask;
                    activeMask = helper.mask;
                    return;
                }

                current = current.parent;
            }
        }

        static void Visit(Transform target, bool hasMask, SoftMask activeMask)
        {
            target.TryGetComponent(out SoftMaskable helper);

            if (hasMask)
            {
                if (helper == null)
                    helper = Add(target);
                if (helper != null)
                    helper.Assign(activeMask, true);
            }
            else if (helper != null && !Remove(helper))
            {
                helper.Assign(null, false);
            }

            if (target.TryGetComponent(out SoftMask ownMask))
            {
                hasMask = true;
                if (ownMask.isActiveAndEnabled)
                    activeMask = ownMask;
            }

            for (int i = 0, count = target.childCount; i < count; i++)
                Visit(target.GetChild(i), hasMask, activeMask);
        }

        static SoftMaskable Add(Transform target)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(target))
                return null;
#endif
            SoftMaskable helper = target.gameObject.AddComponent<SoftMaskable>();
            if (helper != null)
                helper.hideFlags = HelperHideFlags;

            return helper;
        }

        static bool Remove(SoftMaskable helper)
        {
#if UNITY_EDITOR
            // Never touch prefab assets, or helpers that older versions saved into them
            if (UnityEditor.EditorUtility.IsPersistent(helper) || UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(helper) != null)
                return false;
#endif
            DestroyImmediate(helper);
            return true;
        }
        #endregion

#if UNITY_EDITOR
        static bool editorUpdateQueued;

        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorEvents()
        {
            // Safety net for edit-mode changes that bypass transform messages, such as prefab merges
            UnityEditor.EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            UnityEditor.EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
        }

        static void OnEditorHierarchyChanged()
        {
            if (Application.isPlaying)
                return;

            SoftMask.QueueActiveMasks();
        }

        static void OnEditorUpdate()
        {
            if (!ProcessPending() && HasPendingWork)
                return;

            editorUpdateQueued = false;
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
        }
#endif
    }
}
