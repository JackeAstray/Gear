using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>Pack C 后段控件卡（滚轮 / 计时 / 出屏 / 输入框 / 多选 / 日历 / 取色）。</summary>
    internal static partial class ImageExGalleryCatalogExtra
    {
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

        /// <summary>输入框：Enter 提交、长度计数、IME 组合保护。</summary>
        private static void AddInputFieldCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · InputField", "Enter 提交、长度计数（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx field = CreateTile(preview.transform, "Field", new Vector2(0f, 12f),
                new Vector2(230f, 46f), new Color(0.11f, 0.12f, 0.17f, 1f));
            field.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);
            field.OutlineWidth = 1f;
            field.OutlineColor = new Color(1f, 1f, 1f, 0.14f);
            field.raycastTarget = true;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = 14f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            text.raycastTarget = false;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 0f);
            textRect.offsetMax = new Vector2(-14f, 0f);

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholder.text = "输入后按 Enter";
            placeholder.fontSize = 14f;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.color = new Color(0.62f, 0.65f, 0.73f, 0.7f);
            placeholder.raycastTarget = false;
            RectTransform placeholderRect = (RectTransform)placeholderGo.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(14f, 0f);
            placeholderRect.offsetMax = new Vector2(-14f, 0f);

            TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = (RectTransform)field.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.pointSize = 14f;

            GameObject counterGo = new GameObject("Counter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            counterGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI counter = counterGo.GetComponent<TextMeshProUGUI>();
            counter.text = "0/16";
            counter.fontSize = 12f;
            counter.alignment = TextAlignmentOptions.Right;
            counter.color = new Color(0.62f, 0.65f, 0.73f, 1f);
            counter.raycastTarget = false;
            RectTransform counterRect = (RectTransform)counterGo.transform;
            counterRect.sizeDelta = new Vector2(120f, 18f);
            counterRect.anchoredPosition = new Vector2(55f, -20f);

            GearInputFieldEnhancer enhancer = preview.gameObject.AddComponent<GearInputFieldEnhancer>();
            enhancer.InputField = input;
            enhancer.CounterLabel = counter;
            enhancer.MaxLength = 16;
        }

        /// <summary>多选下拉：勾选集合 + 确认。</summary>
        private static void AddMultiSelectCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · MultiSelect", "勾选多项后点确认（Play 模式）", p =>
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
            label.text = "2 项已选";
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

            GearMultiSelect multi = preview.gameObject.AddComponent<GearMultiSelect>();
            multi.Trigger = trigger;
            multi.Label = label;
            multi.SetOptions(new[] { "攻击", "防御", "速度", "暴击", "生命" });
            multi.SetSelected(1, true, false);
            multi.SetSelected(3, true, false);
        }

        /// <summary>月视图日历：今天/选中标记，可翻月。</summary>
        private static void AddCalendarCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Calendar", "月视图、翻月、今天/选中（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI header = headerGo.GetComponent<TextMeshProUGUI>();
            header.text = "2026 年 11 月";
            header.fontSize = 16f;
            header.alignment = TextAlignmentOptions.Center;
            header.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            header.raycastTarget = false;
            RectTransform headerRect = (RectTransform)headerGo.transform;
            headerRect.sizeDelta = new Vector2(220f, 22f);
            headerRect.anchoredPosition = new Vector2(0f, 72f);

            GameObject gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(preview.transform, false);
            RectTransform grid = (RectTransform)gridGo.transform;
            grid.sizeDelta = new Vector2(232f, 144f);
            grid.anchoredPosition = new Vector2(0f, -6f);

            GridLayoutGroup layout = gridGo.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(30f, 20f);
            layout.spacing = new Vector2(3f, 3f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 7;
            layout.childAlignment = TextAnchor.UpperCenter;

            ImageEx cell = CreateTile(grid, "Day Template", Vector2.zero,
                new Vector2(30f, 20f), new Color(0.16f, 0.18f, 0.25f, 1f));
            cell.Rectangle.CornerRadius = new Vector4(4f, 4f, 4f, 4f);
            cell.raycastTarget = true;

            GameObject dayLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            dayLabelGo.transform.SetParent(cell.transform, false);
            TextMeshProUGUI dayLabel = dayLabelGo.GetComponent<TextMeshProUGUI>();
            dayLabel.text = "1";
            dayLabel.fontSize = 11f;
            dayLabel.alignment = TextAlignmentOptions.Center;
            dayLabel.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            dayLabel.raycastTarget = false;
            ((RectTransform)dayLabelGo.transform).sizeDelta = new Vector2(30f, 20f);

            Button dayButton = cell.gameObject.AddComponent<Button>();
            dayButton.transition = Selectable.Transition.None;
            cell.gameObject.SetActive(false);

            GearCalendar calendar = preview.gameObject.AddComponent<GearCalendar>();
            calendar.HeaderLabel = header;
            calendar.GridContent = grid;
            calendar.DayTemplate = dayButton;
            calendar.SetVisibleMonth(2026, 11);
            calendar.SelectDate(new System.DateTime(2026, 11, 14), false);
            calendar.Refresh();
        }

        /// <summary>取色器：SV 方块 + Hue 滑条 + 预览。</summary>
        private static void AddColorPickerCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · ColorPicker", "拖动 SV 方块 / Hue 滑条取色（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx swatch = CreateTile(preview.transform, "Preview", new Vector2(-95f, 62f),
                new Vector2(24f, 24f), Color.red);
            swatch.Rectangle.CornerRadius = new Vector4(6f, 6f, 6f, 6f);

            ImageEx square = CreateTile(preview.transform, "SV Area", new Vector2(0f, 18f),
                new Vector2(132f, 132f), Color.white);
            square.Rectangle.CornerRadius = new Vector4(8f, 8f, 8f, 8f);
            square.raycastTarget = true;
            var squareGradient = square.GradientEffect;
            squareGradient.Enabled = true;
            squareGradient.GradientType = GradientType.Linear;
            squareGradient.Rotation = 0f;
            squareGradient.Speed = 0f;
            squareGradient.Gradient = ImageExGalleryBuilder.MakeGradient(Color.white, new Color(1f, 0f, 0f, 1f));
            square.GradientEffect = squareGradient;
            square.color = Color.white;

            ImageEx handle = CreateTile(square.transform, "Handle", Vector2.zero,
                new Vector2(14f, 14f), Color.white);
            handle.DrawShape = DrawShape.Circle;
            handle.OutlineWidth = 2f;
            handle.OutlineColor = new Color(0f, 0f, 0f, 0.6f);

            ImageEx track = CreateTile(preview.transform, "Hue", new Vector2(0f, -58f),
                new Vector2(170f, 12f), Color.white);
            track.Rectangle.CornerRadius = new Vector4(6f, 6f, 6f, 6f);
            track.raycastTarget = true;
            var trackGradient = track.GradientEffect;
            trackGradient.Enabled = true;
            trackGradient.GradientType = GradientType.Linear;
            trackGradient.Rotation = 0f;
            trackGradient.Speed = 0f;
            trackGradient.Gradient = MakeHueGradient();
            track.GradientEffect = trackGradient;
            track.color = Color.white;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            fillGo.transform.SetParent(track.transform, false);
            ImageEx fill = fillGo.GetComponent<ImageEx>();
            fill.color = new Color(1f, 1f, 1f, 0.15f);
            fill.raycastTarget = false;
            RectTransform fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            ImageEx hueHandle = CreateTile(track.transform, "Hue Handle", Vector2.zero,
                new Vector2(12f, 18f), Color.white);
            hueHandle.DrawShape = DrawShape.Circle;
            hueHandle.OutlineWidth = 2f;
            hueHandle.OutlineColor = new Color(0f, 0f, 0f, 0.6f);

            Slider hue = track.gameObject.AddComponent<Slider>();
            hue.transition = Selectable.Transition.None;
            hue.fillRect = fillRect;
            hue.handleRect = (RectTransform)hueHandle.transform;
            hue.targetGraphic = hueHandle;
            hue.direction = Slider.Direction.LeftToRight;
            hue.minValue = 0f;
            hue.maxValue = 1f;
            hue.SetValueWithoutNotify(0f);

            GearColorPicker picker = square.gameObject.AddComponent<GearColorPicker>();
            picker.SvArea = (RectTransform)square.transform;
            picker.SvHandle = (RectTransform)handle.transform;
            picker.HueSlider = hue;
            picker.Preview = swatch;
            picker.SetColor(Color.red);
        }

        private static Gradient MakeHueGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 1f / 6f),
                    new GradientColorKey(Color.green, 2f / 6f),
                    new GradientColorKey(Color.cyan, 3f / 6f),
                    new GradientColorKey(Color.blue, 4f / 6f),
                    new GradientColorKey(Color.magenta, 5f / 6f),
                    new GradientColorKey(Color.red, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
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
