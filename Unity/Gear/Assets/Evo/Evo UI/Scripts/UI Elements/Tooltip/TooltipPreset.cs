using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [HelpURL(Constants.HelpUrl + "ui-elements/tooltip")]
    [AddComponentMenu("Evo/UI/UI Elements/Tooltip Preset")]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class TooltipPreset : MonoBehaviour
    {
        // Core References
        public RectTransform tooltipRect;
        public CanvasGroup canvasGroup;

        // Content References
        [SerializeField] private Image iconImage;
        [SerializeField, TextComponent] private Graphic titleText;
        [SerializeField, TextComponent] private Graphic descriptionText;
        [SerializeField] private ContentSizeFitter contentSizeFitter;
        [SerializeField] private GameObject contentContainer;
        [SerializeField] private LayoutGroup layoutGroup;
        public LayoutElement layoutElement;

        // Animations
        public TweenSequence openAnimation = CreateAnimation(true);
        public TweenSequence closeAnimation = CreateAnimation(false);

        // Helpers
        bool isInitialized;
        bool isClosing;
        RectTransform runtimeRoot;

        // Playback
        TweenSequenceSampler openSampler;
        TweenSequenceSampler closeSampler;
        TweenSequenceSampler activeSampler;
        float animationTime;
        int animationStartFrame;

        // Pooling, oldest closed copy first
        static readonly List<TooltipPreset> pool = new();
        Tooltip owner;
        GameObject source;
        bool isPooled;

        internal bool IsClosing => isClosing;
        internal bool IsAddedAtRuntime { get; private set; }
        internal RectTransform RuntimeRoot => runtimeRoot;
        GameObject RootObject => runtimeRoot != null ? runtimeRoot.gameObject : gameObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pool.Clear();

        void Awake() => CheckForReferences();

        void LateUpdate()
        {
            if (activeSampler != null && Time.frameCount != animationStartFrame)
                AdvanceAnimation();

            // A closing tooltip stays where it is
            if (!isClosing && owner != null)
                owner.FollowTarget();
        }

        void OnDestroy()
        {
            if (isPooled)
                pool.Remove(this);

            // Hands the clip copies back to Evo Tween without writing to objects that are going away
            openSampler?.Release();
            closeSampler?.Release();
        }

        internal void SetRuntimeRoot(RectTransform root, GameObject prefab, bool addedAtRuntime)
        {
            runtimeRoot = root;
            source = prefab;
            IsAddedAtRuntime = addedAtRuntime;
        }

        /// <summary>
        /// Takes a closed copy of the prefab out of the pool. Returns null when none is available.
        /// </summary>
        internal static TooltipPreset Rent(GameObject prefab)
        {
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                TooltipPreset copy = pool[i];
                if (copy.source != prefab)
                    continue;

                pool.RemoveAt(i);
                copy.isPooled = false;
                return copy;
            }

            return null;
        }

        internal void Activate(Tooltip tooltip)
        {
            owner = tooltip;

            GameObject root = RootObject;
            if (!root.activeSelf)
                root.SetActive(true);
        }

        /// <summary>
        /// Destroys this copy instead of pooling it, for when its Tooltip is destroyed.
        /// </summary>
        internal void Discard()
        {
            isClosing = true;
            activeSampler = null;
            owner = null;
            Destroy(RootObject);
        }

        static TweenSequence CreateAnimation(bool open)
        {
            TweenSequence sequence = new() { TimeMode = TimeMode.Unscaled };

            TweenClip fade = new();
            fade.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = !open, from = 0f, to = open ? 1f : 0f };
            fade.Settings.Duration = 0.1f;
            fade.Settings.Ease = Ease.InOutSine;

            TweenClip slide = new();
            slide.Settings.Module = new RectTransformSlideModule
            {
                fromCurrent = !open,
                from = open ? new Vector2(0f, -20f) : Vector2.zero,
                to = open ? Vector2.zero : new Vector2(0f, -20f)
            };
            slide.Settings.Duration = open ? 0.15f : 0.1f;
            slide.Settings.Ease = open ? Ease.OutCubic : Ease.InCubic;

            sequence.InlineClips.Add(fade);
            sequence.InlineClips.Add(slide);
            return sequence;
        }

        public void Setup(string title, string description, Sprite icon, float maxWidth, bool isCustom = false)
        {
            CheckForReferences();

            if (isCustom)
                return;

            // Check if header should be active, and re-enable it if it was previously disabled
            bool hasHeader = !string.IsNullOrEmpty(title) || icon != null;

            if (titleText != null && titleText.transform.parent != null)
            {
                GameObject headerObj = titleText.transform.parent.gameObject;
                if (headerObj.activeSelf != hasHeader)
                    headerObj.SetActive(hasHeader);
            }

            if (hasHeader)
            {
                SetIcon(icon);
                SetTitle(title);
            }

            SetDescription(description);
            ForceLayoutUpdate(maxWidth);
        }

        void CheckForReferences()
        {
            if (isInitialized)
                return;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (tooltipRect == null)
                tooltipRect = GetComponent<RectTransform>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            isInitialized = true;
        }

        void SetTitle(string title)
        {
            if (titleText == null)
                return;

            TextProvider.SetText(titleText, title);

            // Check if state is changed
            bool hasTitle = !string.IsNullOrEmpty(title);
            if (titleText.gameObject.activeSelf != hasTitle)
                titleText.gameObject.SetActive(hasTitle);
        }

        void SetDescription(string description)
        {
            if (descriptionText == null)
                return;

            TextProvider.SetText(descriptionText, description);

            // Check if state is changed
            bool hasDesc = !string.IsNullOrEmpty(description);
            if (descriptionText.gameObject.activeSelf != hasDesc)
                descriptionText.gameObject.SetActive(hasDesc);
        }

        void SetIcon(Sprite icon)
        {
            if (iconImage == null)
                return;

            // Prevent material dirtying if sprite hasn't changed
            if (iconImage.sprite != icon)
                iconImage.sprite = icon;

            // Check if state is changed
            bool hasIcon = icon != null;
            if (iconImage.gameObject.activeSelf != hasIcon)
                iconImage.gameObject.SetActive(hasIcon);
        }

        void ForceLayoutUpdate(float maxWidth)
        {
            // Measure the unlimited width first. A reused or updated tooltip may still have the last text's limit on.
            if (layoutElement != null && layoutElement.enabled)
                layoutElement.enabled = false;

            if (contentSizeFitter != null)
            {
                contentSizeFitter.SetLayoutHorizontal();
                contentSizeFitter.SetLayoutVertical();
            }

            if (layoutGroup != null)
            {
                layoutGroup.SetLayoutHorizontal();
                layoutGroup.SetLayoutVertical();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

            if (layoutElement != null)
            {
                if (layoutElement.preferredWidth != maxWidth)
                    layoutElement.preferredWidth = maxWidth;

                // If dynamically constrained the width, must rebuild the layout one more time.
                // Otherwise, the tooltip background won't stretch vertically to fit the newly wrapped text.
                if (GetResolvedSize().x > maxWidth)
                {
                    layoutElement.enabled = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
                }
            }
        }

        void Play(TweenSequenceSampler sampler)
        {
            // Capturing records the values the pool restores later, then samples the first frame
            sampler.Capture();
            animationTime = 0f;
            animationStartFrame = Time.frameCount;
            activeSampler = sampler.HasCapturedValues && sampler.Duration > 0f ? sampler : null;
        }

        void AdvanceAnimation()
        {
            animationTime += activeSampler.Sequence.TimeMode == TimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            activeSampler.Sample(animationTime);

            if (animationTime < activeSampler.Duration)
                return;

            activeSampler = null;

            if (isClosing)
                ReturnToPool();
        }

        void ReturnToPool()
        {
            activeSampler = null;
            isClosing = false;

            if (owner != null)
            {
                owner.ReleaseInstance(this);
                owner = null;
            }

            // An inactive hierarchy may be in the middle of being disabled or destroyed, so it isn't reused
            GameObject root = RootObject;
            int maxSize = Tooltip.MaxPoolSize;

            if (source == null || maxSize <= 0 || !root.activeInHierarchy)
            {
                Destroy(root);
                return;
            }

            // Undo what the animations changed, so the next open starts like a new copy
            closeSampler?.Restore();
            openSampler?.Restore();
            root.SetActive(false);

            // A full pool drops the copy that has been closed the longest
            while (pool.Count >= maxSize)
            {
                TooltipPreset oldest = pool[0];
                pool.RemoveAt(0);
                oldest.isPooled = false;
                Destroy(oldest.RootObject);
            }

            pool.Add(this);
            isPooled = true;
        }

        public Vector2 GetResolvedSize()
        {
            CheckForReferences();

            if (tooltipRect == null)
                return Vector2.zero;

            Vector2 size = tooltipRect.rect.size;

            if (size.x <= 0f)
                size.x = LayoutUtility.GetPreferredWidth(tooltipRect);
            if (size.y <= 0f)
                size.y = LayoutUtility.GetPreferredHeight(tooltipRect);
            if (size.x <= 0f || size.y <= 0f)
            {
                Vector3 boundsSize = RectTransformUtility.CalculateRelativeRectTransformBounds(tooltipRect).size;
                if (size.x <= 0f)
                    size.x = boundsSize.x;
                if (size.y <= 0f)
                    size.y = boundsSize.y;
            }

            return Vector2.Max(size, Vector2.zero);
        }

        public void Open()
        {
            if (isClosing)
                return;

            CheckForReferences();
            openSampler ??= new TweenSequenceSampler(gameObject, openAnimation);
            openSampler.SetSource(gameObject, openAnimation);

            // Opening again starts from the values captured before the previous open
            openSampler.Restore();
            Play(openSampler);
        }

        public void Close()
        {
            if (isClosing)
                return;

            isClosing = true;
            closeSampler ??= new TweenSequenceSampler(gameObject, closeAnimation);
            closeSampler.SetSource(gameObject, closeAnimation);
            Play(closeSampler);

            if (activeSampler == null)
                ReturnToPool();
        }

        public void CloseImmediate()
        {
            isClosing = true;
            ReturnToPool();
        }

    }
}