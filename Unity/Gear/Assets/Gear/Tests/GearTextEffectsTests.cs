using NUnit.Framework;
using Gear.Utilities.UI;

namespace Gear.Tests
{
    /// <summary>
    /// GearTextEffects 的纯算术测试（EditMode，不依赖 TMP 渲染）。
    /// </summary>
    public class GearTextEffectsTests
    {
        [Test]
        public void ComputeVisibleCharacters_ClampsToRange()
        {
            Assert.AreEqual(0, GearTextEffects.ComputeVisibleCharacters(0f, 20f, 10));
            Assert.AreEqual(0, GearTextEffects.ComputeVisibleCharacters(-1f, 20f, 10));
            Assert.AreEqual(5, GearTextEffects.ComputeVisibleCharacters(0.25f, 20f, 10));
            Assert.AreEqual(10, GearTextEffects.ComputeVisibleCharacters(10f, 20f, 10));
        }

        [Test]
        public void ComputeVisibleCharacters_HandlesInvalidRateAndCount()
        {
            Assert.AreEqual(0, GearTextEffects.ComputeVisibleCharacters(1f, 0f, 10));
            Assert.AreEqual(0, GearTextEffects.ComputeVisibleCharacters(1f, 20f, 0));
        }

        [Test]
        public void CountVisibleCharacters_SkipsRichTextTags()
        {
            Assert.AreEqual(4, GearTextEffects.CountVisibleCharacters("<b>Hi</b> X"));
            Assert.AreEqual(0, GearTextEffects.CountVisibleCharacters(string.Empty));
        }

        [Test]
        public void ComputeScrambleReveal_ClampsToRange()
        {
            Assert.AreEqual(0, GearTextEffects.ComputeScrambleReveal(0f, 1f, 10));
            Assert.AreEqual(5, GearTextEffects.ComputeScrambleReveal(0.5f, 1f, 10));
            Assert.AreEqual(10, GearTextEffects.ComputeScrambleReveal(2f, 1f, 10));
            Assert.AreEqual(10, GearTextEffects.ComputeScrambleReveal(1f, 0f, 10));
        }
    }
}
