using NUnit.Framework;
using Gear.Framework.Modules;
using Gear.Framework.Modules.Pool;
using UnityEngine;

namespace Gear.Tests
{
    /// <summary>
    /// 共享对象池参数回归。
    /// <para>
    /// 原实现 <c>GetPool</c> 先 <c>AddComponent</c>（立即触发 Awake 建池），之后才给
    /// prefab / prewarmCount / maxSize 赋值；<c>EnsureInitialized()</c> 因池已存在直接返回，
    /// 于是调用方传入的预热数量与上限全部被忽略（上限停在默认 100，预热 0 个）。
    /// </para>
    /// </summary>
    public class ObjectPoolPrewarmTests
    {
        private GameObject prefab;

        [TearDown]
        public void TearDown()
        {
            GameObjectPoolRegistry.ClearAll();
            if (prefab != null)
            {
                Object.DestroyImmediate(prefab);
            }
            prefab = null;
        }

        [Test]
        public void GetPool_Uses_Requested_Prewarm_And_MaxSize()
        {
            prefab = new GameObject("GearPoolProbe");

            var pool = GameObjectPoolRegistry.GetPool(prefab, prewarm: 3, maxSize: 7);

            Assert.IsNotNull(pool);
            Assert.AreEqual(7, pool.maxSize, "maxSize 必须在 Awake 建池之前生效");
            Assert.AreEqual(3, pool.CountInactive,
                "prewarm 必须真正创建实例：UnityEngine.Pool.ObjectPool 的 defaultCapacity 只是栈容量，不会实例化");
        }
    }
}
