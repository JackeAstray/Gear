using Gear.Utilities.UI.Backdrop;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class UIBackdropSharedCaptureTests
    {
        private GameObject sourceGo;
        private Camera source;

        [SetUp]
        public void SetUp()
        {
            sourceGo = new GameObject("SourceCamera");
            source = sourceGo.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            if (sourceGo != null) Object.DestroyImmediate(sourceGo);
        }

        [Test]
        public void Acquire_SharesCameraAndTexture()
        {
            Camera first = UIBackdropSharedCapture.Acquire(source, 2, ~0, 320, 180, out RenderTexture textureA);
            Camera second = UIBackdropSharedCapture.Acquire(source, 2, ~0, 320, 180, out RenderTexture textureB);

            Assert.AreSame(first, second);
            Assert.AreSame(textureA, textureB);
            Assert.AreEqual(1, UIBackdropSharedCapture.EntryCount);

            UIBackdropSharedCapture.Release(first);
            UIBackdropSharedCapture.Release(second);
            Assert.AreEqual(0, UIBackdropSharedCapture.EntryCount);
        }

        [Test]
        public void Release_LastReferenceDestroysEntry()
        {
            Camera camera = UIBackdropSharedCapture.Acquire(source, 1, ~0, 160, 90, out _);
            Assert.AreEqual(1, UIBackdropSharedCapture.EntryCount);

            UIBackdropSharedCapture.Release(camera);
            Assert.AreEqual(0, UIBackdropSharedCapture.EntryCount);
        }
    }
}
