using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace Gear.Framework.Modules
{
#if !UNITY_6000_0_OR_NEWER
    [Preserve]
    public class SkipSplashScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Run()
        {
#if UNITY_WEBGL
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
#else
            System.Threading.Tasks.Task.Run(() => SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate));
#endif
        }
    }
#endif
}
