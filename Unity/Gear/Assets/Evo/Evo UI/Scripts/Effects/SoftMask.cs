using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Applies a soft mask to child Graphic elements.
    /// </summary>
    [AddComponentMenu("Evo/UI/Effects/Soft Mask"), HelpURL(Constants.HelpUrl + "effects/soft-mask")]
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Graphic), typeof(RectTransform)), NoAutoStaticsCleanup]
    public class SoftMask : UIBehaviour, IMeshModifier, ICanvasRaycastFilter
    {
        [Tooltip("Should the graphic serving as the mask be drawn?")]
        [SerializeField] private bool showMaskGraphic = true;

        [Tooltip("Invert the mask so opaque areas are hidden and transparent areas are revealed.")]
        [SerializeField] private bool invertMask;

        // Constants
        public const int MaxMaskDepth = 4;

        // Cache
        Graphic maskGraphic;
        RectTransform rectTransform;
        readonly Dictionary<MaterialKey, MaterialEntry> materialCache = new();
        readonly List<MaterialKey> staleMaterialKeys = new();
        readonly SoftMask[] maskStack = new SoftMask[MaxMaskDepth];
        UnityAction dirtyCallback;
        MaskProperties maskProperties;
        int maskCount;
        int stackVersion;
        [NonSerialized] int stackTopologyVersion;
        int dataVersion;
        bool didWarnMaskLimit;
        bool hasTransformSnapshot;
        bool hasMaskProperties;
        bool lastInvertMask;
        Rect lastRect;
        Matrix4x4 lastWorldToLocal;
        Texture lastMaskTexture;

        // Registry
        static readonly List<SoftMask> activeMasks = new();
        static readonly HashSet<Shader> unsupportedShaders = new();

        /// <summary>
        /// Changes whenever a mask is enabled, disabled or moved. Helpers and mask stacks resolve lazily against it.
        /// </summary>
        internal static int TopologyVersion { get; private set; } = 1;

        // Shader Serialization
        [SerializeField, HideInInspector] Shader embeddedShader;
        [SerializeField, HideInInspector] Shader embeddedTMPShader;
        [SerializeField, HideInInspector] Shader embeddedTMPMobileShader;
        [SerializeField, HideInInspector] Shader embeddedTMPBitmapShader;
        const string ShaderName = "Hidden/Evo/UI/Soft Mask";
        const string TMPShaderName = "Hidden/Evo/UI/Soft Mask TMP";
        const string TMPMobileShaderName = "Hidden/Evo/UI/Soft Mask TMP Mobile";
        const string TMPBitmapShaderName = "Hidden/Evo/UI/Soft Mask TMP Bitmap";
        const string TMPBitmapMobileKeyword = "EVO_TMP_BITMAP_MOBILE";
        const string TMPSpriteKeyword = "EVO_TMP_SPRITE";
        // Only masked variants compile the mask code, which keeps unmasked UI fast to load.
        // One basic mask (sprite, unclipped rectangle or ellipse) uses a much smaller evaluator; nested masks use the full one.
        internal const string SoftMaskKeyword = "EVO_UI_SOFT_MASK";
        internal const string BasicSoftMaskKeyword = "EVO_UI_SOFT_MASK_BASIC";

        // Shader Property IDs
        static readonly int PropsSupport = Shader.PropertyToID("_SoftMaskSupport");
        static readonly int PropsCount = Shader.PropertyToID("_SoftMask_Count");
        static readonly int PropsCanvasToLocalX = Shader.PropertyToID("_SoftMask_CanvasToLocalX");
        static readonly int PropsCanvasToLocalY = Shader.PropertyToID("_SoftMask_CanvasToLocalY");
        static readonly int PropsRect = Shader.PropertyToID("_SoftMask_Rect");
        static readonly int PropsData = Shader.PropertyToID("_SoftMask_Data");
        static readonly int PropsInvert = Shader.PropertyToID("_SoftMask_Invert");
        static readonly int PropsPRRect = Shader.PropertyToID("_SoftMask_PRRect");
        static readonly int PropsPRRadii = Shader.PropertyToID("_SoftMask_PRRadii");
        static readonly int PropsPRFillData = Shader.PropertyToID("_SoftMask_PRFillData");
        static readonly int PropsBorderData = Shader.PropertyToID("_SoftMask_BorderData");
        static readonly int PropsUVOuter = Shader.PropertyToID("_SoftMask_UVOuter");
        static readonly int PropsUVInner = Shader.PropertyToID("_SoftMask_UVInner");
        static readonly int PropsExternalAlpha = Shader.PropertyToID("_EnableExternalAlpha");
        static readonly int[] PropsTextures =
        {
            Shader.PropertyToID("_SoftMaskTex0"),
            Shader.PropertyToID("_SoftMaskTex1"),
            Shader.PropertyToID("_SoftMaskTex2"),
            Shader.PropertyToID("_SoftMaskTex3")
        };

        static readonly Vector4[] CanvasToLocalX = new Vector4[MaxMaskDepth];
        static readonly Vector4[] CanvasToLocalY = new Vector4[MaxMaskDepth];
        static readonly Vector4[] MaskRects = new Vector4[MaxMaskDepth];
        static readonly Vector4[] MaskData = new Vector4[MaxMaskDepth];
        static readonly Vector4[] ProceduralRects = new Vector4[MaxMaskDepth];
        static readonly Vector4[] ProceduralRadii = new Vector4[MaxMaskDepth];
        static readonly Vector4[] ProceduralFillData = new Vector4[MaxMaskDepth];
        static readonly Vector4[] BorderData = new Vector4[MaxMaskDepth];
        static readonly Vector4[] UVOuter = new Vector4[MaxMaskDepth];
        static readonly Vector4[] UVInner = new Vector4[MaxMaskDepth];
        static Vector4 MaskInvert;

        public bool ShowMaskGraphic
        {
            get => showMaskGraphic;
            set
            {
                if (showMaskGraphic == value)
                    return;

                showMaskGraphic = value;
                RefreshMaskGraphic();
            }
        }

        public bool InvertMask
        {
            get => invertMask;
            set
            {
                if (invertMask == value)
                    return;

                invertMask = value;
                MarkMaskPropertiesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            rectTransform = transform as RectTransform;
            TryGetComponent(out maskGraphic);

            LoadShaders();
            activeMasks.Add(this);
            RegisterGraphicCallbacks();
            MarkTopologyChanged();
            MarkMaskPropertiesDirty();
            RefreshMaskGraphic();

            // Children pick this mask up before the next canvas render
            SoftMaskable.EnsureCallbacks();
            SoftMaskable.QueueSubtree(transform);
        }

        protected override void OnDisable()
        {
            activeMasks.Remove(this);
            UnregisterGraphicCallbacks();
            MarkTopologyChanged();
            ClearCache(true);

            if (maskGraphic != null)
            {
                maskGraphic.SetVerticesDirty();
                maskGraphic.SetMaterialDirty();
            }

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ClearCache(false);

            // Removes helpers that no longer have any Soft Mask above them
            SoftMaskable.QueueSubtree(transform);
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkMaskPropertiesDirty();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            MarkTopologyChanged();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            MarkTopologyChanged();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            MarkMaskPropertiesDirty();
        }

        void OnTransformChildrenChanged() => SoftMaskable.QueueChildren(transform);

        void LoadShaders()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
            if (embeddedTMPShader == null)
                embeddedTMPShader = Shader.Find(TMPShaderName);
            if (embeddedTMPMobileShader == null)
                embeddedTMPMobileShader = Shader.Find(TMPMobileShaderName);
            if (embeddedTMPBitmapShader == null)
                embeddedTMPBitmapShader = Shader.Find(TMPBitmapShaderName);
        }

        void RegisterGraphicCallbacks()
        {
            if (maskGraphic == null)
                return;

            dirtyCallback ??= MarkMaskPropertiesDirty;
            maskGraphic.RegisterDirtyMaterialCallback(dirtyCallback);
            maskGraphic.RegisterDirtyVerticesCallback(dirtyCallback);
        }

        void UnregisterGraphicCallbacks()
        {
            if (maskGraphic == null || dirtyCallback == null)
                return;

            maskGraphic.UnregisterDirtyMaterialCallback(dirtyCallback);
            maskGraphic.UnregisterDirtyVerticesCallback(dirtyCallback);
        }

        static void MarkTopologyChanged()
        {
            unchecked { TopologyVersion++; }
            if (TopologyVersion == 0)
                TopologyVersion = 1;
        }

        void MarkMaskPropertiesDirty()
        {
            unchecked { dataVersion++; }
        }

        void RefreshDataVersion()
        {
            if (rectTransform == null || maskGraphic == null)
                return;

            Rect currentRect = maskGraphic.GetPixelAdjustedRect();
            Matrix4x4 currentWorldToLocal = rectTransform.worldToLocalMatrix;
            Texture currentMaskTexture = maskGraphic.mainTexture;

            if (hasTransformSnapshot && currentRect == lastRect && currentWorldToLocal == lastWorldToLocal
                && currentMaskTexture == lastMaskTexture && invertMask == lastInvertMask)
                return;

            hasTransformSnapshot = true;
            lastRect = currentRect;
            lastWorldToLocal = currentWorldToLocal;
            lastMaskTexture = currentMaskTexture;
            lastInvertMask = invertMask;

            unchecked { dataVersion++; }
        }

        void RebuildMaskStack()
        {
            stackTopologyVersion = TopologyVersion;

            int count = 0;
            bool changed = false;
            bool exceededLimit = false;

            for (Transform current = transform; current != null; current = current.parent)
            {
                if (!current.TryGetComponent(out SoftMask mask) || !mask.isActiveAndEnabled)
                    continue;

                if (count < MaxMaskDepth)
                {
                    changed |= maskStack[count] != mask;
                    maskStack[count++] = mask;
                }
                else { exceededLimit = true; }
            }

            changed |= maskCount != count;
            maskCount = count;
            for (int i = count; i < maskStack.Length; i++)
                maskStack[i] = null;

            if (changed)
                unchecked { stackVersion++; }

            if (!exceededLimit)
            {
                didWarnMaskLimit = false;
                return;
            }

            if (!didWarnMaskLimit)
            {
                didWarnMaskLimit = true;
                Debug.LogWarning($"Soft Mask supports up to {MaxMaskDepth} nested masks. The nearest {MaxMaskDepth} masks will be used.", this);
            }
        }

        /// <summary>
        /// Applies hierarchy changes under this mask immediately instead of before the next canvas render.
        /// Only needed when reading rendered materials in the same frame as the change.
        /// </summary>
        public void Refresh()
        {
            MarkTopologyChanged();
            MarkMaskPropertiesDirty();
            SoftMaskable.QueueSubtree(transform);
            SoftMaskable.ProcessPending();
        }

        int GetMaterialDataVersion()
        {
            if (stackTopologyVersion != TopologyVersion || maskCount == 0 || maskStack[0] != this)
                RebuildMaskStack();

            int currentVersion = stackVersion;
            for (int i = 0; i < maskCount; i++)
            {
                SoftMask mask = maskStack[i];
                if (mask == null)
                {
                    // Destroyed without a topology change (e.g. while its object was inactive)
                    RebuildMaskStack();
                    return GetMaterialDataVersion();
                }

                mask.RefreshDataVersion();
                unchecked { currentVersion = currentVersion * 397 ^ mask.dataVersion; }
            }

            return currentVersion;
        }

        void SetMaterialProperties(MaterialEntry entry, bool force, int currentVersion)
        {
            if (entry == null || entry.ModifiedMaterial == null)
                return;

            Matrix4x4 canvasToWorld = entry.Canvas != null ? entry.Canvas.transform.localToWorldMatrix : Matrix4x4.identity;
            if (!force && entry.AppliedVersion == currentVersion && entry.HasCanvasSnapshot && entry.CanvasToWorld == canvasToWorld)
                return;

            for (int i = 0; i < maskCount; i++)
                maskStack[i].WriteMaterialData(i, canvasToWorld, entry.ModifiedMaterial);
            for (int i = maskCount; i < MaxMaskDepth; i++)
                entry.ModifiedMaterial.SetTexture(PropsTextures[i], Texture2D.whiteTexture);

            entry.ModifiedMaterial.SetFloat(PropsCount, maskCount);
            entry.ModifiedMaterial.SetVectorArray(PropsCanvasToLocalX, CanvasToLocalX);
            entry.ModifiedMaterial.SetVectorArray(PropsCanvasToLocalY, CanvasToLocalY);
            entry.ModifiedMaterial.SetVectorArray(PropsRect, MaskRects);
            entry.ModifiedMaterial.SetVectorArray(PropsData, MaskData);
            entry.ModifiedMaterial.SetVector(PropsInvert, MaskInvert);
            entry.ModifiedMaterial.SetVectorArray(PropsPRRect, ProceduralRects);
            entry.ModifiedMaterial.SetVectorArray(PropsPRRadii, ProceduralRadii);
            entry.ModifiedMaterial.SetVectorArray(PropsPRFillData, ProceduralFillData);
            entry.ModifiedMaterial.SetVectorArray(PropsBorderData, BorderData);
            entry.ModifiedMaterial.SetVectorArray(PropsUVOuter, UVOuter);
            entry.ModifiedMaterial.SetVectorArray(PropsUVInner, UVInner);

            // The basic evaluator reads one mask at a constant index, which mobile GPUs keep in registers;
            // nested masks take the full evaluator's loop
            bool basic = maskCount <= 1 && (maskCount == 0 || maskStack[0].maskProperties.Basic);

            // A source change copies properties again, so the keyword is reapplied then too
            int maskKeyword = basic ? 1 : 2;
            if (force || entry.MaskKeyword != maskKeyword)
            {
                entry.ModifiedMaterial.EnableKeyword(basic ? BasicSoftMaskKeyword : SoftMaskKeyword);
                entry.ModifiedMaterial.DisableKeyword(basic ? SoftMaskKeyword : BasicSoftMaskKeyword);
                entry.MaskKeyword = maskKeyword;
            }

            entry.AppliedVersion = currentVersion;
            entry.CanvasToWorld = canvasToWorld;
            entry.HasCanvasSnapshot = true;
        }

        void WriteMaterialData(int index, Matrix4x4 canvasToWorld, Material material)
        {
            if (maskGraphic == null || rectTransform == null)
                return;
            if (!hasMaskProperties || maskProperties.Version != dataVersion)
                RefreshMaterialData();

            Matrix4x4 canvasToLocal = maskProperties.WorldToLocal * canvasToWorld;
            CanvasToLocalX[index] = canvasToLocal.GetRow(0);
            CanvasToLocalY[index] = canvasToLocal.GetRow(1);
            MaskRects[index] = maskProperties.Rect;
            MaskData[index] = maskProperties.Data;
            MaskInvert[index] = invertMask ? 1f : 0f;
            ProceduralRects[index] = maskProperties.ProceduralRect;
            ProceduralRadii[index] = maskProperties.ProceduralRadii;
            ProceduralFillData[index] = maskProperties.ProceduralFillData;
            BorderData[index] = maskProperties.BorderData;
            UVOuter[index] = maskProperties.UVOuter;
            UVInner[index] = maskProperties.UVInner;
            material.SetTexture(PropsTextures[index], maskProperties.Texture);
        }

        void RefreshMaterialData()
        {
            Rect rect = rectTransform.rect;
            float mode = 0f;
            float modeDataY = 0f;
            float modeDataZ = 0f;
            float modeDataW = 0f;
            Texture maskTexture = lastMaskTexture != null ? lastMaskTexture : Texture2D.whiteTexture;
            Vector4 uvOuter = new(0f, 0f, 1f, 1f);
            Vector4 uvInner = uvOuter;
            Vector4 borderData = Vector4.zero;
            Vector4 proceduralRect = Vector4.zero;
            Vector4 proceduralRadii = Vector4.zero;
            Vector4 proceduralFillData = Vector4.zero;
            Matrix4x4 worldToLocal = lastWorldToLocal;
            // Decided by configuration only: an animated clip or fill amount never switches shader variants
            bool basic = true;

            if (maskGraphic is ProceduralShape proceduralMask)
            {
                mode = 2f;
                basic = proceduralMask.ShaderShapeID <= (int)ProceduralShape.ShapeType.Ellipse
                    && ((int)proceduralMask.GetPackedClipConfig() & 7) == 0;
                rect = proceduralMask.GetDrawingRect();
                Vector2 halfSize = rect.size * 0.5f;
                proceduralRect = new Vector4(rect.center.x, rect.center.y, halfSize.x, halfSize.y);
                proceduralRadii = proceduralMask.GetShaderRadii(rect);
                modeDataY = Mathf.Max(0f, proceduralMask.Softness);
                modeDataZ = Mathf.Max(0f, proceduralMask.OutlineWidth);
                proceduralFillData = new Vector4(Mathf.Clamp01(proceduralMask.ClipAmount), proceduralMask.GetPackedClipConfig(),
                    proceduralMask.ShaderShapeID, proceduralMask.UsesSquircleCorners ? 1f : 0f);

                if (proceduralMask.HasShapeTransform)
                    worldToLocal = proceduralMask.GetShapeTransform(rect.center, true) * worldToLocal;

                maskTexture = Texture2D.whiteTexture;
            }
            else if (maskGraphic is Image image)
            {
                Sprite sprite = image.overrideSprite;
                rect = lastRect;
                if (sprite != null)
                {
                    uvOuter = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);

                    if (image.type == Image.Type.Simple) { rect = GetImageDrawingRect(image, sprite, rect, image.preserveAspect); }
                    else if (image.type == Image.Type.Sliced)
                    {
                        if (sprite.border.sqrMagnitude > 0f)
                        {
                            mode = 1f;
                            modeDataY = image.fillCenter ? 1f : 0f;
                            GetSlicedImageData(image, sprite, ref rect, out borderData);
                            uvInner = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
                        }
                        else
                        {
                            rect = GetImageDrawingRect(image, sprite, rect, false);
                        }
                    }
                    else if (image.type == Image.Type.Filled)
                    {
                        mode = 3f;
                        basic = false;
                        rect = GetImageDrawingRect(image, sprite, rect, image.preserveAspect);
                        float packedFill = (int)image.fillMethod + Mathf.Clamp(image.fillOrigin, 0, 3) * 8 + (image.fillClockwise ? 64 : 0);
                        proceduralFillData = new Vector4(Mathf.Clamp01(image.fillAmount), packedFill, 0f, 0f);
                    }
                    else if (image.type == Image.Type.Tiled)
                    {
                        mode = 4f;
                        modeDataY = image.fillCenter ? 1f : 0f;
                        GetTiledImageData(image, sprite, rect, out borderData, out modeDataZ, out modeDataW);
                        uvInner = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
                    }
                }
            }
            else if (maskGraphic is RawImage rawImage)
            {
                rect = lastRect;
                Rect uvRect = rawImage.uvRect;
                uvOuter = new Vector4(uvRect.xMin, uvRect.yMin, uvRect.xMax, uvRect.yMax);
                uvInner = uvOuter;
            }

            maskProperties.WorldToLocal = worldToLocal;
            maskProperties.Texture = maskTexture;
            maskProperties.Rect = new Vector4(rect.xMin, rect.yMin, rect.width, rect.height);
            maskProperties.Data = new Vector4(mode, modeDataY, modeDataZ, modeDataW);
            maskProperties.ProceduralRect = proceduralRect;
            maskProperties.ProceduralRadii = proceduralRadii;
            maskProperties.ProceduralFillData = proceduralFillData;
            maskProperties.BorderData = borderData;
            maskProperties.UVOuter = uvOuter;
            maskProperties.UVInner = uvInner;
            maskProperties.Basic = basic;
            maskProperties.Version = dataVersion;
            hasMaskProperties = true;
        }

        static Rect GetImageDrawingRect(Image image, Sprite sprite, Rect rect, bool preserveAspect)
        {
            Vector2 size = sprite.rect.size;
            if (preserveAspect && size.x > 0f && size.y > 0f && rect.width > 0f && rect.height > 0f)
            {
                float spriteRatio = size.x / size.y;
                float rectRatio = rect.width / rect.height;

                if (spriteRatio > rectRatio)
                {
                    float oldHeight = rect.height;
                    rect.height = rect.width / spriteRatio;
                    rect.y += (oldHeight - rect.height) * image.rectTransform.pivot.y;
                }
                else
                {
                    float oldWidth = rect.width;
                    rect.width = rect.height * spriteRatio;
                    rect.x += (oldWidth - rect.width) * image.rectTransform.pivot.x;
                }
            }

            int spriteWidth = Mathf.RoundToInt(size.x);
            int spriteHeight = Mathf.RoundToInt(size.y);
            if (spriteWidth <= 0 || spriteHeight <= 0)
                return rect;

            Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
            float xMin = padding.x / spriteWidth;
            float yMin = padding.y / spriteHeight;
            float xMax = (spriteWidth - padding.z) / spriteWidth;
            float yMax = (spriteHeight - padding.w) / spriteHeight;
            return Rect.MinMaxRect(rect.x + rect.width * xMin, rect.y + rect.height * yMin, rect.x + rect.width * xMax, rect.y + rect.height * yMax);
        }

        static Vector4 GetAdjustedBorders(RectTransform target, Vector4 border, Rect adjustedRect)
        {
            Rect originalRect = target.rect;
            for (int axis = 0; axis <= 1; axis++)
            {
                if (originalRect.size[axis] != 0f)
                {
                    float borderScaleRatio = adjustedRect.size[axis] / originalRect.size[axis];
                    border[axis] *= borderScaleRatio;
                    border[axis + 2] *= borderScaleRatio;
                }

                float combinedBorders = border[axis] + border[axis + 2];
                if (adjustedRect.size[axis] < combinedBorders && combinedBorders != 0f)
                {
                    float borderScaleRatio = adjustedRect.size[axis] / combinedBorders;
                    border[axis] *= borderScaleRatio;
                    border[axis + 2] *= borderScaleRatio;
                }
            }
            return border;
        }

        static void GetSlicedImageData(Image image, Sprite sprite, ref Rect rect, out Vector4 borderData)
        {
            float pixelsPerUnit = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 1f;

            Vector4 border = GetAdjustedBorders(image.rectTransform, sprite.border / pixelsPerUnit, rect);
            Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite) / pixelsPerUnit;
            float paddedWidth = Mathf.Max(0f, rect.width - padding.x - padding.z);
            float paddedHeight = Mathf.Max(0f, rect.height - padding.y - padding.w);

            borderData = new Vector4(Mathf.Clamp(border.x - padding.x, 0f, paddedWidth), Mathf.Clamp(border.y - padding.y, 0f, paddedHeight),
                Mathf.Clamp(rect.width - border.z - padding.x, 0f, paddedWidth), Mathf.Clamp(rect.height - border.w - padding.y, 0f, paddedHeight));
            rect = new Rect(rect.x + padding.x, rect.y + padding.y, paddedWidth, paddedHeight);
        }

        static void GetTiledImageData(Image image, Sprite sprite, Rect rect, out Vector4 borderData, out float tileWidth, out float tileHeight)
        {
            float pixelsPerUnit = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 1f;

            Vector4 spriteBorder = sprite.border;
            Vector4 border = GetAdjustedBorders(image.rectTransform, spriteBorder / pixelsPerUnit, rect);
            borderData = new Vector4(border.x, border.y, rect.width - border.z, rect.height - border.w);
            tileWidth = (sprite.rect.width - spriteBorder.x - spriteBorder.z) / pixelsPerUnit;
            tileHeight = (sprite.rect.height - spriteBorder.y - spriteBorder.w) / pixelsPerUnit;

            if (tileWidth <= 0f)
                tileWidth = Mathf.Max(borderData.z - borderData.x, 0.001f);
            if (tileHeight <= 0f)
                tileHeight = Mathf.Max(borderData.w - borderData.y, 0.001f);
        }

        internal static void UpdateActiveMasks()
        {
            for (int i = 0; i < activeMasks.Count; i++)
                activeMasks[i].UpdateCachedMaterials();
        }

        internal static void QueueActiveMasks()
        {
            for (int i = 0; i < activeMasks.Count; i++)
                SoftMaskable.QueueSubtree(activeMasks[i].transform);
        }

        void UpdateCachedMaterials()
        {
            if (materialCache.Count == 0 || maskGraphic == null)
                return;

            staleMaterialKeys.Clear();
            int currentVersion = GetMaterialDataVersion();

            foreach (KeyValuePair<MaterialKey, MaterialEntry> pair in materialCache)
            {
                MaterialEntry entry = pair.Value;
                if (entry.Users.Count == 0 && !entry.KeepAlive)
                {
                    // Users switch materials on their next rebuild. Keep the old one until then.
                    if (++entry.UnusedUpdates > 2)
                        staleMaterialKeys.Add(pair.Key);

                    continue;
                }

                entry.UnusedUpdates = 0;
                if (entry.SourceMaterial == null || entry.ModifiedMaterial == null || (entry.HasCanvas && entry.Canvas == null) || !entry.HasValidShaders())
                {
                    staleMaterialKeys.Add(pair.Key);
                    continue;
                }

                SyncMaterial(entry, false, currentVersion);
            }

            for (int i = 0; i < staleMaterialKeys.Count; i++)
            {
                MaterialKey key = staleMaterialKeys[i];
                if (!materialCache.TryGetValue(key, out MaterialEntry entry))
                    continue;

                materialCache.Remove(key);
                entry.ReleaseUsers();
                DestroyMaterial(entry.ModifiedMaterial);
            }
        }

        public void ModifyMesh(VertexHelper vertexHelper)
        {
            if (isActiveAndEnabled && !showMaskGraphic)
                vertexHelper.Clear();
        }

        public void ModifyMesh(Mesh mesh)
        {
            if (isActiveAndEnabled && !showMaskGraphic)
                mesh.Clear();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled)
                return true;
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            bool contains = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, eventCamera);
            return invertMask ? !contains : contains;
        }

        void RefreshMaskGraphic()
        {
            if (maskGraphic == null)
                return;

            maskGraphic.SetVerticesDirty();

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        void ClearCache(bool notifyUsers)
        {
            foreach (KeyValuePair<MaterialKey, MaterialEntry> pair in materialCache)
            {
                if (notifyUsers) { pair.Value.ReleaseUsers(); }
                else { pair.Value.Users.Clear(); }

                DestroyMaterial(pair.Value.ModifiedMaterial);
            }

            materialCache.Clear();
        }

        void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Cache invalidation can happen during a Canvas rebuild or OnValidate
                if (materialsToDestroy.Count == 0)
                    UnityEditor.EditorApplication.update += DestroyQueuedMaterials;

                materialsToDestroy.Add(material);
                return;
            }
