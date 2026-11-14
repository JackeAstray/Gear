using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;
using TMPro;

namespace Gear.Tests
{
    /// <summary>GearInputFieldEnhancer 的纯逻辑：字符计数、提交策略、清洗与截断。</summary>
    public class GearInputFieldEnhancerTests
    {
        [Test]
        public void CountCharacters_CountsSurrogatePairsAsOne()
        {
            Assert.AreEqual(0, GearInputFieldEnhancer.CountCharacters(null));
            Assert.AreEqual(3, GearInputFieldEnhancer.CountCharacters("abc"));
            Assert.AreEqual(3, GearInputFieldEnhancer.CountCharacters("a😀b"), "emoji 占两个 UTF-16 单元但算一个字符");
            Assert.AreEqual(2, GearInputFieldEnhancer.CountCharacters("中文"));
        }

        [Test]
        public void FormatCounter_ShowsLimitOrPlainCount()
        {
            Assert.AreEqual("3/16", GearInputFieldEnhancer.FormatCounter(3, 16, "{0}/{1}"));
            Assert.AreEqual("3", GearInputFieldEnhancer.FormatCounter(3, 0, "{0}/{1}"));
            Assert.AreEqual("3 / 16 字", GearInputFieldEnhancer.FormatCounter(3, 16, "{0} / {1} 字"));
        }

        [Test]
        public void ShouldSubmit_BlocksCompositionAndEmpty()
        {
            Assert.IsFalse(GearInputFieldEnhancer.ShouldSubmit(false, false, true, "abc"), "IME 组合中不提交");
            Assert.IsFalse(GearInputFieldEnhancer.ShouldSubmit(false, false, false, "   "), "空白不提交");
            Assert.IsTrue(GearInputFieldEnhancer.ShouldSubmit(false, false, false, "abc"));
        }

        [Test]
        public void ShouldSubmit_MultilineNeedsCtrl()
        {
            Assert.IsFalse(GearInputFieldEnhancer.ShouldSubmit(true, false, false, "abc"));
            Assert.IsTrue(GearInputFieldEnhancer.ShouldSubmit(true, true, false, "abc"));
        }

        [Test]
        public void Truncate_KeepsSurrogatePairsWhole()
        {
            Assert.AreEqual("ab", GearInputFieldEnhancer.Truncate("abc", 2));
            Assert.AreEqual("a😀", GearInputFieldEnhancer.Truncate("a😀b", 2), "不能截出半个 emoji");
            Assert.AreEqual("abc", GearInputFieldEnhancer.Truncate("abc", 10));
        }

        [Test]
        public void Sanitize_TrimsAndFlattensNewlines()
        {
            Assert.AreEqual("abc", GearInputFieldEnhancer.Sanitize("  abc  ", true, false));
            Assert.AreEqual("a b", GearInputFieldEnhancer.Sanitize("a\nb", true, false));
            Assert.AreEqual("a\nb", GearInputFieldEnhancer.Sanitize("a\nb", true, true));
            Assert.AreEqual("  abc  ", GearInputFieldEnhancer.Sanitize("  abc  ", false, false));
        }

        [Test]
        public void TrySubmit_BlocksCompositionAndEmpty()
        {
            GameObject go = new GameObject("Input", typeof(RectTransform), typeof(GearInputFieldEnhancer));
            try
            {
                GearInputFieldEnhancer enhancer = go.GetComponent<GearInputFieldEnhancer>();
                int calls = 0;
                enhancer.OnSubmitted.AddListener(_ => calls++);

                enhancer.CompositionProvider = () => true;
                Assert.IsFalse(enhancer.TrySubmit("abc"), "IME 组合中拒绝提交");
                Assert.AreEqual(0, calls);

                enhancer.CompositionProvider = () => false;
                Assert.IsFalse(enhancer.TrySubmit("   "), "空文本默认不提交");
                Assert.AreEqual(0, calls);

                enhancer.AllowEmptySubmit = true;
                Assert.IsTrue(enhancer.TrySubmit("   "));
                Assert.AreEqual(1, calls);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TrySubmit_CleansValueAndRaisesEvent()
        {
            GameObject go = new GameObject("Input", typeof(RectTransform), typeof(GearInputFieldEnhancer));
            try
            {
                GearInputFieldEnhancer enhancer = go.GetComponent<GearInputFieldEnhancer>();
                string captured = null;
                enhancer.Submitted += value => captured = value;

                Assert.IsTrue(enhancer.TrySubmit("  a\nb  "));
                Assert.AreEqual("a b", captured, "单行提交应 Trim 并把换行压成空格");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsMultiline_RecognizesBothMultiLineTypes()
        {
            Assert.IsFalse(GearInputFieldEnhancer.IsMultiline(TMP_InputField.LineType.SingleLine));
            Assert.IsTrue(GearInputFieldEnhancer.IsMultiline(TMP_InputField.LineType.MultiLineNewline));
            Assert.IsTrue(GearInputFieldEnhancer.IsMultiline(TMP_InputField.LineType.MultiLineSubmit));
        }
    }
}
