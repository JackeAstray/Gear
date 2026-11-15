using System.Collections.Generic;
using Gear.UI.Showcase;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    /// <summary>
    /// 展厅目录的纯数据用例：空页裁剪、查找、搜索匹配。
    /// 不依赖场景/预制体，CI 的 EditMode 跑得起。
    /// </summary>
    public sealed class ShowcaseCatalogTests
    {
        private ShowcaseCatalog catalog;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null)
            {
                Object.DestroyImmediate(catalog);
                catalog = null;
            }
        }

        [Test]
        public void SetPages_SkipsEmptyPagesAndCountsEntries()
        {
            catalog = ScriptableObject.CreateInstance<ShowcaseCatalog>();
            ShowcasePage filled = new ShowcasePage
            {
                id = "shape",
                displayName = "形状",
                entries = new List<ShowcaseEntry>
                {
                    new ShowcaseEntry { id = "shape/0", title = "形状 · Circle", description = "圆", pageId = "shape" },
                },
            };
            ShowcasePage empty = new ShowcasePage
            {
                id = "misc",
                displayName = "其他",
                entries = new List<ShowcaseEntry>(),
            };

            catalog.SetPages(new[] { filled, empty });

            Assert.AreEqual(1, catalog.Pages.Count, "空页应被裁掉");
            Assert.AreEqual(1, catalog.TotalCount);
            Assert.IsNotNull(catalog.FindPage("shape"));
            Assert.IsNull(catalog.FindPage("misc"));
        }

        [Test]
        public void Matches_TitleDescriptionAndTags_AreCaseInsensitive()
        {
            ShowcaseEntry entry = new ShowcaseEntry
            {
                title = "渐变 · Linear",
                description = "45° 紫→青，自动流动",
                tags = new[] { "材质" },
            };

            Assert.IsTrue(ShowcaseCatalog.Matches(entry, "linear"));
            Assert.IsTrue(ShowcaseCatalog.Matches(entry, "45"));
            Assert.IsTrue(ShowcaseCatalog.Matches(entry, "材质"));
            Assert.IsTrue(ShowcaseCatalog.Matches(entry, "  "));
            Assert.IsFalse(ShowcaseCatalog.Matches(entry, "Progress"));
            Assert.IsFalse(ShowcaseCatalog.Matches(null, "Linear"));
        }
    }
}
