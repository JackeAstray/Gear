using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Connects one uGUI Graphic or TMP text component to its nearest CurvedCanvas owner.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent, RequireComponent(typeof(Graphic), typeof(RectTransform))]
    public class CurvedCanvasGraphic : BaseMeshEffect
    {
        CurvedCanvas owner;
        Graphic source;
        TMP_Text text;
        RectTransform sourceRect;
        Action<TMP_TextInfo> preRenderText;
        bool subscribed;
        bool hasMatrix;
        Matrix4x4 previousToCanvas;

        protected override void OnEnable()
        {
            source = graphic;
            sourceRect = (RectTransform)transform;
            text = source as TMP_Text;
            CurvedCanvas previousOwner = owner;
            ResolveOwner();
           
            if (ReferenceEquals(previousOwner, owner))
                OwnerStateChanged();
           
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Unsubscribe();
            MarkDirty(); // Regenerate cached TMP geometry flat as well as the ordinary Graphic
            hasMatrix = false;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= CleanupInEditor;
#endif
            Unsubscribe();

            if (owner != null)
                owner.Unregister(this);
           
            owner = null;
            base.OnDestroy();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            ResolveOwner();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            ResolveOwner();
        }

        internal void ResolveOwner()
        {
            // A disabled nested owner is still a boundary. Submeshes are curved by their TMP parent.
            Initialize(graphic is TMP_SubMeshUI ? null : GetComponentInParent<CurvedCanvas>(true));
        }

        internal void Initialize(CurvedCanvas canvas)
        {
            if (ReferenceEquals(owner, canvas))
                return;
           
            if (owner != null)
                owner.Unregister(this);

            owner = canvas;
           
            if (owner != null)
                owner.Register(this);

            OwnerStateChanged();
        }

        internal void OwnerStateChanged()
        {
            bool shouldSubscribe = isActiveAndEnabled && owner != null && owner.IsEffective && text != null;
            if (shouldSubscribe && !subscribed)
            {
                preRenderText ??= OnPreRenderText;
                text.OnPreRenderText += preRenderText;
                subscribed = true;
            }
            else if (!shouldSubscribe)
                Unsubscribe();
            hasMatrix = false;
            MarkDirty();
#if UNITY_EDITOR
            if (owner == null || !owner.IsEffective)
                QueueEditorCleanup();
#endif
        }

        void Unsubscribe()
        {
            if (!subscribed)
                return;
            // Remove the managed event even if Unity already destroyed the native text object.
            if (!ReferenceEquals(text, null))
                text.OnPreRenderText -= preRenderText;
            subscribed = false;
        }

        void OnPreRenderText(TMP_TextInfo info)
        {
            if (IsActive() && owner != null && owner.IsEffective)
                owner.CurveTMPTextInfo(text, info);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (IsActive() && owner != null && owner.IsEffective && text == null && source is not TMP_SubMeshUI)
                owner.CurveMesh(vh, source);
        }

        internal void CheckForChanges(in Matrix4x4 worldToCanvas, bool forceDirty)
        {
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled || sourceRect == null)
                return;
           
            Matrix4x4 toCanvas = worldToCanvas * sourceRect.localToWorldMatrix;
            if (!forceDirty && hasMatrix && previousToCanvas.Equals(toCanvas))
                return;
          
            previousToCanvas = toCanvas;
            hasMatrix = true;

            if (text != null)
            {
                // SetVerticesDirty alone does not regenerate TMP. ForceMeshUpdate sets its internal
                // generation flag directly, without havePropertiesChanged's SetAllDirty/layout work.
                // This runs after layout and before Graphic rebuilds, never inside TMP generation.
                if (text.font != null && text.canvas != null)
                    text.ForceMeshUpdate();
            }
            else { source.SetVerticesDirty(); }
        }

        void MarkDirty()
        {
            if (source == null)
                source = graphic;
            if (text == null)
                text = source as TMP_Text;
           
            // Lifecycle changes may occur during loading or a rebuild. Let Unity schedule restoration.
            if (text != null)
                text.havePropertiesChanged = true;
            else if (source != null)
                source.SetVerticesDirty();
        }

#if UNITY_EDITOR
        bool cleanupQueued;

        internal void QueueEditorCleanup()
        {
            if (cleanupQueued || Application.IsPlaying(gameObject))
                return;

            cleanupQueued = true;
            UnityEditor.EditorApplication.delayCall += CleanupInEditor;
        }

        void CleanupInEditor()
        {
            cleanupQueued = false;

            if (this == null || Application.IsPlaying(gameObject) || (hideFlags & HideFlags.DontSaveInEditor) == 0
                || (owner != null && owner.IsEffective))
                return;

            // A delay avoids modifying the hierarchy in OnValidate, OnDisable or scene loading
            DestroyImmediate(this);
        }
#endif
    }
}