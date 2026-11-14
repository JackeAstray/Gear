using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 错峰出现：按子节点顺序依次做“从 <see cref="FromScale"/> 弹到 1 + 淡入”，
    /// 每条子项的延迟 = 序号 × <see cref="Stagger"/>。
    /// <para>
    /// 语义参考 Evo 的 AnimatedContainer，但不依赖补间库：用 unscaled 时间在 Update 里推进，
    /// 同时提供 <see cref="Sample"/> 便于编辑器预览与 EditMode 测试。
    /// 只改子项 localScale / CanvasGroup.alpha，不接管布局。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Animated Container")]
    [DisallowMultipleComponent]
    public sealed class GearAnimatedContainer : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float itemDuration = 0.28f;
        [SerializeField, Min(0f)] private float stagger = 0.045f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float fromScale = 0.86f;
        [SerializeField] private float toScale = 1f;
        [SerializeField] private bool fade = true;
        [SerializeField] private bool disableWhenHidden;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop;
        [SerializeField, Min(0.05f)] private float loopInterval = 1.5f;
        [SerializeField] private bool resetOnDisable;
        [SerializeField] private bool useUnscaledTime = true;

        private sealed class Item
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Selectable Selectable;
            public Vector3 BaseScale = Vector3.one;
            public bool HasBaseScale;
            public float Delay;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();
        private bool cached;
        private float elapsed = -1f;
        private float loopCountdown = -1f;

        /// <summary>单条子项的时长（秒）。</summary>
        public float ItemDuration
        {
            get => itemDuration;
            set => itemDuration = Mathf.Max(0.01f, value);
        }

        /// <summary>相邻子项的启动间隔（秒）。</summary>
        public float Stagger
        {
            get => stagger;
            set => stagger = Mathf.Max(0f, value);
        }

        /// <summary>起始缩放（1 表示不缩放）。</summary>
        public float FromScale
        {
            get => fromScale;
            set => fromScale = value;
        }

        /// <summary>播完后是否按 <see cref="LoopInterval"/> 重播。</summary>
        public bool Loop
        {
            get => loop;
            set => loop = value;
        }

        /// <summary>循环间隔（秒）。</summary>
        public float LoopInterval
        {
            get => loopInterval;
            set => loopInterval = Mathf.Max(0.05f, value);
        }

        /// <summary>总时长（最后一条结束）。</summary>
        public float TotalDuration
        {
            get
            {
                Cache();
                return items.Count == 0 ? 0f : items[items.Count - 1].Delay + itemDuration;
            }
        }

        /// <summary>是否正在播放。</summary>
        public bool IsPlaying => elapsed >= 0f;

        private void Awake()
        {
            Cache();
        }

        private void OnEnable()
        {
            Cache();
            if (playOnEnable) Play();
        }

        private void OnDisable()
        {
            elapsed = -1f;
            loopCountdown = -1f;
            if (resetOnDisable) Sample(0f);
        }

        private void Update()
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (elapsed >= 0f)
            {
                elapsed += delta;
                Apply(elapsed);

                if (elapsed >= TotalDuration)
                {
                    Apply(TotalDuration);
                    elapsed = -1f;
                    if (loop) loopCountdown = loopInterval;
                }
                return;
            }

            if (!loop || loopCountdown < 0f) return;

            loopCountdown -= delta;
            if (loopCountdown <= 0f) Play();
        }

        /// <summary>从头播放。</summary>
        public void Play()
        {
            Cache();
            loopCountdown = -1f;
            elapsed = 0f;
            Apply(0f);
        }

        /// <summary>直接跳到完成态（常用于不可见时预置）。</summary>
        public void CompleteImmediately()
        {
            Cache();
            elapsed = -1f;
            loopCountdown = loop ? loopInterval : -1f;
            Apply(TotalDuration);
        }

        /// <summary>按已播放秒数采样；不改变播放状态，供编辑预览与测试使用。</summary>
        public void Sample(float seconds)
        {
            Cache();
            Apply(Mathf.Max(0f, seconds));
        }

        private void Cache()
        {
            if (cached && items.Count == transform.childCount) return;

            items.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                RectTransform rect = child as RectTransform;
                if (rect == null) continue;

                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (fade && group == null) group = child.gameObject.AddComponent<CanvasGroup>();

                if (!baseScales.TryGetValue(rect, out Vector3 baseScale))
                {
                    baseScale = rect.localScale;
                    baseScales[rect] = baseScale;
                }

                items.Add(new Item
                {
                    Rect = rect,
                    Group = group,
                    Selectable = child.GetComponent<Selectable>(),
                    BaseScale = baseScale,
                    HasBaseScale = true,
                    Delay = items.Count * stagger,
                });
            }

            cached = true;
        }

        private void Apply(float seconds)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                float local = itemDuration <= 0f ? 1f : Mathf.Clamp01((seconds - item.Delay) / itemDuration);
                float eased = scaleCurve != null ? scaleCurve.Evaluate(local) : local;
                float scale = Mathf.LerpUnclamped(fromScale, toScale, eased);

                if (item.Rect != null)
                {
                    Vector3 baseScale = item.HasBaseScale ? item.BaseScale : Vector3.one;
                    item.Rect.localScale = baseScale * scale;
                }

                if (item.Group != null)
                {
                    item.Group.alpha = fade ? local : 1f;
                }

                if (disableWhenHidden && item.Selectable != null)
                {
                    item.Selectable.interactable = local >= 0.999f;
                }
            }
        }
    }
}
