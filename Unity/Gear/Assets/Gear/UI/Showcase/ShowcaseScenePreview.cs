using UnityEngine;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// `UI Showcase Example` 场景里的编辑期预览实例。
    /// <para>
    /// 场景本身只保留 <see cref="ShowcaseLauncher"/>；为了打开场景就能看到展厅布局，
    /// 这里放一份 ShowcaseUIPlane prefab 实例。进入 Play 后它会在 Awake 立刻移除，
    /// 正式窗口仍由 Launcher 通过 Resources + UISystem 加载，避免两份 UI 同时存在。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ShowcaseScenePreview : MonoBehaviour
    {
        private void Awake()
        {
            if (!Application.isPlaying) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
