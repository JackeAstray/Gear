using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Animation/Animated Container"), HelpURL(Constants.HelpUrl + "animation/animated-container")]
    public class AnimatedContainer : MonoBehaviour
    {
        // Objects
        [SerializeField] private bool getChildObjects = true;
        public List<Transform> containerObjects = new();

        // Animation
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
        [Tooltip("Add a delay before the animation process.")]
        [Min(0f)] public float animationDelay = 0f;

        // Scale
        [Tooltip("Sets the object's initial animation scale.")]
        [Range(0f, 0.99f)] public float startScale = 0.5f;
        [Tooltip("How long the scaling animation lasts (seconds).")]
        [Min(0f)] public float scaleDuration = 0.25f;

        // Fade
        [Tooltip("Start fading when the eased scale progress reaches this value.")]
        [Range(0f, 0.99f)] public float fadeAfterScale = 0.5f;
        [Tooltip("How long the fade animation lasts (seconds).")]
        [Min(0f)] public float fadeDuration = 0.2f;

        // Settings
        public bool useUnscaledTime = false;
        public bool playOnEnable = true;
        [Tooltip("Play the animation only once.")]
        public bool playOnce = false;
        [Tooltip("Add a cooldown before skipping to the next active item.")]
        [Range(0f, 1f)] public float itemCooldown = 0.1f;

        // Constants
        const int TweenPropertyKey = 0x41434E54; // ACNT

        // Cache
        static readonly Action<AnimatedContainer, float> TweenUpdateCallback = ApplyTweenValue;
        readonly List<AnimatedObject> cachedObjects = new();
        Action completionCallback;
        TweenHandle currentAnimation;
        bool initialized;

        // Animation State
        AnimationCurve currentCurve;
        Vector3 initialScale;
        float currentScaleDuration;
        float currentFadeDuration;
        float currentFadeThreshold;

        // Properties
        public bool PlayedOnce { get; private set; }
        public bool IsAnimating => currentAnimation.IsActive;

        void Awake() => Initialize();

        void OnEnable()
        {
            if (playOnEnable)
                Animate();
        }

        void OnDisable() => StopAnimation();

        void OnDestroy()
        {
            StopAnimation();
            cachedObjects.Clear();
        }

        void Initialize()
        {
            if (initialized)
                return;

            completionCallback = StopAnimation;

            if (containerObjects != null)
            {
                for (int i = 0; i < containerObjects.Count; i++)
                    CacheObject(containerObjects[i]);
            }

            if (getChildObjects)
            {
                for (int i = 0; i < transform.childCount; i++)
                    CacheObject(transform.GetChild(i));
            }

            initialized = true;
        }

        void CacheObject(Transform child)
        {
            if (child == null)
                return;

            for (int i = 0; i < cachedObjects.Count; i++)
            {
                if (cachedObjects[i].Transform == child)
                    return;
            }

            if (!child.TryGetComponent<CanvasGroup>(out var cg))
                cg = child.gameObject.AddComponent<CanvasGroup>();

            cachedObjects.Add(new AnimatedObject { Transform = child, CanvasGroup = cg });
        }

        public void Animate()
        {
            if (!isActiveAndEnabled || (playOnce && PlayedOnce))
                return;

            Initialize();
            StopAnimation();

            currentCurve = animationCurve;
            initialScale = Vector3.one * Mathf.Clamp01(startScale);
            currentScaleDuration = Mathf.Max(0f, scaleDuration);
            currentFadeDuration = Mathf.Max(0f, fadeDuration);
            currentFadeThreshold = Mathf.Clamp01(fadeAfterScale);

            float delay = Mathf.Max(0f, animationDelay);
            float cooldown = Mathf.Max(0f, itemCooldown);
            float duration = 0f;
            bool hasObjects = false;

            for (int i = 0; i < cachedObjects.Count; i++)
            {
                AnimatedObject item = cachedObjects[i];
                if (item.Transform == null || item.CanvasGroup == null || !item.Transform.gameObject.activeInHierarchy)
                    continue;

                item.DefaultScale = item.Transform.localScale;
                item.DefaultAlpha = item.CanvasGroup.alpha;
                item.Interactable = item.CanvasGroup.interactable;
                item.BlocksRaycasts = item.CanvasGroup.blocksRaycasts;
                item.Delay = delay;
                item.FadeStartTime = currentFadeThreshold <= 0f ? 0f : -1f;
                item.IsAnimating = true;
                item.Transform.localScale = initialScale;
                item.CanvasGroup.alpha = 0f;
                item.CanvasGroup.interactable = false;
                item.CanvasGroup.blocksRaycasts = false;

                duration = delay + currentScaleDuration + currentFadeDuration;
                delay += cooldown;
                hasObjects = true;
            }

            if (!hasObjects)
                return;

            PlayedOnce = true;

            if (!ApplyAnimation(0f))
                return;

            currentAnimation = EvoTween.Create(0f, duration, duration)
                .WithEase(Ease.Linear)
                .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                .OnComplete(completionCallback)
                .Bind(this, TweenUpdateCallback, TweenPropertyKey);

            if (!currentAnimation.IsActive)
                StopAnimation();
        }

        static void ApplyTweenValue(AnimatedContainer container, float elapsed)
        {
            if (!container.ApplyAnimation(elapsed))
                container.StopAnimation();
        }

        bool ApplyAnimation(float elapsed)
        {
            bool hasObjects = false;

            for (int i = 0; i < cachedObjects.Count; i++)
            {
                AnimatedObject item = cachedObjects[i];
                if (!item.IsAnimating)
                    continue;

                if (item.Transform == null || item.CanvasGroup == null || !item.Transform.gameObject.activeInHierarchy)
                {
                    item.Restore();
                    continue;
                }

                float itemTime = elapsed - item.Delay;
                if (itemTime < 0f)
                {
                    hasObjects = true;
                    continue;
                }

                float scaleProgress = currentScaleDuration > 0f ? Mathf.Clamp01(itemTime / currentScaleDuration) : 1f;
                float curveValue = currentCurve != null && currentCurve.length > 0 ? currentCurve.Evaluate(scaleProgress) : scaleProgress;
                item.Transform.localScale = scaleProgress >= 1f ? item.DefaultScale : Vector3.LerpUnclamped(initialScale, item.DefaultScale, curveValue);

                // Always start the fade by the scale endpoint, even if the curve never reaches the threshold.
                if (item.FadeStartTime < 0f && (curveValue >= currentFadeThreshold || scaleProgress >= 1f))
                    item.FadeStartTime = Mathf.Min(itemTime, currentScaleDuration);

                float fadeProgress = 0f;
                if (item.FadeStartTime >= 0f)
                {
                    fadeProgress = currentFadeDuration > 0f ? Mathf.Clamp01((itemTime - item.FadeStartTime) / currentFadeDuration) : 1f;
                    item.CanvasGroup.alpha = item.DefaultAlpha * fadeProgress;
                    item.CanvasGroup.interactable = item.Interactable;
                    item.CanvasGroup.blocksRaycasts = item.BlocksRaycasts;
                }

                if (scaleProgress >= 1f && fadeProgress >= 1f) { item.Restore(); }
                else { hasObjects = true; }
            }

            return hasObjects;
        }

        void StopAnimation()
        {
            currentAnimation.Kill();
            currentAnimation = default;

            for (int i = 0; i < cachedObjects.Count; i++)
                cachedObjects[i].Restore();
        }

        sealed class AnimatedObject
        {
            public Transform Transform;
            public CanvasGroup CanvasGroup;
            public Vector3 DefaultScale;
            public float DefaultAlpha;
            public float Delay;
            public float FadeStartTime;
            public bool Interactable;
            public bool BlocksRaycasts;
            public bool IsAnimating;

            public void Restore()
            {
                if (!IsAnimating)
                    return;

                IsAnimating = false;

                if (Transform != null)
                    Transform.localScale = DefaultScale;

                if (CanvasGroup != null)
                {
                    CanvasGroup.alpha = DefaultAlpha;
                    CanvasGroup.interactable = Interactable;
                    CanvasGroup.blocksRaycasts = BlocksRaycasts;
                }
            }
        }
    }
}