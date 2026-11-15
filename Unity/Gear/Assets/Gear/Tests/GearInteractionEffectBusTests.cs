using System.Collections.Generic;
using Gear.Utilities.UI.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearInteractionEffectBusTests
    {
        private sealed class FakeEffect : IGearInteractionEffect
        {
            public int Priority { get; set; }
            public string Channel { get; set; } = "fx";
            public readonly List<GearInteractionState> States = new List<GearInteractionState>();

            public void OnInteraction(GearInteractionState state, GearInteractive source)
            {
                States.Add(state);
            }
        }

        private GameObject root;
        private GearInteractionEffectBus bus;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Bus", typeof(RectTransform), typeof(GearInteractive), typeof(GearInteractionEffectBus));
            bus = root.GetComponent<GearInteractionEffectBus>();
            bus.Source = root.GetComponent<GearInteractive>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Dispatch_RunsHigherPriorityFirst()
        {
            var low = new FakeEffect { Priority = 1, Channel = "a" };
            var high = new FakeEffect { Priority = 10, Channel = "b" };
            bus.Register(low);
            bus.Register(high);

            bus.Dispatch(GearInteractionState.Pressed);

            Assert.AreEqual(1, low.States.Count);
            Assert.AreEqual(1, high.States.Count);
            Assert.AreEqual(high, bus.Effects[0]);
        }

        [Test]
        public void Dispatch_DeduplicatesSameChannel()
        {
            var first = new FakeEffect { Priority = 10, Channel = "glow" };
            var second = new FakeEffect { Priority = 5, Channel = "glow" };
            bus.Register(first);
            bus.Register(second);

            bus.Dispatch(GearInteractionState.Highlighted);

            Assert.AreEqual(1, first.States.Count);
            Assert.AreEqual(0, second.States.Count);
        }

        [Test]
        public void Dispatch_WithoutDedupe_RunsBoth()
        {
            bus.DedupeChannels = false;
            var first = new FakeEffect { Priority = 10, Channel = "glow" };
            var second = new FakeEffect { Priority = 5, Channel = "glow" };
            bus.Register(first);
            bus.Register(second);

            bus.Dispatch(GearInteractionState.Highlighted);

            Assert.AreEqual(1, first.States.Count);
            Assert.AreEqual(1, second.States.Count);
        }
    }
}
