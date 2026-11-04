using UnityEngine;
using UnityEngine.Rendering;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// 旧版 Unity（&lt; 6000）不支持在 Player Settings 里关闭 Splash，需要在启动前主动停掉。
    /// Unity 6 起该项目已由引擎处理，因此整段在 6000+ 上不参与编译。
    /// 放在 <c>Gear.Framework.Modules</c> 而不是全局命名空间：全局类型会和接入方代码撞名。
    /// </summary>
#if !UNITY_6000_0_OR_NEWER
    internal static class SkipSplashScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Run()
        {
            // SplashScreen.Stop 必须在主线程调用，BeforeSplashScreen 回调保证在主线程执行
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        }
    }
#endif
}
