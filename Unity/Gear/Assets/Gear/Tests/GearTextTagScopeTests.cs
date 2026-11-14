using System.Collections.Generic;
using NUnit.Framework;
using Gear.Utilities.UI;

namespace Gear.Tests
{
    /// <summary>GearTextTagScope 的标签解析与字符区间映射。</summary>
    public class GearTextTagScopeTests
    {
        private static string Parse(string source, out List<GearTextTagRange> ranges)
        {
            ranges = new List<GearTextTagRange>();
            var wave = new List<int>();
            var shake = new List<int>();
            var rainbow = new List<int>();
            var bounce = new List<int>();
            var pulse = new List<int>();
            return GearTextTagScope.Parse(source, ranges, wave, shake, rainbow, bounce, pulse);
        }

        [Test]
        public void Parse_StripsKnownTagsAndRecordsRange()
        {
            string text = Parse("普通<wave>波浪</wave>文本", out var ranges);

            Assert.AreEqual("普通波浪文本", text);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(GearTextTagKind.Wave, ranges[0].Kind);
            Assert.AreEqual(2, ranges[0].Start);
            Assert.AreEqual(4, ranges[0].End);
        }

        [Test]
        public void Parse_KeepsUnknownRichTextTags()
        {
            string text = Parse("<b>粗体</b> <wave>波浪</wave>", out var ranges);

            Assert.AreEqual("<b>粗体</b> 波浪", text);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(3, ranges[0].Start, "TMP 富文本标签不参与可见字符计数");
            Assert.AreEqual(5, ranges[0].End);
        }

        [Test]
        public void Parse_HandlesMultipleAndCaseInsensitiveTags()
        {
            string text = Parse("<WAVE>a</wave><shake>b</SHAKE>", out var ranges);

            Assert.AreEqual("ab", text);
            Assert.AreEqual(2, ranges.Count);
            Assert.AreEqual(0, ranges[0].Start);
            Assert.AreEqual(1, ranges[0].End);
            Assert.AreEqual(1, ranges[1].Start);
            Assert.AreEqual(2, ranges[1].End);
        }

        [Test]
        public void Parse_UnclosedTagRunsToEnd()
        {
            string text = Parse("前<bounce>后", out var ranges);

            Assert.AreEqual("前后", text);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(1, ranges[0].Start);
            Assert.AreEqual(2, ranges[0].End);
        }

        [Test]
        public void Parse_CountsSurrogatePairsOnce()
        {
            string text = Parse("😀<rainbow>彩</rainbow>", out var ranges);

            Assert.AreEqual("😀彩", text);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(1, ranges[0].Start, "emoji 占一个字符");
            Assert.AreEqual(2, ranges[0].End);
        }

        [Test]
        public void Parse_NoTagsReturnsInput()
        {
            string text = Parse("普通文本", out var ranges);
            Assert.AreEqual("普通文本", text);
            Assert.IsEmpty(ranges);
        }

        [Test]
        public void Parse_MalformedTagIsKept()
        {
            string text = Parse("a<wave b", out var ranges);
            Assert.AreEqual("a<wave b", text);
            Assert.IsEmpty(ranges);
        }
    }
}
