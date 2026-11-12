using Evo.Tween;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// One file on the Context Menu tab. Its menu items call these methods, so each pick changes the tile it was opened on.
    /// The tile's Interactive outlines it while hovered, and keeps it Selected while its menu is open.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFileTile : MonoBehaviour
    {
        const int ScaleKey = 1;
        const int BadgeKey = 2;

        [SerializeField] private DemoContextMenu owner;
        [SerializeField] private ContextMenu menu;
        [SerializeField] private Interactive interactive;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform badge;
        [SerializeField] private Image badgeIcon;

        public ContextMenu Menu => menu;
        public RectTransform Rect => (RectTransform)transform;
        public bool Removed { get; private set; }

        void Awake()
        {
            menu.onShow.AddListener(OnShow);
            menu.onHide.AddListener(OnHide);
        }

        // A copy starts plain: no badge
        public void Clear()
        {
            badge.gameObject.SetActive(false);
            group.alpha = 1;
            transform.localScale = Vector3.one;
        }

        public void SetIcon(Sprite sprite) => icon.sprite = sprite;

        public void Open()
        {
            EvoTween.Create(1f, 1.05f, 0.18f)
                .WithEase(Ease.OutCubic)
                .WithLoops(2, LoopMode.Yoyo)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(transform, static (t, s) => t.localScale = new Vector3(s, s, 1), ScaleKey);
        }

        // The copy is made from this tile, so the tile drops its open-menu look first and the copy starts without it
        public void Duplicate()
        {
            interactive.SetState(InteractionState.Normal);
            owner.Duplicate(this);
        }

        public void Share(Sprite sprite)
        {
            badgeIcon.sprite = sprite;
            badge.gameObject.SetActive(true);
            badge.localScale = Vector3.zero;
            EvoTween.Create(0f, 1f, 0.28f)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(badge, static (t, s) => t.localScale = new Vector3(s, s, 1), BadgeKey);
        }

        public void Delete()
        {
            if (Removed)
                return;

            Removed = true;
            group.blocksRaycasts = false;
            owner.Remove(this);

            EvoTween.Create(1f, 0.9f, 0.2f)
                .WithEase(Ease.InCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(transform, static (t, s) => t.localScale = new Vector3(s, s, 1), ScaleKey);
            EvoTween.Create(1f, 0f, 0.2f)
                .WithEase(Ease.InCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(() => Destroy(gameObject))
                .BindToAlpha(group);
        }

        public void PopIn()
        {
            group.alpha = 0;
            transform.localScale = Vector3.one * 0.85f;
            EvoTween.Create(0.85f, 1f, 0.26f)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(transform, static (t, s) => t.localScale = new Vector3(s, s, 1), ScaleKey);
            EvoTween.Create(0f, 1f, 0.2f)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(group);
        }

        // Runs before the menu builds its items, so Duplicate can drop out while every slot is taken
        void OnShow()
        {
            owner.OnMenuShow(this);
            interactive.SetState(InteractionState.Selected);
        }

        void OnHide() => interactive.SetState(InteractionState.Normal);
    }
}
