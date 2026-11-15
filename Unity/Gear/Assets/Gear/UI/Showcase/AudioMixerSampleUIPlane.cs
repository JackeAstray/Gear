using System.Collections.Generic;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 整屏样例 1：Audio Mixer。
    /// 4 个通道（音量/静音/电平）+ 主输出 + 预设，全部用 Gear 现有组件拼成。
    /// </summary>
    [UIWindow("AudioMixerSampleUIPlane", Layer = PanelType.NormalUI, Modal = true)]
    public sealed class AudioMixerSampleUIPlane : ShowcaseSampleUIPlaneBase
    {
        private static readonly string[] ChannelNames = { "Music", "SFX", "Voice", "Ambience" };
        private static readonly string[] PresetNames = { "Studio", "Game", "Cinema" };
        private static readonly Color[] ChannelColors =
        {
            new Color(0.30f, 0.55f, 1f, 1f),
            new Color(1f, 0.54f, 0.30f, 1f),
            new Color(0.30f, 0.85f, 0.66f, 1f),
            new Color(0.69f, 0.42f, 1f, 1f),
        };

        private readonly List<Slider> channelSliders = new List<Slider>();
        private readonly List<TextMeshProUGUI> channelValues = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> channelDbs = new List<TextMeshProUGUI>();
        private readonly List<GearSwitch> channelMutes = new List<GearSwitch>();
        private readonly List<GearProgressBar> meters = new List<GearProgressBar>();
        private readonly List<float> meterBases = new List<float>();
        private Slider masterSlider;
        private TextMeshProUGUI masterValue;
        private TextMeshProUGUI masterDb;
        private GearRadioGroup presets;
        private int presetIndex;

        protected override string GetTitle() => "Audio Mixer";

        protected override string GetSubtitle() => "整屏样例 · 4 通道 / 主输出 / 预设";

        protected override void BuildSample()
        {
            if (TryBindBaked()) return;

            const float cardWidth = 300f;
            const float cardHeight = 680f;
            const float gap = 20f;

            for (int i = 0; i < ChannelNames.Length; i++)
            {
                BuildChannelCard(i, i * (cardWidth + gap), cardHeight);
            }

            BuildMasterCard(1296f, cardHeight);

            TextMeshProUGUI hint = ShowcaseUIFactory.CreateText("Hint", Content,
                "拖动滑杆调音量；Mute 静音；Apply 应用预设。所有控件都是 Gear 现有组件。",
                16f, ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(hint.rectTransform, 0f, 700f, 1600f, 26f);

            GameObject pulseObject = new GameObject("LevelPulse", typeof(ShowcaseLevelPulse));
            pulseObject.transform.SetParent(Content, false);
            ShowcaseLevelPulse pulse = pulseObject.GetComponent<ShowcaseLevelPulse>();
            pulse.Configure(meters.ToArray(), meterBases.ToArray(),
                new[] { 2.1f, 3.3f, 2.7f, 1.7f, 2.4f },
                new[] { 0.2f, 1.1f, 2.2f, 3.0f, 0.7f });
        }

        private bool TryBindBaked()
        {
            Transform panel = transform.Find("Panel");
            Transform content = panel != null ? panel.Find("Content") : null;
            if (content == null) return false;

            channelSliders.Clear();
            channelValues.Clear();
            channelDbs.Clear();
            channelMutes.Clear();
            meters.Clear();
            meterBases.Clear();

            for (int i = 0; i < ChannelNames.Length; i++)
            {
                Transform card = content.Find(ChannelNames[i]);
                if (card == null) return false;
                Slider slider = card.Find("Volume")?.GetComponent<Slider>();
                TextMeshProUGUI value = card.Find("Value")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI db = card.Find("Db")?.GetComponent<TextMeshProUGUI>();
                GearSwitch mute = card.Find("Mute")?.GetComponent<GearSwitch>();
                GearProgressBar meter = card.Find("Meter")?.GetComponent<GearProgressBar>();
                if (slider == null || value == null || db == null || mute == null || meter == null) return false;
                channelSliders.Add(slider);
                channelValues.Add(value);
                channelDbs.Add(db);
                channelMutes.Add(mute);
                meters.Add(meter);
                meterBases.Add(0.55f);
            }

            Transform master = content.Find("Master");
            if (master == null) return false;
            masterSlider = master.Find("MasterVolume")?.GetComponent<Slider>();
            masterValue = master.Find("Value")?.GetComponent<TextMeshProUGUI>();
            masterDb = master.Find("MasterDb")?.GetComponent<TextMeshProUGUI>();
            GearProgressBar masterMeter = master.Find("MasterMeter")?.GetComponent<GearProgressBar>();
            presets = master.Find("Presets")?.GetComponent<GearRadioGroup>();
            Button apply = master.Find("Apply")?.GetComponent<Button>();
            Button reset = master.Find("Reset")?.GetComponent<Button>();
            if (masterSlider == null || masterValue == null || masterDb == null || masterMeter == null
                || presets == null || apply == null || reset == null) return false;

            for (int i = 0; i < channelSliders.Count; i++)
            {
                int index = i;
                channelSliders[i].onValueChanged.RemoveAllListeners();
                channelSliders[i].onValueChanged.AddListener(newValue =>
                {
                    int percent = Mathf.RoundToInt(newValue);
                    channelValues[index].text = percent.ToString("0") + "%";
                    channelDbs[index].text = ToDb(newValue);
                });

                channelMutes[i].OnValueChanged.RemoveAllListeners();
                channelMutes[i].OnValueChanged.AddListener(muted =>
                {
                    channelSliders[index].interactable = !muted;
                    Transform card = channelMutes[index].transform.parent;
                    TextMeshProUGUI label = card.Find("MuteLabel")?.GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.text = muted ? "Muted" : "Mute";
                        label.color = muted ? new Color(1f, 0.45f, 0.45f, 1f) : ShowcaseUIFactory.MutedText;
                    }
                });

                channelValues[i].text = Mathf.RoundToInt(channelSliders[i].value).ToString("0") + "%";
                channelDbs[i].text = ToDb(channelSliders[i].value);
            }

            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.onValueChanged.AddListener(newValue =>
            {
                masterValue.text = Mathf.RoundToInt(newValue).ToString("0") + "%";
                masterDb.text = ToDb(newValue);
            });
            masterValue.text = Mathf.RoundToInt(masterSlider.value).ToString("0") + "%";
            masterDb.text = ToDb(masterSlider.value);

            presets.OnValueChanged.RemoveAllListeners();
            presets.OnValueChanged.AddListener(index => presetIndex = Mathf.Clamp(index, 0, PresetNames.Length - 1));
            presetIndex = Mathf.Clamp(presets.SelectedIndex, 0, PresetNames.Length - 1);

            apply.onClick.RemoveAllListeners();
            apply.onClick.AddListener(ApplyPreset);
            reset.onClick.RemoveAllListeners();
            reset.onClick.AddListener(ResetMixer);

            meters.Add(masterMeter);
            meterBases.Add(0.62f);
            ShowcaseLevelPulse pulse = content.GetComponentInChildren<ShowcaseLevelPulse>(true);
            if (pulse != null)
            {
                pulse.Configure(meters.ToArray(), meterBases.ToArray(),
                    new[] { 2.1f, 3.3f, 2.7f, 1.7f, 2.4f },
                    new[] { 0.2f, 1.1f, 2.2f, 3.0f, 0.7f });
            }

            return true;
        }

        private void BuildChannelCard(int index, float x, float height)
        {
            string channel = ChannelNames[index];
            Color color = ChannelColors[index];

            ImageEx card = ShowcaseUIFactory.CreatePanel(channel, Content, ShowcaseUIFactory.SoftPanelColor, 16f, true);
            ShowcaseUIFactory.TopLeft(card.rectTransform, x, 0f, 300f, height);

            ImageEx accent = ShowcaseUIFactory.CreatePanel("Accent", card.transform, color, 2f);
            ShowcaseUIFactory.TopLeft(accent.rectTransform, 0f, 0f, 300f, 5f);

            ImageEx avatar = ShowcaseUIFactory.CreateCircle("Avatar", card.transform, color, 44f);
            ShowcaseUIFactory.TopLeft(avatar.rectTransform, 20f, 24f, 44f, 44f);
            TextMeshProUGUI initial = ShowcaseUIFactory.CreateText("Initial", avatar.transform,
                channel.Substring(0, 1), 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            ShowcaseUIFactory.Stretch(initial.rectTransform);

            TextMeshProUGUI name = ShowcaseUIFactory.CreateText("Name", card.transform, channel, 22f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(name.rectTransform, 78f, 30f, 200f, 30f);

            TextMeshProUGUI value = ShowcaseUIFactory.CreateText("Value", card.transform, "72%", 52f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(value.rectTransform, 20f, 84f, 220f, 60f);

            TextMeshProUGUI levelLabel = ShowcaseUIFactory.CreateText("LevelLabel", card.transform, "Level", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(levelLabel.rectTransform, 20f, 150f, 120f, 22f);

            Slider slider = ShowcaseUIFactory.CreateSlider("Volume", card.transform, 0f, 100f, 72f, color,
                new Color(0.12f, 0.14f, 0.18f, 1f));
            ShowcaseUIFactory.TopLeft(slider.GetComponent<RectTransform>(), 20f, 176f, 260f, 28f);
            slider.onValueChanged.AddListener(newValue =>
            {
                int percent = Mathf.RoundToInt(newValue);
                value.text = percent.ToString("0") + "%";
                channelDbs[index].text = ToDb(newValue);
            });

            TextMeshProUGUI meterLabel = ShowcaseUIFactory.CreateText("MeterLabel", card.transform, "Output", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(meterLabel.rectTransform, 20f, 220f, 120f, 22f);

            GearProgressBar meter = ShowcaseUIFactory.CreateMeter("Meter", card.transform, color, 0.55f);
            ShowcaseUIFactory.TopLeft(meter.GetComponent<RectTransform>(), 20f, 242f, 260f, 10f);

            GearSwitch mute = ShowcaseUIFactory.CreateSwitch("Mute", card.transform, false);
            ShowcaseUIFactory.TopLeft(mute.GetComponent<RectTransform>(), 20f, 278f, 56f, 30f);

            TextMeshProUGUI muteLabel = ShowcaseUIFactory.CreateText("MuteLabel", card.transform, "Mute", 16f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(muteLabel.rectTransform, 88f, 282f, 120f, 24f);

            TextMeshProUGUI db = ShowcaseUIFactory.CreateText("Db", card.transform, ToDb(72f), 18f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(db.rectTransform, 20f, 330f, 220f, 26f);

            mute.OnValueChanged.AddListener(muted =>
            {
                slider.interactable = !muted;
                muteLabel.text = muted ? "Muted" : "Mute";
                muteLabel.color = muted ? new Color(1f, 0.45f, 0.45f, 1f) : ShowcaseUIFactory.MutedText;
            });

            channelSliders.Add(slider);
            channelValues.Add(value);
            channelDbs.Add(db);
            channelMutes.Add(mute);
            meters.Add(meter);
            meterBases.Add(0.55f);
        }

        private void BuildMasterCard(float x, float height)
        {
            ImageEx card = ShowcaseUIFactory.CreatePanel("Master", Content, ShowcaseUIFactory.SoftPanelColor, 16f, true);
            ShowcaseUIFactory.TopLeft(card.rectTransform, x, 0f, 360f, height);

            TextMeshProUGUI title = ShowcaseUIFactory.CreateText("Title", card.transform, "MASTER", 24f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(title.rectTransform, 20f, 24f, 220f, 30f);

            masterValue = ShowcaseUIFactory.CreateText("Value", card.transform, "72%", 46f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(masterValue.rectTransform, 20f, 64f, 260f, 54f);

            masterSlider = ShowcaseUIFactory.CreateSlider("MasterVolume", card.transform, 0f, 100f, 72f,
                ShowcaseUIFactory.AccentColor, new Color(0.12f, 0.14f, 0.18f, 1f));
            ShowcaseUIFactory.TopLeft(masterSlider.GetComponent<RectTransform>(), 20f, 132f, 320f, 30f);
            masterSlider.onValueChanged.AddListener(newValue =>
            {
                masterValue.text = Mathf.RoundToInt(newValue).ToString("0") + "%";
                masterDb.text = ToDb(newValue);
            });

            GearProgressBar masterMeter = ShowcaseUIFactory.CreateMeter("MasterMeter", card.transform,
                ShowcaseUIFactory.AccentColor, 0.62f);
            ShowcaseUIFactory.TopLeft(masterMeter.GetComponent<RectTransform>(), 20f, 184f, 320f, 12f);

            ImageEx divider = ShowcaseUIFactory.CreatePanel("Divider", card.transform,
                new Color(1f, 1f, 1f, 0.08f), 0f);
            ShowcaseUIFactory.TopLeft(divider.rectTransform, 20f, 222f, 320f, 1f);

            TextMeshProUGUI presetLabel = ShowcaseUIFactory.CreateText("PresetLabel", card.transform, "Preset", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(presetLabel.rectTransform, 20f, 240f, 120f, 22f);

            presets = ShowcaseUIFactory.CreateRadioGroup("Presets", card.transform, PresetNames, 0, 320f, 44f, 8f, true);
            ShowcaseUIFactory.TopLeft(presets.GetComponent<RectTransform>(), 20f, 268f, 320f, 148f);
            presets.OnValueChanged.AddListener(index =>
            {
                presetIndex = Mathf.Clamp(index, 0, PresetNames.Length - 1);
            });

            Button apply = ShowcaseUIFactory.CreateButton("Apply", card.transform, "Apply Preset",
                ShowcaseUIFactory.AccentColor, 10f, ApplyPreset, 20f);
            ShowcaseUIFactory.TopLeft(apply.GetComponent<RectTransform>(), 20f, 440f, 320f, 52f);

            Button reset = ShowcaseUIFactory.CreateButton("Reset", card.transform, "Reset",
                new Color(0.16f, 0.18f, 0.23f, 1f), 10f, ResetMixer, 18f);
            ShowcaseUIFactory.TopLeft(reset.GetComponent<RectTransform>(), 20f, 504f, 320f, 44f);

            masterDb = ShowcaseUIFactory.CreateText("MasterDb", card.transform, ToDb(72f), 18f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(masterDb.rectTransform, 20f, 566f, 320f, 26f);

            meters.Add(masterMeter);
            meterBases.Add(0.62f);
        }

        private void ApplyPreset()
        {
            ShowToast("已应用预设 · " + PresetNames[Mathf.Clamp(presetIndex, 0, PresetNames.Length - 1)]);
        }

        private void ResetMixer()
        {
            for (int i = 0; i < channelSliders.Count; i++)
            {
                channelSliders[i].SetValueWithoutNotify(65f);
                channelValues[i].text = "65%";
                channelDbs[i].text = ToDb(65f);
                channelMutes[i].SetIsOn(false, false);
                channelSliders[i].interactable = true;
            }

            masterSlider.SetValueWithoutNotify(72f);
            masterValue.text = "72%";
            masterDb.text = ToDb(72f);
            presets.Select(0, false);
            presetIndex = 0;
            ShowToast("混音台已重置");
        }

        private static string ToDb(float percent)
        {
            if (percent <= 0.5f) return "−∞ dB";
            float db = 20f * Mathf.Log10(Mathf.Max(percent, 1f) / 100f);
            db = Mathf.Clamp(db, -60f, 0f);
            return "−" + Mathf.Abs(db).ToString("0.0") + " dB";
        }
    }
}
