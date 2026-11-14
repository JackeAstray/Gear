using System.Collections.Generic;
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
            AddRadioGroupCard(content);
            AddQuantityCard(content);
            AddPickerWheelCard(content);
            AddTimePickerCard(content);
            AddTimerCard(content);
            AddOffScreenCard(content);
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
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx field = CreateTile(preview.transform, "Field", Vector2.zero,
                new Vector2(240f, 46f), new Color(0.11f, 0.12f, 0.17f, 1f));
            field.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);
            field.OutlineWidth = 1f;
            field.OutlineColor = new Color(1f, 1f, 1f, 0.14f);
            field.raycastTarget = true;

            Button trigger = field.gameObject.AddComponent<Button>();
            trigger.transition = Selectable.Transition.None;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "中";
            label.fontSize = 15f;
            label.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-38f, 0f);

            ImageEx caret = CreateTile(field.transform, "Caret", new Vector2(102f, 0f),
                new Vector2(11f, 9f), new Color(0.62f, 0.65f, 0.73f, 1f));
            caret.DrawShape = DrawShape.Triangle;

            GearDropdown dropdown = field.gameObject.AddComponent<GearDropdown>();
            dropdown.Label = label;
            dropdown.Trigger = trigger;
            dropdown.SetOptions(new[] { "低", "中", "高" });
            dropdown.SetValueWithoutNotify(1);
        }

        /// <summary>单选组：三选一，选中项换色。</summary>
        private static void AddRadioGroupCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · RadioGroup", "三选一，选中项高亮（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            var options = new List<GearRadioGroup.Option>();
            float[] xs = { -96f, 0f, 96f };
            for (int i = 0; i < xs.Length; i++)
            {
                ImageEx tile = CreateTile(preview.transform, "Option " + (i + 1),
                    new Vector2(xs[i], 0f), new Vector2(84f, 56f), new Color(0.16f, 0.18f, 0.25f));
                tile.Rectangle.CornerRadius = new Vector4(12f, 12f, 12f, 12f);
                tile.raycastTarget = true;

                Button button = tile.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                options.Add(new GearRadioGroup.Option { button = button, visual = tile });
            }

            GearRadioGroup group = preview.gameObject.AddComponent<GearRadioGroup>();
            group.Configure(options, 1);
        }

        /// <summary>数量选择：减号 / 数值 / 加号。</summary>
        private static void AddQuantityCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Quantity", "加减数量，到达边界禁用按钮（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx minus = CreateTile(preview.transform, "Minus", new Vector2(-90f, 0f),
                new Vector2(38f, 38f), new Color(0.16f, 0.18f, 0.25f));
            minus.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);
            minus.raycastTarget = true;
            Button minusButton = minus.gameObject.AddComponent<Button>();
            minusButton.transition = Selectable.Transition.None;

            ImageEx plus = CreateTile(preview.transform, "Plus", new Vector2(90f, 0f),
                new Vector2(38f, 38f), new Color(0.49f, 0.36f, 0.99f, 1f));
            plus.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);
            plus.raycastTarget = true;
            Button plusButton = plus.gameObject.AddComponent<Button>();
            plusButton.transition = Selectable.Transition.None;

            GameObject labelGo = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "3";
            label.fontSize = 24f;
            label.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            ((RectTransform)labelGo.transform).sizeDelta = new Vector2(80f, 40f);

            GearQuantitySelector selector = preview.gameObject.AddComponent<GearQuantitySelector>();
            selector.MinusButton = minusButton;
            selector.PlusButton = plusButton;
            selector.Label = label;
            selector.Configure(0, 9, 3);
        }

        /// <summary>滚轮选择：竖向拖动 / 滚轮，松手吸附最近项（循环）。</summary>
        private static void AddPickerWheelCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · PickerWheel", "上下拖动或滚轮，松手吸附（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            CreateWheelColumn(preview.transform, "Wheel", Vector2.zero, new Vector2(170f, 150f),
                new[] { "01", "02", "03", "04", "05" }, 40f, true);
        }

        /// <summary>时间选择：时 / 分 / AM-PM 三列滚轮联动。</summary>
        private static void AddTimePickerCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · TimePicker", "三列滚轮联动，12/24 小时制（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            var hours = new string[12];
            for (int i = 0; i < hours.Length; i++) hours[i] = (i + 1).ToString("00");

            var minutes = new string[60];
            for (int i = 0; i < minutes.Length; i++) minutes[i] = i.ToString("00");

            GearPickerWheel hourWheel = CreateWheelColumn(preview.transform, "Hour Column",
                new Vector2(-72f, -10f), new Vector2(58f, 112f), hours, 32f, true);
            GearPickerWheel minuteWheel = CreateWheelColumn(preview.transform, "Minute Column",
                new Vector2(0f, -10f), new Vector2(58f, 112f), minutes, 32f, true);
            GearPickerWheel meridiemWheel = CreateWheelColumn(preview.transform, "Meridiem Column",
                new Vector2(72f, -10f), new Vector2(58f, 112f), new[] { "AM", "PM" }, 32f, false);

            GameObject labelGo = new GameObject("Time Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI timeLabel = labelGo.GetComponent<TextMeshProUGUI>();
            timeLabel.fontSize = 18f;
            timeLabel.alignment = TextAlignmentOptions.Center;
            timeLabel.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            timeLabel.raycastTarget = false;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.sizeDelta = new Vector2(180f, 24f);
            labelRect.anchoredPosition = new Vector2(0f, 66f);

            GearTimePicker picker = preview.gameObject.AddComponent<GearTimePicker>();
            picker.HourWheel = hourWheel;
            picker.MinuteWheel = minuteWheel;
            picker.MeridiemWheel = meridiemWheel;
            picker.Label = timeLabel;
            picker.Use24Hour = false;
            picker.Format = "hh:mm tt";
            picker.SetTime(9, 30, 0);
        }

        /// <summary>计时器：倒计时文本（Play 模式自动走秒）。</summary>
        private static void AddTimerCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Timer", "倒计时 / 秒表，格式化文本（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx field = CreateTile(preview.transform, "Field", new Vector2(0f, -6f),
                new Vector2(190f, 64f), new Color(0.11f, 0.12f, 0.17f, 1f));
            field.Rectangle.CornerRadius = new Vector4(12f, 12f, 12f, 12f);
            field.OutlineWidth = 1f;
            field.OutlineColor = new Color(1f, 1f, 1f, 0.14f);

            GameObject labelGo = new GameObject("Clock", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI clock = labelGo.GetComponent<TextMeshProUGUI>();
            clock.text = "00:30";
            clock.fontSize = 28f;
            clock.alignment = TextAlignmentOptions.Center;
            clock.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            clock.raycastTarget = false;
            ((RectTransform)labelGo.transform).sizeDelta = new Vector2(190f, 40f);

            GearTimer timer = preview.gameObject.AddComponent<GearTimer>();
            timer.Label = clock;
            timer.Configure(GearTimerMode.Countdown, 30f, true, "mm:ss");
            timer.Restart();
        }

        /// <summary>出屏指示：目标离开视野时贴边箭头 + 距离。</summary>
        private static void AddOffScreenCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · OffScreen", "目标出屏时贴边箭头 + 距离（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx screen = CreateTile(preview.transform, "Screen", Vector2.zero,
                new Vector2(210f, 150f), new Color(0.09f, 0.1f, 0.14f, 1f));
            screen.Rectangle.CornerRadius = new Vector4(14f, 14f, 14f, 14f);

            ImageEx arrow = CreateTile(screen.transform, "Arrow", new Vector2(84f, 0f),
                new Vector2(20f, 24f), new Color(0.49f, 0.36f, 0.99f, 1f));
            arrow.DrawShape = DrawShape.Triangle;
            arrow.transform.localEulerAngles = new Vector3(0f, 0f, -90f);

            GameObject labelGo = new GameObject("Distance", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(screen.transform, false);
            TextMeshProUGUI distance = labelGo.GetComponent<TextMeshProUGUI>();
            distance.text = "24 m";
            distance.fontSize = 14f;
            distance.alignment = TextAlignmentOptions.Right;
            distance.color = new Color(0.62f, 0.65f, 0.73f, 1f);
            distance.raycastTarget = false;
            RectTransform distanceRect = (RectTransform)labelGo.transform;
            distanceRect.sizeDelta = new Vector2(70f, 20f);
            distanceRect.anchoredPosition = new Vector2(40f, -32f);

            GearOffScreenIndicator indicator = preview.gameObject.AddComponent<GearOffScreenIndicator>();
            indicator.Indicator = (RectTransform)arrow.transform;
            indicator.Area = (RectTransform)screen.transform;
            indicator.DistanceLabel = distance;
        }

        /// <summary>滚轮列：视口 + RectMask2D + 行标签，返回配置好的 <see cref="GearPickerWheel"/>。</summary>
        private static GearPickerWheel CreateWheelColumn(Transform parent, string name, Vector2 position,
            Vector2 size, string[] labels, float itemHeight, bool wrap)
        {
            ImageEx viewport = CreateTile(parent, name, position, size, new Color(0.09f, 0.1f, 0.14f, 1f));
            viewport.Rectangle.CornerRadius = new Vector4(12f, 12f, 12f, 12f);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            GameObject contentGo = new GameObject(name + " Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            RectTransform wheelContent = (RectTransform)contentGo.transform;
            wheelContent.anchorMin = new Vector2(0.5f, 0.5f);
            wheelContent.anchorMax = new Vector2(0.5f, 0.5f);
            wheelContent.pivot = new Vector2(0.5f, 0.5f);
            wheelContent.sizeDelta = size;

            for (int i = 0; i < labels.Length; i++)
            {
                ImageEx row = CreateTile(wheelContent, "Row " + i, new Vector2(0f, -i * itemHeight),
                    new Vector2(size.x - 10f, itemHeight - 6f), new Color(0.16f, 0.18f, 0.25f, 1f));
                row.Rectangle.CornerRadius = new Vector4(8f, 8f, 8f, 8f);
                row.raycastTarget = false;

                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(row.transform, false);
                TextMeshProUGUI rowLabel = labelGo.GetComponent<TextMeshProUGUI>();
                rowLabel.text = labels[i];
                rowLabel.fontSize = 16f;
                rowLabel.alignment = TextAlignmentOptions.Center;
                rowLabel.color = new Color(0.94f, 0.95f, 0.98f, 1f);
                rowLabel.raycastTarget = false;
                ((RectTransform)labelGo.transform).sizeDelta = new Vector2(size.x - 10f, itemHeight - 6f);
            }

            GearPickerWheel wheel = viewport.gameObject.AddComponent<GearPickerWheel>();
            wheel.Content = wheelContent;
            wheel.ItemHeight = itemHeight;
            wheel.Wrap = wrap;
            wheel.SetOptions(labels, false);
            wheel.SnapTo(0);
            return wheel;
        }
    }
}
