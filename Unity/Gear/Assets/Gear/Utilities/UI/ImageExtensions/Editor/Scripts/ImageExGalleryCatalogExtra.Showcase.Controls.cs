using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.EditorTools.ImageExtensions
{
    internal static partial class ImageExGalleryCatalogExtra
    {
        internal static void PopulateControls(RectTransform content)
        {
            AddSwitchCard(content);
            AddRangeSliderCard(content);
            AddDropdownCard(content);
        }

        /// <summary>开关：点击切换，把手滑动 + 背景换色。</summary>
        private static void AddSwitchCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Switch", "点击切换开关（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx track = CreateTile(preview.transform, "Switch Track", new Vector2(-100f, 0f),
                new Vector2(76f, 38f), new Color(0.2f, 0.22f, 0.28f, 1f));
            track.Rectangle.CornerRadius = new Vector4(19f, 19f, 19f, 19f);
            track.raycastTarget = true;

            ImageEx handle = CreateTile(track.transform, "Handle", new Vector2(-17f, 0f),
                new Vector2(30f, 30f), Color.white);
            handle.Rectangle.CornerRadius = new Vector4(15f, 15f, 15f, 15f);

            GearSwitch toggle = track.gameObject.AddComponent<GearSwitch>();
            toggle.Background = track;
            toggle.Handle = (RectTransform)handle.transform;
            toggle.OffPosition = new Vector2(-17f, 0f);
            toggle.OnPosition = new Vector2(17f, 0f);
            toggle.OffColor = new Color(0.2f, 0.22f, 0.28f, 1f);
            toggle.OnColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        }

        /// <summary>区间滑杆：拖动最近把手，保持 low ≤ high。</summary>
        private static void AddRangeSliderCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · RangeSlider", "拖动把手选择区间（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx track = CreateTile(preview.transform, "Track", new Vector2(0f, -20f),
                new Vector2(240f, 10f), new Color(0.2f, 0.22f, 0.28f, 1f));
            track.Rectangle.CornerRadius = new Vector4(5f, 5f, 5f, 5f);
            track.raycastTarget = true;

            ImageEx fill = CreateTile(track.transform, "Fill", Vector2.zero,
                new Vector2(0f, 10f), new Color(0.49f, 0.36f, 0.99f, 1f));
            fill.Rectangle.CornerRadius = new Vector4(5f, 5f, 5f, 5f);

            ImageEx low = CreateTile(track.transform, "Low", new Vector2(-60f, 0f),
                new Vector2(20f, 20f), Color.white);
            low.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);

            ImageEx high = CreateTile(track.transform, "High", new Vector2(60f, 0f),
                new Vector2(20f, 20f), Color.white);
            high.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);

            GearRangeSlider slider = track.gameObject.AddComponent<GearRangeSlider>();
            slider.Track = (RectTransform)track.transform;
            slider.Fill = (RectTransform)fill.transform;
            slider.LowHandle = (RectTransform)low.transform;
            slider.HighHandle = (RectTransform)high.transform;
            slider.SetMinMax(0f, 100f);
            slider.SetValues(25f, 75f, false);
        }

        /// <summary>下拉选择：点击展开 Popover 选项（Play 模式）。</summary>
        private static void AddDropdownCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Dropdown",
                "点击展开选项（Play 模式）", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(10f, 10f, 10f, 10f) };
                p.color = new Color(0.11f, 0.12f, 0.17f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.14f);
            });

            if (preview == null) return;

            Button trigger = preview.gameObject.AddComponent<Button>();
            trigger.transition = Selectable.Transition.None;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "中";
            label.fontSize = 16f;
            label.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, 0f);

            GearDropdown dropdown = preview.gameObject.AddComponent<GearDropdown>();
            dropdown.Label = label;
            dropdown.Trigger = trigger;
            dropdown.SetOptions(new[] { "低", "中", "高" });
            dropdown.SetValueWithoutNotify(1);
        }

    }
}
