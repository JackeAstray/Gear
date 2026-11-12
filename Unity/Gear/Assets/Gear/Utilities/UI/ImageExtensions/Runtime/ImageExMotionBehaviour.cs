using UnityEngine;

using Gear.Common;
namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 运行时动效组件基类。
    /// <para>
    /// 设计约束：
    /// 1. 只驱动 <see cref="ImageEx"/> 已有的公开属性，不接管网格/材质渲染，也不引入任何补间库；
    /// 2. 禁用即停，Update 不做无意义计算；
    /// 3. 时间基准可选 scaled / unscaled，暂停菜单（timeScale=0）建议用 unscaled。
    /// </para>
    /// <para>
    /// 说明：本组件为 Gear 自研实现，参考"组件只负责参数、渲染由图形组件负责"的通用做法，
    /// 不依赖也不复制第三方 UI 插件的代码。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ImageEx))]
    public abstract class ImageExMotionBehaviour : MonoBehaviour
    {
        [SerializeField, Tooltip("推进速度（单位见具体组件，通常为“每秒多少单位/度”）")]
        protected float speed = 1f;

        [SerializeField, Tooltip("使用 unscaled 时间：暂停菜单、timeScale=0 时仍然动")]
        protected bool useUnscaledTime = true;

        [SerializeField, Tooltip("启用时把相位归零，避免复用对象时从上一次的位置继续")]
        protected bool restartOnEnable = true;

        /// <summary>当前驱动的 ImageEx（Awake 时解析，缺失时自动补取）。</summary>
        protected ImageEx Target { get; private set; }

        protected virtual void Awake()
        {
            Target = GetComponent<ImageEx>();
        }

        protected virtual void OnEnable()
        {
            if (Target == null) Target = GetComponent<ImageEx>();
            if (restartOnEnable) ResetPhase();
            // 立即应用一次，避免"启用后第一帧静止"的观感
            Apply(0f);
        }

        protected virtual void Update()
        {
            if (Target == null) return;
            Apply(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>把相位归零（子类覆盖）。</summary>
        protected virtual void ResetPhase() { }

        /// <summary>每帧推进一次；deltaTime 已按时间基准选择，首次调用为 0。</summary>
        protected abstract void Apply(float deltaTime);

        /// <summary>限频告警：只在第一次命中条件时提示，避免每帧刷 Console。</summary>
        protected static void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            flag = true;
            Log.Warning("[ImageEx 动效] {0}", message);
        }
    }
}
