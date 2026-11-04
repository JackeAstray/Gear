using NUnit.Framework;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Scene;
using Gear.Framework.Runtime.UI;
using Gear.Common;

namespace Gear.Tests
{
    /// <summary>
    /// 窗口目录的声明式元数据测试（纯反射，不实例化 MonoBehaviour）。
    /// </summary>
    public class UIWindowCatalogTests
    {
        [UIWindow("GearTestStaticWindow", Layer = PanelType.TipsUI, Static = true)]
        private sealed class StaticTestWindow : UIController
        {
        }

        [UIWindow("GearTestPooledWindow", Pooled = true)]
        private sealed class PooledTestWindow : UIController
        {
        }

        private sealed class PlainTestWindow : UIController
        {
        }

        [Test]
        public void StaticDeclaration_IsReadIntoDescriptor()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("GearTestStaticWindow");

            Assert.IsTrue(descriptor.Declared);
            Assert.IsTrue(descriptor.Static, "[UIWindow(Static = true)] 应写入描述符");
            Assert.IsFalse(descriptor.Pooled);
            Assert.AreEqual(PanelType.TipsUI, descriptor.Layer);
        }

        [Test]
        public void PooledDeclaration_DoesNotEnableStatic()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("GearTestPooledWindow");

            Assert.IsTrue(descriptor.Declared);
            Assert.IsTrue(descriptor.Pooled);
            Assert.IsFalse(descriptor.Static, "Pooled 与 Static 是两套语义，不能互相隐含");
        }

        [Test]
        public void UndeclaredWindow_FallsBackToDefaults()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("PlainTestWindow");

            Assert.IsFalse(descriptor.Declared);
            Assert.IsFalse(descriptor.Static);
            Assert.IsFalse(descriptor.Pooled);
            Assert.AreEqual(PanelType.NormalUI, descriptor.Layer);
        }

        [Test]
        public void GetId_ByType_MatchesDeclaredId()
        {
            Assert.AreEqual("GearTestStaticWindow", UIWindowCatalog.GetId(typeof(StaticTestWindow)));
        }

        [Test]
        public void Reset_ThenRescan_StillFindsDeclaredWindows()
        {
            UIWindowCatalog.Reset();
            try
            {
                // Reset 之后必须能重新扫描出窗口（清表但不至于把 built 永久置 true 或反向）
                UIWindowDescriptor descriptor = UIWindowCatalog.Get("GearTestStaticWindow");
                Assert.IsTrue(descriptor.Declared, "Reset 后重扫应重新登记带 [UIWindow] 的类型");
                Assert.Greater(UIWindowCatalog.Count, 0);
            }
            finally
            {
                UIWindowCatalog.Reset();
            }
        }

        [Test]
        public void AssemblyLoad_InvalidatesCatalog_AndRescanFindsTypes()
        {
            UIWindowCatalog.Warmup();
            int before = UIWindowCatalog.Count;

            // 模拟"运行期又加载了一个程序集"：直接触发私有 AssemblyLoad 处理逻辑。
            // 这一路径此前会与 EnsureBuilt 末尾的 built = true 竞态，导致缓存被标记为有效、
            // 新程序集里的窗口永远登记不上；这里确保失效后仍能重扫出完整目录。
            var handler = typeof(UIWindowCatalog).GetMethod(
                "OnAssemblyLoad",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(handler, "AssemblyLoad 失效逻辑应保留（否则 UPM 包窗口在懒加载后不可见）");
            handler.Invoke(null, new object[] { null, null });

            Assert.AreEqual(before, UIWindowCatalog.Count, "重扫结果应与失效前一致（当前程序集集合未变）");
            Assert.IsTrue(UIWindowCatalog.Get("GearTestStaticWindow").Declared);
        }

        [Test]
        public void ConcurrentWarmup_DoesNotCorruptCatalog()
        {
            UIWindowCatalog.Reset();
            var errors = new System.Collections.Concurrent.ConcurrentBag<System.Exception>();

            // 多个线程同时触发构建：没有互斥时会出现"边 Clear 边遍历"，Count/字典可能不一致
            var threads = new System.Threading.Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new System.Threading.Thread(() =>
                {
                    try
                    {
                        for (int n = 0; n < 20; n++)
                        {
                            UIWindowCatalog.Warmup();
                            _ = UIWindowCatalog.Count;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads[i].Start();
            }
            foreach (var t in threads) t.Join();

            Assert.IsEmpty(errors, "并发构建不应抛异常");
            Assert.IsTrue(UIWindowCatalog.Get("GearTestStaticWindow").Declared, "并发构建后目录应完整");
        }
    }
}
