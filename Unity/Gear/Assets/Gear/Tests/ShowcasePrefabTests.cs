using Gear.UI.Showcase;
using Gear.Utilities.UI.Backdrop;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using Gear.Utilities.UI.Interaction;
using Gear.Utilities.UI.Motion;
using Gear.Utilities.UI.Navigation;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    /// <summary>
    /// 窗口/样例必须是可直接编辑的 prefab，而不是运行时空根。
    /// </summary>
    public sealed class ShowcasePrefabTests
    {
        [Test]
        public void ShowcaseWindow_HasAuthoredChrome()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UIs/ShowcaseUIPlane");
            Assert.IsNotNull(prefab, "ShowcaseUIPlane.prefab 不存在");
            Assert.IsNotNull(prefab.transform.Find("Header"), "Header 未烘焙");
            Assert.IsNotNull(prefab.transform.Find("Header/Search"), "Search 未烘焙");
            Assert.IsNotNull(prefab.transform.Find("Header/Theme"), "Theme 按钮未烘焙");
            Assert.IsNotNull(prefab.transform.Find("Grid"), "Grid 未烘焙");
            Assert.IsNotNull(prefab.transform.Find("Grid").GetComponent<GearAnimatedContainer>(),
                "Grid 缺少错峰入场");
            Assert.IsNotNull(prefab.transform.Find("Footer"), "Footer 未烘焙");

            Transform tabs = prefab.transform.Find("Tabs");
            Assert.IsNotNull(tabs);
            Assert.AreEqual(8, tabs.childCount, "8 个分类 Tab 应直接存在于 prefab");
            for (int i = 0; i < tabs.childCount; i++)
            {
                GameObject tab = tabs.GetChild(i).gameObject;
                Assert.IsNotNull(tab.GetComponent<GearInteractive>(), "Tab 缺少五态 Interactive");
                Assert.IsNotNull(tab.GetComponent<GearInteractiveTint>(), "Tab 缺少五态 Tint");
                Assert.AreEqual(Selectable.Transition.None, tab.GetComponent<Button>().transition,
                    "Tab 应关闭 Button ColorTint，避免与五态 Tint 叠加");
            }

            string[] buttons =
            {
                "Header/Theme", "Header/Motion", "Header/SampleAudio", "Header/SampleParty",
                "Header/SampleInventory", "Header/Close", "Footer/Prev", "Footer/Next",
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                Transform button = prefab.transform.Find(buttons[i]);
                Assert.IsNotNull(button, buttons[i] + " 未烘焙");
                Assert.IsNotNull(button.GetComponent<GearInteractive>(), buttons[i] + " 缺少五态 Interactive");
                Assert.IsNotNull(button.GetComponent<GearInteractiveTint>(), buttons[i] + " 缺少五态 Tint");
                Assert.AreEqual(Selectable.Transition.None, button.GetComponent<Button>().transition);
            }
        }

        [Test]
        public void DetailWindow_HasPreviewHost()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UIs/ShowcaseDetailUIPlane");
            Assert.IsNotNull(prefab, "ShowcaseDetailUIPlane.prefab 不存在");
            Assert.IsNotNull(prefab.transform.Find("Panel/Title"));
            Assert.IsNotNull(prefab.transform.Find("Panel/Preview/Card Host"), "详情预览区未烘焙");
            Assert.IsNotNull(prefab.transform.Find("Panel/Close").GetComponent<GearThemeColorTarget>());
            Assert.IsNotNull(prefab.transform.Find("Panel/Back").GetComponent<GearThemeColorTarget>());
            Assert.IsNotNull(prefab.transform.Find("Panel/Customize"), "详情缺少 FX 按钮");
            Assert.IsNotNull(prefab.transform.Find("Panel/Customizer").GetComponent<ShowcaseCustomizer>(),
                "详情缺少 Customizer");
        }

        [Test]
        public void BackdropBlurCard_UsesSharedCapture()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/shape/157-组合---BackdropBlur.prefab");
            Assert.IsNotNull(prefab, "BackdropBlur 卡不存在");
            var blur = prefab.GetComponentInChildren<UIBackdropBlur>(true);
            Assert.IsNotNull(blur);
            Assert.IsTrue(blur.UseSharedCapture);
        }

        [Test]
        public void GooeySample_HasInteractiveBlob()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/175-样例---GooeyMenu.prefab");
            Assert.IsNotNull(prefab, "GooeyMenu 样例不存在");
            Assert.IsNotNull(prefab.GetComponentInChildren<GearInteractive>(true));
        }

        [Test]
        public void ActivityRingsSample_HasRadialRings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/176-样例---ActivityRings.prefab");
            Assert.IsNotNull(prefab, "ActivityRings 样例不存在");
            ImageEx[] images = prefab.GetComponentsInChildren<ImageEx>(true);
            int rings = 0;
            for (int i = 0; i < images.Length; i++)
                if (images[i].Clip == ImageEx.ClipMode.Radial) rings++;
            Assert.GreaterOrEqual(rings, 3);
        }

        [Test]
        public void ThreeDKeysSample_HasInnerShadow()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/177-样例---3DKeys.prefab");
            Assert.IsNotNull(prefab, "3DKeys 样例不存在");
            ImageEx[] images = prefab.GetComponentsInChildren<ImageEx>(true);
            bool found = false;
            for (int i = 0; i < images.Length; i++)
                if (images[i].EnableInnerShadow) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void WallpaperSample_HasAnimatedPattern()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/178-样例---Wallpaper.prefab");
            Assert.IsNotNull(prefab, "Wallpaper 样例不存在");
            ImageEx[] images = prefab.GetComponentsInChildren<ImageEx>(true);
            bool found = false;
            for (int i = 0; i < images.Length; i++)
                if (images[i].Pattern != ImageEx.PatternMode.None && images[i].PatternSpeed != Vector2.zero) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void PatternSourceCard_UsesShapeSource()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/174-效果---PatternSource.prefab");
            Assert.IsNotNull(prefab, "PatternSource 卡不存在");
            var main = prefab.transform.Find("PreviewClip/Preview/PatternMain");
            Assert.IsNotNull(main);
            var image = main.GetComponent<ImageEx>();
            Assert.IsNotNull(image);
            Assert.AreEqual(ImageEx.PatternSource.Shape, image.PatternSourceType);
            Assert.AreEqual(ImageEx.PatternMode.Dots, image.Pattern);
        }

        [Test]
        public void ShapeOperandCard_HasSubtractTarget()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/173-效果---ShapeOperand.prefab");
            Assert.IsNotNull(prefab, "ShapeOperand 卡不存在");
            var mainGo = prefab.transform.Find("PreviewClip/Preview/Main");
            Assert.IsNotNull(mainGo, "Main 形状不存在");
            var main = mainGo.GetComponent<ImageEx>();
            Assert.IsNotNull(main);
            Assert.IsNotNull(main.Operand.target);
            Assert.AreEqual(ShapeOperandOperation.Subtract, main.Operand.operation);
        }

        [Test]
        public void InteractionEffectsCard_HasBusAndAdapters()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/172-控件---InteractionEffects.prefab");
            Assert.IsNotNull(prefab, "InteractionEffects 卡不存在");
            Assert.IsNotNull(prefab.GetComponentInChildren<GearInteractionEffectBus>(true));
            int effectCount = 0;
            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IGearInteractionEffect) effectCount++;
            }
            Assert.AreEqual(3, effectCount);
        }

        [Test]
        public void ReorderableCard_HasItemsAndAdapters()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/170-控件---Reorderable.prefab");
            Assert.IsNotNull(prefab, "Reorderable 卡不存在");
            var list = prefab.GetComponent<GearReorderableList>();
            Assert.IsNotNull(list);
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(4, prefab.GetComponentsInChildren<GearReorderableItem>(true).Length);
        }

        [Test]
        public void MarqueeCard_HasViewportAndContent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/168-控件---Marquee.prefab");
            Assert.IsNotNull(prefab, "Marquee 卡不存在");
            var marquee = prefab.GetComponent<GearMarquee>();
            Assert.IsNotNull(marquee);
            Assert.IsNotNull(marquee.Viewport);
            Assert.IsNotNull(marquee.Content);
        }

        [Test]
        public void ComparisonSliderCard_HasTrackAndHandles()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/169-控件---ComparisonSlider.prefab");
            Assert.IsNotNull(prefab, "ComparisonSlider 卡不存在");
            var slider = prefab.GetComponent<GearComparisonSlider>();
            Assert.IsNotNull(slider);
            Assert.IsNotNull(slider.Track);
            Assert.IsNotNull(slider.Before);
            Assert.IsNotNull(slider.After);
            Assert.IsNotNull(slider.Handle);
        }

        [Test]
        public void SelectorCard_HasOptionsAndIndicator()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/165-控件---Selector.prefab");
            Assert.IsNotNull(prefab, "Selector 卡不存在");
            var selector = prefab.GetComponent<GearSelector>();
            Assert.IsNotNull(selector);
            Assert.AreEqual(3, selector.Options.Count);
            Assert.IsNotNull(selector.Indicator);
        }

        [Test]
        public void RectDraggerCard_HasHandleAndBounds()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/166-控件---RectDragger.prefab");
            Assert.IsNotNull(prefab, "RectDragger 卡不存在");
            var dragger = prefab.GetComponentInChildren<GearRectDragger>(true);
            Assert.IsNotNull(dragger);
            Assert.IsNotNull(dragger.Bounds);
            Assert.IsTrue(dragger.SpringBack);
        }

        [Test]
        public void ButtonGroupDimmerCard_HasMembers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/167-控件---ButtonGroupDimmer.prefab");
            Assert.IsNotNull(prefab, "ButtonGroupDimmer 卡不存在");
            var dimmer = prefab.GetComponentInChildren<GearButtonGroupDimmer>(true);
            Assert.IsNotNull(dimmer);
            Assert.AreEqual(3, dimmer.Members.Count);
        }

        [Test]
        public void FlexibleGridCard_HasResponsiveMasonry()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/164-控件---FlexibleGrid.prefab");
            Assert.IsNotNull(prefab, "FlexibleGrid 卡不存在");
            var grid = prefab.GetComponentInChildren<GearFlexibleGridLayout>(true);
            Assert.IsNotNull(grid);
            Assert.AreEqual(GearFlexibleGridLayout.FitType.Responsive, grid.Fit);
            Assert.IsTrue(grid.Masonry);
            Assert.GreaterOrEqual(prefab.GetComponentsInChildren<LayoutElement>(true).Length, 8);
        }

        [Test]
        public void CarouselCard_HasThreePagesAndAutoSlide()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/163-控件---Carousel.prefab");
            Assert.IsNotNull(prefab, "Carousel 卡不存在");
            var carousel = prefab.GetComponent<GearCarousel>();
            var view = prefab.GetComponent<GearPageView>();
            Assert.IsNotNull(carousel);
            Assert.IsNotNull(view);
            Assert.AreEqual(3, view.Pages.Count);
            Assert.IsTrue(carousel.AutoSlide);
        }

        [Test]
        public void CalendarCard_HasHeaderZoom()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/132-控件---Calendar.prefab");
            Assert.IsNotNull(prefab, "Calendar 卡不存在");
            var calendar = prefab.GetComponentInChildren<GearCalendar>(true);
            Assert.IsNotNull(calendar);
            Assert.IsNotNull(calendar.ZoomOutButton);
        }

        [Test]
        public void RippleCard_HasEightWaveComponent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/162-效果---Ripple.prefab");
            Assert.IsNotNull(prefab, "Ripple 卡不存在");
            Assert.IsNotNull(prefab.GetComponentInChildren<GearRipple>(true));
        }

        [Test]
        public void ShowcasePanelCard_HasThreePagesAndAutoPlay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/161-控件---ShowcasePanel.prefab");
            Assert.IsNotNull(prefab, "ShowcasePanel 卡不存在");
            var panel = prefab.GetComponent<GearShowcasePanel>();
            var view = prefab.GetComponent<GearPageView>();
            Assert.IsNotNull(panel);
            Assert.IsNotNull(view);
            Assert.AreEqual(3, view.Pages.Count);
            Assert.AreEqual(3, panel.Items.Count);
            Assert.IsTrue(panel.AutoPlay);
        }

        [Test]
        public void ColorPaletteCard_HasTwelveSwatches()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/160-控件---ColorPalette.prefab");
            Assert.IsNotNull(prefab, "ColorPalette 卡不存在");
            var palette = prefab.GetComponent<GearColorPalette>();
            Assert.IsNotNull(palette);
            Assert.AreEqual(12, prefab.GetComponentsInChildren<GearColorSwatch>(true).Length);
        }

        [Test]
        public void ColorWheelCard_HasWheelComponent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/159-控件---ColorWheel.prefab");
            Assert.IsNotNull(prefab, "ColorWheel 卡不存在");
            Assert.IsNotNull(prefab.GetComponent<GearColorWheel>());
            Assert.AreEqual(256, prefab.GetComponent<GearColorWheel>().TextureSize);
        }

        [Test]
        public void ProgressButtonCard_HasProgressComponent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/widgets/158-控件---ProgressButton.prefab");
            Assert.IsNotNull(prefab, "ProgressButton 卡不存在");
            Assert.IsNotNull(prefab.GetComponent<ShowcaseProgressDemo>());
            Assert.IsNotNull(prefab.GetComponentInChildren<GearProgressButton>(true));
        }

        [Test]
        public void CardChrome_HasThemeTargets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Gear/UI/Showcase/Cards" });
            int cards = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null) continue;
                cards++;
                GearThemeColorTarget root = prefab.GetComponent<GearThemeColorTarget>();
                Assert.IsNotNull(root, prefab.name + " 缺少卡面主题目标");
                Assert.AreEqual("Surface", root.TokenId, prefab.name + " 卡面 token 应为 Surface");

                Transform title = prefab.transform.Find("CardTitle");
                Transform desc = prefab.transform.Find("CardDescription");
                Assert.IsNotNull(title?.GetComponent<GearThemeColorTarget>(), prefab.name + " 标题缺少主题目标");
                Assert.IsNotNull(desc?.GetComponent<GearThemeColorTarget>(), prefab.name + " 说明缺少主题目标");
                Assert.AreEqual("Text", title.GetComponent<GearThemeColorTarget>().TokenId);
                Assert.AreEqual("TextSecondary", desc.GetComponent<GearThemeColorTarget>().TokenId);
            }

            Assert.Greater(cards, 100, "应扫描到全部卡片 prefab");
        }

        [Test]
        public void CardButtons_HaveFiveState()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Gear/UI/Showcase/Cards" });
            int buttons = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null) continue;
                Button[] found = prefab.GetComponentsInChildren<Button>(true);
                for (int j = 0; j < found.Length; j++)
                {
                    buttons++;
                    Assert.IsNotNull(found[j].GetComponent<GearInteractive>(),
                        prefab.name + "/" + found[j].name + " 缺少 Interactive");
                    Assert.IsNotNull(found[j].GetComponent<GearInteractiveTint>(),
                        prefab.name + "/" + found[j].name + " 缺少 Tint");
                    Assert.AreEqual(Selectable.Transition.None, found[j].transition,
                        prefab.name + "/" + found[j].name + " 仍有 Button ColorTint");
                }
            }

            Assert.Greater(buttons, 0, "卡片里应至少有一个 Button");
        }

        [Test]
        public void CustomizerPrefab_HasFourSlidersAndReset()
        {
            GameObject prefab = Resources.Load<GameObject>("UI/Showcase/ShowcaseCustomizer");
            Assert.IsNotNull(prefab, "ShowcaseCustomizer.prefab 不存在");
            Assert.IsNotNull(prefab.GetComponent<ShowcaseCustomizer>());
            Assert.AreEqual(4, prefab.GetComponentsInChildren<Slider>(true).Length);
            Assert.IsNotNull(prefab.transform.Find("Reset"));
        }

        [Test]
        public void SampleWindows_HaveAuthoredContent()
        {
            string[] names =
            {
                "AudioMixerSampleUIPlane",
                "PartyLobbySampleUIPlane",
                "InventorySampleUIPlane",
            };

            for (int i = 0; i < names.Length; i++)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/UIs/" + names[i]);
                Assert.IsNotNull(prefab, names[i] + ".prefab 不存在");
                Assert.IsNotNull(prefab.transform.Find("Panel"), names[i] + " Panel 未烘焙");
                Assert.IsNotNull(prefab.transform.Find("Panel/Content"), names[i] + " Content 未烘焙");
            }

            AssertActionButton("AudioMixerSampleUIPlane", "Panel/Content/Master/Apply");
            AssertActionButton("AudioMixerSampleUIPlane", "Panel/Content/Master/Reset");
            AssertActionButton("PartyLobbySampleUIPlane", "Panel/Content/Settings/Start");
            AssertActionButton("InventorySampleUIPlane", "Panel/Content/Sort");
            AssertActionButton("InventorySampleUIPlane", "Panel/Content/Detail/Use");
            AssertActionButton("InventorySampleUIPlane", "Panel/Content/Detail/Equip");
            AssertActionButton("InventorySampleUIPlane", "Panel/Content/Detail/Discard");
        }

        [Test]
        public void InventoryItemCell_HasPrefabFiveState()
        {
            GameObject prefab = Resources.Load<GameObject>("UI/Showcase/InventoryItemCell");
            Assert.IsNotNull(prefab, "InventoryItemCell.prefab 不存在");
            Assert.IsNotNull(prefab.GetComponent<ImageEx>());
            Assert.IsNotNull(prefab.GetComponent<Button>());
            Assert.IsNotNull(prefab.GetComponent<GearInteractive>());
            Assert.IsNotNull(prefab.GetComponent<GearInteractiveTint>());
            Assert.AreEqual(Selectable.Transition.None, prefab.GetComponent<Button>().transition);
            Assert.IsNotNull(prefab.transform.Find("Icon"));
            Assert.IsNotNull(prefab.transform.Find("Name"));
            Assert.IsNotNull(prefab.transform.Find("Count"));
            Assert.IsNotNull(prefab.transform.Find("Rarity"));
        }

        [Test]
        public void ShowcaseGridCell_HasPrefabFiveState()
        {
            GameObject prefab = Resources.Load<GameObject>("UI/Showcase/ShowcaseGridCell");
            Assert.IsNotNull(prefab, "ShowcaseGridCell.prefab 不存在");
            Assert.IsNotNull(prefab.transform.Find("CardHost"), "格子缺少 CardHost");
            Assert.IsNotNull(prefab.GetComponent<GearInteractive>(), "格子缺少 hover Interactive");
            Transform hover = prefab.transform.Find("Hover");
            Assert.IsNotNull(hover, "格子缺少 hover overlay");
            Assert.IsNotNull(hover.GetComponent<ImageExStateColors>());
            Assert.IsNotNull(prefab.GetComponent<GearRectSway>(), "格子缺少 hover sway");
            Transform open = prefab.transform.Find("Open");
            Assert.IsNotNull(open, "格子缺少 Open 按钮");
            Assert.IsNotNull(open.GetComponent<GearInteractive>());
            Assert.IsNotNull(open.GetComponent<GearInteractiveTint>());
            Assert.AreEqual(Selectable.Transition.None, open.GetComponent<Button>().transition);
        }

        [Test]
        public void OverlayExtras_HavePrefabDemoComponents()
        {
            string[] paths =
            {
                "Assets/Gear/UI/Showcase/Cards/overlays/150-覆盖层---Toast.prefab",
                "Assets/Gear/UI/Showcase/Cards/overlays/151-覆盖层---Tooltip.prefab",
                "Assets/Gear/UI/Showcase/Cards/overlays/152-覆盖层---ContextMenu.prefab",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                Assert.IsNotNull(prefab, paths[i] + " 不存在");
                Assert.IsNotNull(prefab.GetComponent<ShowcaseExtrasDemo>(), "缺少覆盖层演示组件");
                Assert.IsNull(prefab.GetComponentInChildren<GearPopoverTrigger>(true),
                    "覆盖层卡不应残留 GearPopoverTrigger");
            }
        }

        [Test]
        public void BorderGlowCard_HasGlowEnabled()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/shape/153-组合---BorderGlow.prefab");
            Assert.IsNotNull(prefab, "BorderGlow 卡不存在");
            ImageEx preview = prefab.transform.Find("PreviewClip/Preview")?.GetComponent<ImageEx>();
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.EnableBorderGlow);
            Assert.Greater(preview.BorderGlowIntensity, 0.1f);
        }

        [Test]
        public void BackdropAndDualSoftMaskCards_HavePrefabs()
        {
            GameObject backdrop = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/shape/157-组合---BackdropBlur.prefab");
            Assert.IsNotNull(backdrop, "BackdropBlur 卡不存在");
            Assert.IsNotNull(backdrop.GetComponentInChildren<Gear.Utilities.UI.Backdrop.UIBackdropBlur>(true));

            GameObject mask = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Gear/UI/Showcase/Cards/effects/156-遮罩---双面板SoftMask.prefab");
            Assert.IsNotNull(mask, "双面板 SoftMask 卡不存在");
            Assert.GreaterOrEqual(mask.GetComponentsInChildren<GearSoftMask>(true).Length, 2);
        }

        private static void AssertActionButton(string prefabName, string path)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UIs/" + prefabName);
            Transform button = prefab != null ? prefab.transform.Find(path) : null;
            Assert.IsNotNull(button, prefabName + " 缺少按钮 " + path);
            Assert.IsNotNull(button.GetComponent<GearInteractive>(), path + " 缺少五态 Interactive");
            Assert.IsNotNull(button.GetComponent<GearInteractiveTint>(), path + " 缺少五态 Tint");
            Assert.AreEqual(Selectable.Transition.None, button.GetComponent<Button>().transition);
        }

        [Test]
        public void ShowcaseButtons_DoNotUseWhiteOnWhite()
        {
            string[] paths =
            {
                "Prefabs/UIs/ShowcaseUIPlane",
                "Prefabs/UIs/ShowcaseDetailUIPlane",
                "Prefabs/UIs/AudioMixerSampleUIPlane",
                "Prefabs/UIs/PartyLobbySampleUIPlane",
                "Prefabs/UIs/InventorySampleUIPlane",
                "UI/Showcase/ShowcaseGridCell",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = Resources.Load<GameObject>(paths[i]);
                if (prefab == null) continue;
                Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
                for (int j = 0; j < buttons.Length; j++)
                {
                    Graphic background = buttons[j].targetGraphic;
                    TMP_Text label = buttons[j].GetComponentInChildren<TMP_Text>(true);
                    if (background == null || label == null) continue;
                    Color bg = background.color;
                    Color text = label.color;
                    bool whiteOnWhite = bg.r > 0.9f && bg.g > 0.9f && bg.b > 0.9f
                        && text.r > 0.9f && text.g > 0.9f && text.b > 0.9f;
                    Assert.IsFalse(whiteOnWhite, paths[i] + "/" + buttons[j].name + " 是白底白字");
                }
            }
        }
    }
}
