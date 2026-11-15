using System.Collections.Generic;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Motion;
using Gear.Utilities.UI.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 整屏样例 2：Party Lobby。
    /// 4 个玩家位（头像/准备开关）+ 模式/回合设置 + 开始倒计时。
    /// </summary>
    [UIWindow("PartyLobbySampleUIPlane", Layer = PanelType.NormalUI, Modal = true)]
    public sealed class PartyLobbySampleUIPlane : ShowcaseSampleUIPlaneBase
    {
        private const float CountdownSeconds = 5f;

        private static readonly string[] PlayerNames = { "You", "Nova", "Kai", "Mira" };
        private static readonly Color[] PlayerColors =
        {
            new Color(0.35f, 0.62f, 1f, 1f),
            new Color(1f, 0.56f, 0.32f, 1f),
            new Color(0.35f, 0.85f, 0.66f, 1f),
            new Color(0.72f, 0.45f, 1f, 1f),
        };
        private static readonly string[] ModeNames = { "Co-op", "Versus", "Party" };

        private readonly List<GearSwitch> readySwitches = new List<GearSwitch>();
        private readonly List<TextMeshProUGUI> statusLabels = new List<TextMeshProUGUI>();
        private TextMeshProUGUI readyCountLabel;
        private TextMeshProUGUI timerLabel;
        private Button startButton;
        private GearTimer countdown;
        private GearRadioGroup modeGroup;
        private GearQuantitySelector rounds;

        protected override string GetTitle() => "Party Lobby";

        protected override string GetSubtitle() => "整屏样例 · 玩家位 / 准备状态 / 模式 / 倒计时";

        protected override void BuildSample()
        {
            if (TryBindBaked()) return;

            RectTransform playersRoot = ShowcaseUIFactory.CreateRect("Players", Content);
            ShowcaseUIFactory.TopLeft(playersRoot, 0f, 0f, 1600f, 300f);

            for (int i = 0; i < PlayerNames.Length; i++)
            {
                BuildPlayerCard(playersRoot, i, i * 400f);
            }

            BuildSettings();

            TextMeshProUGUI hint = ShowcaseUIFactory.CreateText("Hint", Content,
                "点击 Ready 切换准备；Start 启动 5 秒倒计时；模式与回合都会被 Start 读取。",
                16f, ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(hint.rectTransform, 0f, 670f, 1600f, 26f);

            GearAnimatedContainer entrance = playersRoot.gameObject.AddComponent<GearAnimatedContainer>();
            entrance.FromScale = 0.9f;
            entrance.Stagger = 0.08f;
            entrance.Play();
        }

        private bool TryBindBaked()
        {
            Transform panel = transform.Find("Panel");
            Transform content = panel != null ? panel.Find("Content") : null;
            if (content == null) return false;

            Transform playersRoot = content.Find("Players");
            Transform settings = content.Find("Settings");
            if (playersRoot == null || settings == null) return false;

            readySwitches.Clear();
            statusLabels.Clear();

            for (int i = 0; i < PlayerNames.Length; i++)
            {
                Transform card = playersRoot.Find("Player " + (i + 1));
                if (card == null) return false;
                GearSwitch ready = card.Find("Ready")?.GetComponent<GearSwitch>();
                TextMeshProUGUI status = card.Find("Status")?.GetComponent<TextMeshProUGUI>();
                if (ready == null || status == null) return false;

                ready.OnValueChanged.RemoveAllListeners();
                ready.OnValueChanged.AddListener(on =>
                {
                    status.text = on ? "Ready" : "Waiting";
                    status.color = on ? new Color(0.35f, 0.85f, 0.66f, 1f) : ShowcaseUIFactory.MutedText;
                    UpdateReadyCount();
                });
                readySwitches.Add(ready);
                statusLabels.Add(status);
            }

            modeGroup = settings.Find("Modes")?.GetComponent<GearRadioGroup>();
            rounds = settings.Find("Rounds")?.GetComponent<GearQuantitySelector>();
            startButton = settings.Find("Start")?.GetComponent<Button>();
            readyCountLabel = settings.Find("ReadyCount")?.GetComponent<TextMeshProUGUI>();
            timerLabel = settings.Find("Countdown")?.GetComponent<TextMeshProUGUI>();
            if (modeGroup == null || rounds == null || startButton == null
                || readyCountLabel == null || timerLabel == null) return false;

            countdown = timerLabel.GetComponent<GearTimer>();
            if (countdown == null) return false;
            countdown.Label = timerLabel;
            countdown.Completed -= OnCountdownCompleted;
            countdown.Completed += OnCountdownCompleted;

            modeGroup.OnValueChanged.RemoveAllListeners();
            modeGroup.OnValueChanged.AddListener(index => ShowToast("模式切换 · " + ModeNames[index]));
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartCountdown);

            UpdateReadyCount();
            return true;
        }

        private void BuildPlayerCard(RectTransform parent, int index, float x)
        {
            string playerName = PlayerNames[index];
            Color color = PlayerColors[index];

            ImageEx card = ShowcaseUIFactory.CreatePanel("Player " + (index + 1), parent,
                ShowcaseUIFactory.SoftPanelColor, 16f, true);
            ShowcaseUIFactory.TopLeft(card.rectTransform, x, 0f, 380f, 300f);

            ImageEx accent = ShowcaseUIFactory.CreatePanel("Accent", card.transform, color, 2f);
            ShowcaseUIFactory.TopLeft(accent.rectTransform, 0f, 0f, 380f, 5f);

            ImageEx avatar = ShowcaseUIFactory.CreateCircle("Avatar", card.transform, color, 96f);
            ShowcaseUIFactory.TopLeft(avatar.rectTransform, 24f, 24f, 96f, 96f);
            TextMeshProUGUI initial = ShowcaseUIFactory.CreateText("Initial", avatar.transform,
                playerName.Substring(0, 1), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            ShowcaseUIFactory.Stretch(initial.rectTransform);

            TextMeshProUGUI name = ShowcaseUIFactory.CreateText("Name", card.transform, playerName, 24f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(name.rectTransform, 140f, 30f, 200f, 30f);

            TextMeshProUGUI seat = ShowcaseUIFactory.CreateText("Seat", card.transform,
                "Slot " + (index + 1), 15f, ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(seat.rectTransform, 140f, 64f, 200f, 22f);

            TextMeshProUGUI status = ShowcaseUIFactory.CreateText("Status", card.transform,
                index < 2 ? "Ready" : "Waiting", 16f,
                index < 2 ? new Color(0.35f, 0.85f, 0.66f, 1f) : ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(status.rectTransform, 24f, 238f, 160f, 24f);

            GearSwitch ready = ShowcaseUIFactory.CreateSwitch("Ready", card.transform, index < 2);
            ShowcaseUIFactory.TopLeft(ready.GetComponent<RectTransform>(), 196f, 234f, 56f, 30f);

            TextMeshProUGUI readyLabel = ShowcaseUIFactory.CreateText("ReadyLabel", card.transform, "Ready", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(readyLabel.rectTransform, 260f, 238f, 100f, 22f);

            if (index == 0)
            {
                ImageEx host = ShowcaseUIFactory.CreatePanel("Host", card.transform,
                    new Color(1f, 0.78f, 0.32f, 1f), 6f);
                ShowcaseUIFactory.TopLeft(host.rectTransform, 296f, 24f, 60f, 24f);
                TextMeshProUGUI hostText = ShowcaseUIFactory.CreateText("HostText", host.transform, "HOST", 12f,
                    Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
                ShowcaseUIFactory.Stretch(hostText.rectTransform);
            }

            ready.OnValueChanged.AddListener(on =>
            {
                status.text = on ? "Ready" : "Waiting";
                status.color = on ? new Color(0.35f, 0.85f, 0.66f, 1f) : ShowcaseUIFactory.MutedText;
                UpdateReadyCount();
            });

            readySwitches.Add(ready);
            statusLabels.Add(status);
        }

        private void BuildSettings()
        {
            ImageEx panel = ShowcaseUIFactory.CreatePanel("Settings", Content, ShowcaseUIFactory.SoftPanelColor, 16f, true);
            ShowcaseUIFactory.TopLeft(panel.rectTransform, 0f, 330f, 1600f, 300f);

            TextMeshProUGUI modeLabel = ShowcaseUIFactory.CreateText("ModeLabel", panel.transform, "GAME MODE", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(modeLabel.rectTransform, 24f, 22f, 160f, 22f);

            modeGroup = ShowcaseUIFactory.CreateRadioGroup("Modes", panel.transform, ModeNames, 0, 210f, 44f, 12f);
            ShowcaseUIFactory.TopLeft(modeGroup.GetComponent<RectTransform>(), 24f, 50f, 654f, 44f);
            modeGroup.OnValueChanged.AddListener(index => ShowToast("模式切换 · " + ModeNames[index]));

            TextMeshProUGUI roundsLabel = ShowcaseUIFactory.CreateText("RoundsLabel", panel.transform, "ROUNDS", 15f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(roundsLabel.rectTransform, 720f, 22f, 120f, 22f);

            rounds = ShowcaseUIFactory.CreateQuantity("Rounds", panel.transform, 1, 9, 3);
            ShowcaseUIFactory.TopLeft(rounds.GetComponent<RectTransform>(), 720f, 50f, 180f, 48f);

            readyCountLabel = ShowcaseUIFactory.CreateText("ReadyCount", panel.transform, "2 / 4 ready", 22f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(readyCountLabel.rectTransform, 24f, 150f, 300f, 30f);

            TextMeshProUGUI summary = ShowcaseUIFactory.CreateText("Summary", panel.transform,
                "本地多人 · 4 个槽位 · 可随时切换准备", 16f, ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(summary.rectTransform, 24f, 186f, 600f, 24f);

            startButton = ShowcaseUIFactory.CreateButton("Start", panel.transform, "START",
                ShowcaseUIFactory.AccentColor, 10f, StartCountdown, 24f);
            ShowcaseUIFactory.TopLeft(startButton.GetComponent<RectTransform>(), 1360f, 40f, 216f, 60f);

            timerLabel = ShowcaseUIFactory.CreateText("Countdown", panel.transform, "", 26f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.Center, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(timerLabel.rectTransform, 1360f, 116f, 216f, 36f);

            countdown = timerLabel.gameObject.AddComponent<GearTimer>();
            countdown.Label = timerLabel;
            countdown.Format = "ss";
            countdown.Configure(GearTimerMode.Countdown, CountdownSeconds, false, "ss");
            countdown.Completed += OnCountdownCompleted;

            UpdateReadyCount();
        }

        private void UpdateReadyCount()
        {
            int ready = 0;
            for (int i = 0; i < readySwitches.Count; i++)
            {
                if (readySwitches[i] != null && readySwitches[i].IsOn) ready++;
            }

            if (readyCountLabel != null)
                readyCountLabel.text = ready + " / " + readySwitches.Count + " ready";

            if (startButton != null && countdown != null && !countdown.IsRunning)
                startButton.interactable = ready > 0;
        }

        private void StartCountdown()
        {
            if (countdown == null || countdown.IsRunning) return;

            startButton.interactable = false;
            timerLabel.text = Mathf.CeilToInt(CountdownSeconds).ToString("0");
            countdown.Configure(GearTimerMode.Countdown, CountdownSeconds, true, "ss");
            ShowToast("派对将在 " + Mathf.CeilToInt(CountdownSeconds) + " 秒后开始");
        }

        private void OnCountdownCompleted()
        {
            timerLabel.text = "GO!";
            if (startButton != null) startButton.interactable = true;
            ShowToast("Party starting · " + ModeNames[Mathf.Clamp(modeGroup.SelectedIndex, 0, ModeNames.Length - 1)]
                + " · " + rounds.Value + " rounds");
        }
    }
}
