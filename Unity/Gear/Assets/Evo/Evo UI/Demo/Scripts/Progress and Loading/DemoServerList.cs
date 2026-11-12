using Evo.Tween;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Spinner (Horizontal) tab: loading shows the spinner over breathing skeleton rows, then the spinner fades out
    /// and the regions rise in one after another, the best one picked. Refresh and every spinner option load again.
    /// Each row is an Interactive: its shapes show the hover slab, and the picked row stays in the Selected state.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoServerList : MonoBehaviour
    {
        [SerializeField] private CanvasGroup spinner;
        [SerializeField] private CanvasGroup placeholders;
        [SerializeField] private Interactive[] rows;
        [Tooltip("Each row's Canvas Group, in the same order.")]
        [SerializeField] private CanvasGroup[] rowGroups;
        [SerializeField] private float loadTime = 2.6f;
        [SerializeField] private float rowDelay = 0.055f;

        const float FadeDuration = 0.2f;
        const float RowDuration = 0.35f;
        const float RowRise = 12;

        Vector2[] rowPositions;
        TweenHandle[] rowFades;
        TweenHandle[] rowSlides;
        TweenHandle spinnerFade;
        TweenHandle placeholderFade;
        float loading;

        void Awake()
        {
            rowPositions = new Vector2[rows.Length];
            rowFades = new TweenHandle[rows.Length];
            rowSlides = new TweenHandle[rows.Length];

            for (int i = 0; i < rows.Length; i++)
                rowPositions[i] = ((RectTransform)rows[i].transform).anchoredPosition;
        }

        void OnEnable() => Load();

        void OnDisable() => KillAll();

        void Update()
        {
            if (loading <= 0)
                return;

            loading -= Time.unscaledDeltaTime;
            if (loading <= 0)
                Reveal();
        }

        public void Load()
        {
            if (!isActiveAndEnabled)
                return;

            KillAll();
            loading = loadTime;
            spinnerFade = DemoUtils.Fade(spinner, 1, FadeDuration);

            // Placeholders breathe until the list arrives
            placeholders.alpha = 1;
            placeholderFade = EvoTween.Create(1f, 0.45f, 0.6f)
                .WithEase(Ease.InOutSine)
                .WithLoops(0, LoopMode.Yoyo)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(placeholders);

            for (int i = 0; i < rows.Length; i++)
            {
                rowGroups[i].alpha = 0;
                rowGroups[i].blocksRaycasts = false;
                ((RectTransform)rows[i].transform).anchoredPosition = rowPositions[i];
            }
        }

        void Reveal()
        {
            spinnerFade = DemoUtils.Fade(spinner, 0, FadeDuration);
            placeholderFade.Kill();
            placeholderFade = DemoUtils.Fade(placeholders, 0, FadeDuration);
            Select(0, true);

            for (int i = 0; i < rows.Length; i++)
            {
                var rect = (RectTransform)rows[i].transform;
                float delay = i * rowDelay;
                rowGroups[i].blocksRaycasts = true;

                rowFades[i] = EvoTween.Create(0f, 1f, RowDuration)
                    .WithDelay(delay)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToAlpha(rowGroups[i]);

                rowSlides[i] = EvoTween.Create(rowPositions[i] - new Vector2(0, RowRise), rowPositions[i], RowDuration)
                    .WithDelay(delay)
                    .WithEase(Ease.OutQuint)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToAnchoredPosition(rect);
            }
        }

        // Called by each row's Interactive
        public void Select(int index) => Select(index, false);

        // The rows are sorted by ping, so the first one is the best region. Only the rows whose pick changes get a new state,
        // so a hovered row keeps its hover
        void Select(int index, bool instant)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (i == index) { rows[i].SetState(InteractionState.Selected, instant); }
                else if (rows[i].InteractionState == InteractionState.Selected) { rows[i].SetState(InteractionState.Normal, instant); }
            }
        }

        void KillAll()
        {
            spinnerFade.Kill();
            placeholderFade.Kill();

            for (int i = 0; i < rows.Length; i++)
            {
                rowFades[i].Kill();
                rowSlides[i].Kill();
            }
        }
    }
}
