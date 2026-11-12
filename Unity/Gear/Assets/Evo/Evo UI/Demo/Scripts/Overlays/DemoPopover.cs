using System;
using Evo.Tween;
using Evo.Tween.Modules.Transforms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Popover tab: a game library whose Filter button opens a popover of live kit Toggles, one per genre.
    /// The panel opens below, left or right of the button; games outside the picked genres fade out.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoPopover : MonoBehaviour
    {
        const float FilterDuration = 0.25f;
        const float HiddenScale = 0.94f;
        const float HiddenAlpha = 0.18f;

        [Serializable]
        class Game
        {
            public RectTransform rect;
            public CanvasGroup group;
            public int genre;
        }

        [SerializeField] private Popover popover;
        [SerializeField] private RectTransform button;
        [SerializeField] private float gap = 16;

        [Header("Filter")]
        [SerializeField] private Toggle[] genres;
        [SerializeField] private Game[] games;

        [Header("Options")]
        [SerializeField] private TMP_Text info;

        static readonly Vector2[] Pivots = { new(0.5f, 1), new(1, 0.5f), new(0, 0.5f) };
        static readonly Vector3[] Corners = new Vector3[4];

        RectTransform panel;
        int side;
        bool placePending;

        void Awake()
        {
            panel = (RectTransform)popover.transform;
            foreach (Toggle genre in genres)
                genre.onValueChanged.AddListener(OnFilterChanged);
        }

        void Start()
        {
            placePending = true;
            Filter(false);
        }

        // The button sizes itself to its label, which happens in the page layout after the tab first opens
        void LateUpdate()
        {
            if (placePending && button.rect.width > 1)
            {
                placePending = false;
                Place();
            }
        }

        void OnEnable() => UpdateInfo();

        // Below, Left or Right
        public void SetSide(int index)
        {
            side = index;
            if (!placePending && panel != null)
                Place();
        }

        public void SetOutsideClick(bool value)
        {
            popover.closeOnOutsideClick = value;
            UpdateInfo();
        }

        public void SetEscapeKey(bool value)
        {
            popover.closeOnEscapeKey = value;
            UpdateInfo();
        }

        // Opening scales from wherever closing left it, so the close animation's end scale is the start scale
        public void SetStartScale(float percent)
        {
            Vector3 scale = Vector3.one * (percent / 100f);

            for (int i = 0; i < popover.closeAnimation.Count; i++)
            {
                if (popover.closeAnimation.Clips[i].Settings.Module is TransformScaleModule module)
                    module.to = scale;
            }

            if (!popover.isOpen)
                popover.transform.localScale = scale;
        }

        public void OnStateChanged(bool open) => UpdateInfo();

        void OnFilterChanged(bool _) => Filter(true);

        void Filter(bool animate)
        {
            for (int i = 0; i < games.Length; i++)
            {
                Game game = games[i];
                bool shown = genres[game.genre].IsOn;
                float alpha = shown ? 1f : HiddenAlpha;
                float scale = shown ? 1f : HiddenScale;
                if (!animate)
                {
                    game.group.alpha = alpha;
                    game.rect.localScale = new Vector3(scale, scale, 1);
                    continue;
                }

                DemoUtils.Fade(game.group, alpha, FilterDuration);
                EvoTween.Create(game.rect.localScale, new Vector3(scale, scale, 1), FilterDuration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToScale(game.rect);
            }
        }

        // The panel sits on the chosen side of the button, centered on it
        void Place()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(button);
            Rect anchor = ButtonRect();
            panel.pivot = Pivots[side];
            panel.anchoredPosition = side switch
            {
                0 => new Vector2(anchor.center.x, anchor.yMin - gap),
                1 => new Vector2(anchor.xMin - gap, anchor.center.y),
                _ => new Vector2(anchor.xMax + gap, anchor.center.y),
            };
        }

        // The button's rect in the space of the panel's parent, whose anchors the panel uses at its center
        Rect ButtonRect()
        {
            var parent = (RectTransform)panel.parent;
            button.GetWorldCorners(Corners);
            Vector3 a = parent.InverseTransformPoint(Corners[0]), b = parent.InverseTransformPoint(Corners[2]);
            Vector2 offset = parent.rect.center;
            return Rect.MinMaxRect(a.x - offset.x, a.y - offset.y, b.x - offset.x, b.y - offset.y);
        }

        void UpdateInfo()
        {
            if (!popover.isOpen) { info.SetText("Open Filter: the panel holds live Toggles."); }
            else if (popover.closeOnOutsideClick && popover.closeOnEscapeKey) { info.SetText("Click outside, press Esc or click the button again to close it."); }
            else if (popover.closeOnOutsideClick) { info.SetText("Click outside or click the button again to close it."); }
            else if (popover.closeOnEscapeKey) { info.SetText("Press Esc or click the button again to close it."); }
            else { info.SetText("Only the button closes it now."); }
        }
    }
}