#endif
            Destroy(material);
        }

        static bool IsMaterialCompatible(Material material)
        {
            if (material == null || !material.HasProperty(PropsSupport) || !material.HasProperty(PropsCount))
                return false;

            for (int i = 0; i < PropsTextures.Length; i++)
            {
                if (!material.HasTexture(PropsTextures[i]))
                    return false;
            }

            return true;
        }

        Shader GetReplacementShader(Material baseMaterial)
        {
            if (baseMaterial == null || baseMaterial.shader == null)
                return null;

            string shaderName = baseMaterial.shader.name;
            if (shaderName == "UI/Default" || shaderName == "UI/DefaultETC1" || shaderName == ShaderName)
                return embeddedShader;

            if (shaderName == TMPShaderName)
                return embeddedTMPShader;
            if (shaderName == TMPMobileShaderName)
                return embeddedTMPMobileShader;
            if (shaderName == TMPBitmapShaderName)
                return embeddedTMPBitmapShader;

            if (shaderName.StartsWith("TextMeshPro/", StringComparison.Ordinal))
            {
                if (shaderName.IndexOf("Sprite", StringComparison.Ordinal) >= 0 || shaderName.IndexOf("Bitmap", StringComparison.Ordinal) >= 0)
                    return embeddedTMPBitmapShader;

                return shaderName.IndexOf("Mobile", StringComparison.Ordinal) >= 0 ? embeddedTMPMobileShader : embeddedTMPShader;
            }

            if (baseMaterial.HasProperty(PropsSupport) && baseMaterial.GetFloat(PropsSupport) > 0.5f)
                return baseMaterial.shader;

            return null;
        }

        /// <summary>
        /// Returns this mask's material for a child Graphic. The material lives until the mask is disabled.
        /// </summary>
        public Material GetModifiedMaterialForChild(Material baseMaterial, Canvas targetCanvas)
        {
            MaterialEntry entry = GetMaterialEntry(baseMaterial, targetCanvas);
            if (entry == null)
                return baseMaterial;

            entry.KeepAlive = true;
            return entry.ModifiedMaterial;
        }

        internal MaterialEntry GetMaterialEntry(Material baseMaterial, Canvas targetCanvas)
        {
            if (!isActiveAndEnabled || maskGraphic == null || baseMaterial == null)
                return null;
            if (targetCanvas != null)
                targetCanvas = targetCanvas.rootCanvas;

            MaterialKey key = new(baseMaterial, targetCanvas);
            if (materialCache.TryGetValue(key, out MaterialEntry entry))
            {
                if (entry.ModifiedMaterial != null && entry.HasValidShaders())
                {
                    SyncMaterial(entry, false, GetMaterialDataVersion());
                    return entry;
                }

                materialCache.Remove(key);
                entry.ReleaseUsers();
                DestroyMaterial(entry.ModifiedMaterial);
            }

            Shader replacementShader = GetReplacementShader(baseMaterial);
            if (replacementShader == null || (replacementShader == baseMaterial.shader && !IsMaterialCompatible(baseMaterial)))
            {
                WarnUnsupportedShader(baseMaterial.shader);
                return null;
            }

            Material modifiedMaterial = new(replacementShader)
            {
                name = $"{baseMaterial.name} (Evo Soft Mask)",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!IsMaterialCompatible(modifiedMaterial))
            {
                WarnUnsupportedShader(baseMaterial.shader);
                DestroyMaterial(modifiedMaterial);
                return null;
            }

            entry = new MaterialEntry(baseMaterial, modifiedMaterial, targetCanvas);
            materialCache[key] = entry;
            SyncMaterial(entry, true, GetMaterialDataVersion());
            return entry;
        }

        void WarnUnsupportedShader(Shader shader)
        {
            if (shader != null && unsupportedShaders.Add(shader))
                Debug.LogWarning($"Soft Mask cannot mask shader '{shader.name}'. Use a supported UI shader or implement " +
                    $"the SoftMask.cginc properties and alpha application in the custom shader.", this);
        }

        void SyncMaterial(MaterialEntry entry, bool force, int currentVersion)
        {
            int sourceCRC = entry.SourceMaterial.ComputeCRC();
            bool sourceChanged = force || !entry.HasSourceSnapshot || entry.SourceCRC != sourceCRC;
            if (sourceChanged)
            {
                entry.ModifiedMaterial.CopyMatchingPropertiesFromMaterial(entry.SourceMaterial);

                if (entry.UsesExternalAlpha)
                {
                    entry.ModifiedMaterial.EnableKeyword("ETC1_EXTERNAL_ALPHA");
                    entry.ModifiedMaterial.SetFloat(PropsExternalAlpha, 1f);
                }

                entry.ModifiedMaterial.DisableKeyword(TMPBitmapMobileKeyword);
                entry.ModifiedMaterial.DisableKeyword(TMPSpriteKeyword);

                if (entry.TMPBitmapMode == 1)
                    entry.ModifiedMaterial.EnableKeyword(TMPBitmapMobileKeyword);
                else if (entry.TMPBitmapMode == 2)
                    entry.ModifiedMaterial.EnableKeyword(TMPSpriteKeyword);

                entry.SourceCRC = sourceCRC;
                entry.HasSourceSnapshot = true;
            }

            // A Procedural Shape page or backdrop copy can switch its shader program without a property change. The source
            // can be a stencil copy made before that switch, so the shape drawing with the copy names the program.
            // A disabled shape stays a user until its next material update, so the first drawing one is used.
            Material programSource = null;
            for (int i = 0; i < entry.Users.Count && programSource == null; i++)
            {
                if (entry.Users[i] != null && entry.Users[i].Graphic is ProceduralShape shape && shape != null && shape.isActiveAndEnabled)
                    programSource = shape.ProgramMaterial;
            }

            ProceduralShape.SyncShaderProgram(programSource != null ? programSource : entry.SourceMaterial, entry.ModifiedMaterial);
            SetMaterialProperties(entry, sourceChanged, currentVersion);
        }

        struct MaskProperties
        {
            public int Version;
            public Matrix4x4 WorldToLocal;
            public Texture Texture;
            public Vector4 Rect;
            public Vector4 Data;
            public Vector4 ProceduralRect;
            public Vector4 ProceduralRadii;
            public Vector4 ProceduralFillData;
            public Vector4 BorderData;
            public Vector4 UVOuter;
            public Vector4 UVInner;
            public bool Basic;
        }

        readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            readonly Material material;
            readonly Canvas canvas;
            readonly int hashCode;

            public MaterialKey(Material material, Canvas canvas)
            {
                this.material = material;
                this.canvas = canvas;

                int materialId = material != null ? material.GetHashCode() : 0;
                int canvasId = canvas != null ? canvas.GetHashCode() : 0;
                hashCode = materialId * 397 ^ canvasId;
            }

            public bool Equals(MaterialKey other) => ReferenceEquals(material, other.material) && ReferenceEquals(canvas, other.canvas);

            public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);

            public override int GetHashCode() => hashCode;
        }

        internal sealed class MaterialEntry
        {
            public readonly Material SourceMaterial;
            public readonly Material ModifiedMaterial;
            public readonly Shader SourceShader;
            public readonly Canvas Canvas;
            public readonly int TMPBitmapMode;
            public readonly bool HasCanvas;
            public readonly bool UsesExternalAlpha;
            public readonly List<SoftMaskable> Users = new();
            Shader modifiedShader;
            public bool KeepAlive;
            public int UnusedUpdates;
            public int AppliedVersion = int.MinValue;
            public int MaskKeyword; // 1 basic, 2 full, 0 not applied yet
            public int SourceCRC;
            public bool HasSourceSnapshot;
            public bool HasCanvasSnapshot;
            public Matrix4x4 CanvasToWorld;

            public MaterialEntry(Material sourceMaterial, Material modifiedMaterial, Canvas canvas)
            {
                SourceMaterial = sourceMaterial;
                ModifiedMaterial = modifiedMaterial;
                SourceShader = sourceMaterial.shader;
                Canvas = canvas;
                HasCanvas = canvas != null;
                modifiedShader = modifiedMaterial.shader;

                string shaderName = SourceShader != null ? SourceShader.name : string.Empty;
                UsesExternalAlpha = shaderName == "UI/DefaultETC1";
                if (shaderName.IndexOf("Sprite", StringComparison.Ordinal) >= 0)
                    TMPBitmapMode = 2;
                else if (shaderName.IndexOf("Bitmap", StringComparison.Ordinal) >= 0
                    && shaderName.IndexOf("Mobile", StringComparison.Ordinal) >= 0)
                    TMPBitmapMode = 1;
            }

            public void AddUser(SoftMaskable user)
            {
                user.EntryIndex = Users.Count;
                Users.Add(user);
            }

            public void RemoveUser(SoftMaskable user)
            {
                int index = user.EntryIndex;
                user.EntryIndex = -1;
                if (index < 0 || index >= Users.Count || Users[index] != user)
                    return;

                // Swap-remove keeps releases O(1) for materials shared by many graphics
                int last = Users.Count - 1;
                if (index != last)
                {
                    Users[index] = Users[last];
                    Users[index].EntryIndex = index;
                }

                Users.RemoveAt(last);
            }

            public void ReleaseUsers()
            {
                for (int i = 0; i < Users.Count; i++)
                {
                    if (Users[i] != null)
                        Users[i].OnMaskReleased();
                }

                Users.Clear();
            }

            public bool HasValidShaders()
            {
                if (SourceShader != SourceMaterial.shader)
                    return false;

#if UNITY_EDITOR
                // Shader reimports can change properties without replacing the Shader object.
                if (!Application.isPlaying && !IsMaterialCompatible(ModifiedMaterial))
                    return false;
#endif
                Shader currentShader = ModifiedMaterial.shader;
                if (modifiedShader == currentShader)
                    return true;
                if (!IsMaterialCompatible(ModifiedMaterial))
                    return false;

                modifiedShader = currentShader;
                HasSourceSnapshot = false;
                return true;
            }
        }

#if UNITY_EDITOR
        static readonly List<Material> materialsToDestroy = new();

        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= CleanupEditorMaterials;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupEditorMaterials;
            UnityEditor.EditorApplication.quitting -= CleanupEditorMaterials;
            UnityEditor.EditorApplication.quitting += CleanupEditorMaterials;
        }

        static void CleanupEditorMaterials()
        {
            for (int i = 0; i < activeMasks.Count; i++)
                activeMasks[i].ClearCache(false);

            DestroyQueuedMaterials();
        }

        static void DestroyQueuedMaterials()
        {
            UnityEditor.EditorApplication.update -= DestroyQueuedMaterials;
            for (int i = 0; i < materialsToDestroy.Count; i++)
            {
                if (materialsToDestroy[i] != null)
                    DestroyImmediate(materialsToDestroy[i]);
            }
            materialsToDestroy.Clear();
        }

        protected override void Reset()
        {
            base.Reset();
            rectTransform = transform as RectTransform;
            TryGetComponent(out maskGraphic);
            LoadShaders();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!isActiveAndEnabled)
                return;

            LoadShaders();
            MarkMaskPropertiesDirty();
            RefreshMaskGraphic();
        }
#endif
    }
}
