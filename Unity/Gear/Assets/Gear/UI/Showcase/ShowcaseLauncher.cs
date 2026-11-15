using System;
using Cysharp.Threading.Tasks;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using UnityEngine;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 示例场景入口：等 UISystem 初始化完成后打开展厅。
    /// <para>
    /// 放在 <c>UI Showcase Example</c> 场景里；Bootstrap 对该场景只初始化引擎、不跳转场景，
    /// 因此这里只负责开窗。其它工程接入时也可以复制这段等待逻辑。
    /// </para>
    /// </summary>
    public sealed class ShowcaseLauncher : MonoBehaviour
    {
        private async void Start()
        {
            try
            {
                for (int i = 0; i < 600; i++)
                {
                    UISystem system = UISystem.GetOrInstance();
                    if (system != null && system.IsInited)
                    {
                        await system.OpenWindowAsync<ShowcaseUIPlane>();
                        return;
                    }

                    await UniTask.Yield();
                }

                Log.Error("[ShowcaseLauncher] UISystem 在 600 帧内没有初始化，展厅未打开");
            }
            catch (Exception ex)
            {
                Log.Error("[ShowcaseLauncher] 打开展厅失败: {0}", ex);
            }
        }
    }
}
