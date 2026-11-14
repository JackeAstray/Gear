using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearSearchIndex 的分词、排序与多词匹配。</summary>
    public class GearSearchIndexTests
    {
        private GameObject go;
        private GearSearchIndex index;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Search", typeof(RectTransform), typeof(GearSearchIndex));
            index = go.GetComponent<GearSearchIndex>();
            index.Build(new List<string>
            {
                "Attack Boost",
                "Attack Matrix",
                "Critical Core",
                "防御矩阵",
                "attack speed",
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Search_ExactTokenComesBeforePrefixAndContains()
        {
            var results = new List<int>();
            int count = index.Search("attack", results);

            Assert.AreEqual(3, count);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, results, "整词命中按原序返回");
            Assert.IsFalse(results.Contains(2), "未命中的条目不应出现");
        }

        [Test]
        public void Search_IsCaseInsensitive()
        {
            var a = new List<int>();
            var b = new List<int>();
            index.Search("ATTACK", a);
            index.Search("attack", b);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Search_MultipleTermsRequireAll()
        {
            var results = new List<int>();
            Assert.AreEqual(1, index.Search("attack matrix", results));
            Assert.AreEqual(1, results[0]);

            Assert.AreEqual(0, index.Search("attack 防御", results));
        }

        [Test]
        public void Search_MatchesSubstringInCjkText()
        {
            var results = new List<int>();
            Assert.AreEqual(1, index.Search("矩阵", results));
            Assert.AreEqual(3, results[0]);
        }

        [Test]
        public void Search_EmptyQueryReturnsAll()
        {
            var results = new List<int>();
            Assert.AreEqual(5, index.Search("   ", results));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, results);
        }

        [Test]
        public void Search_ClearsPreviousResults()
        {
            var results = new List<int> { 99, 98 };
            index.Search("critical", results);
            Assert.AreEqual(1, results.Count, "旧结果必须被清空");
            Assert.AreEqual(2, results[0]);
        }

        [Test]
        public void Rebuild_ReplacesOldEntries()
        {
            index.Build(new List<string> { "Only One" });
            var results = new List<int>();
            Assert.AreEqual(0, index.Search("attack", results));
            Assert.AreEqual(1, index.Search("only", results));
            Assert.AreEqual(1, index.Count);
        }
    }
}
