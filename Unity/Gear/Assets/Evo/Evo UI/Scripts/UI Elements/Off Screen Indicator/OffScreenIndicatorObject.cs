using System;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [AddComponentMenu("Evo/UI/UI Elements/Off Screen Indicator Object"), HelpURL(Constants.HelpUrl)]
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(CanvasGroup)), NoAutoStaticsCleanup]
    public class OffScreenIndicatorObject : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform arrowObject;
        [SerializeField] private Image iconObject;
        [SerializeField, TextComponent] private Graphic distanceText;

        // Cache
        RectTransform indicatorUI;
        CanvasGroup canvasGroup;
        TweenHandle fadeTween;
        Action fadeCompleteCallback;
        float visibilityAlpha = 1f;
        char[] distanceBuffer = new char[64];
        int distanceLength = -1;
        string lastDistanceText;

        // Constants
        const int FadePropertyKey = 0x4F534946; // OSIF
        static readonly Action<OffScreenIndicatorObject, float> FadeUpdateCallback = ApplyFadeAlpha;

        // Properties
        public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;
        public float VisibilityAlpha
        {
            get => visibilityAlpha;
            set => visibilityAlpha = Mathf.Clamp01(value);
        }

        void Awake() => Initialize();

        void OnDisable() => StopFade();

        public void Initialize()
        {
            if (indicatorUI != null && canvasGroup != null)
                return;

            indicatorUI = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            fadeCompleteCallback = CompleteFade;
        }

        void StartFade(float targetAlpha, float duration)
        {
            Initialize();
            StopFade();

            if (!isActiveAndEnabled || canvasGroup == null)
                return;

            if (duration <= 0f || canvasGroup.alpha == targetAlpha)
            {
                ApplyFadeAlpha(this, targetAlpha);
                CompleteFade();
                return;
            }

            fadeTween = EvoTween.Create(canvasGroup.alpha, targetAlpha, duration)
                .OnComplete(fadeCompleteCallback)
                .Bind(this, FadeUpdateCallback, FadePropertyKey);
        }

        static void ApplyFadeAlpha(OffScreenIndicatorObject indicator, float alpha)
        {
            indicator.VisibilityAlpha = alpha;
            indicator.SetAlpha(alpha);
        }

        void CompleteFade()
        {
            fadeTween = default;

            if (canvasGroup != null && canvasGroup.alpha <= 0f)
                gameObject.SetActive(false);
        }

        internal void StopFade()
        {
            fadeTween.Kill();
            fadeTween = default;
        }

        public void SetPosition(Vector2 position)
        {
            if (indicatorUI != null && indicatorUI.anchoredPosition != position)
                indicatorUI.anchoredPosition = position;
        }

        public void SetSize(float size)
        {
            Vector3 newScale = Vector3.one * size;
            if (indicatorUI != null && indicatorUI.localScale != newScale)
                indicatorUI.localScale = newScale;
        }

        public void SetIcon(Sprite icon)
        {
            if (iconObject != null && iconObject.sprite != icon)
                iconObject.sprite = icon;
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                alpha = Mathf.Clamp01(alpha);
                if (canvasGroup.alpha != alpha)
                    canvasGroup.alpha = alpha;
            }
        }

        public void SetArrowVisible(bool visible)
        {
            if (arrowObject != null && arrowObject.gameObject.activeSelf != visible)
                arrowObject.gameObject.SetActive(visible);
        }

        public void SetArrowRotation(float angle)
        {
            if (arrowObject != null && Mathf.Abs(Mathf.DeltaAngle(arrowObject.localEulerAngles.z, angle)) > 0.1f)
                arrowObject.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public void SetDistanceVisible(bool visible)
        {
            if (distanceText != null && distanceText.gameObject.activeSelf != visible)
                distanceText.gameObject.SetActive(visible);
        }

        public void SetDistanceText(string text)
        {
            if (distanceText == null)
                return;

            text ??= string.Empty;
            if (distanceLength < 0 && lastDistanceText == text)
                return;

            distanceLength = -1;
            lastDistanceText = text;
            TextProvider.SetText(distanceText, text);
        }

        internal void SetDistanceTextBuffer(StringBuilder text)
        {
            if (distanceText == null)
                return;

            bool changed = distanceLength != text.Length;
            for (int i = 0; !changed && i < text.Length; i++)
                changed = distanceBuffer[i] != text[i];

            if (!changed)
                return;

            if (distanceBuffer.Length < text.Length)
                Array.Resize(ref distanceBuffer, Mathf.NextPowerOfTwo(text.Length));

            text.CopyTo(0, distanceBuffer, 0, text.Length);
            distanceLength = text.Length;
            TextProvider.SetText(distanceText, distanceBuffer, 0, distanceLength);
        }

        public void FadeIn(float duration = 0.5f)
        {
            Initialize();

            if (!gameObject.activeSelf)
            {
                ApplyFadeAlpha(this, 0f);
                gameObject.SetActive(true);
            }

            StartFade(1f, duration);
        }

        public void FadeOut(float duration = 0.5f) => StartFade(0f, duration);

        #region Obsolete API
        [System.Obsolete("Use VisibilityAlpha instead.")]
        public void SetVisibilityAlpha(float alpha) => VisibilityAlpha = alpha;

        [System.Obsolete("Use VisibilityAlpha instead.")]
        public float GetVisibilityAlpha() => VisibilityAlpha;
        #endregion
    }
}