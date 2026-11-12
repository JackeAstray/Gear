using System;
using UnityEngine;
using TMPro;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample mission briefing. A Tab Manager switches Briefing and Intel, a Radio Button Group picks the objective,
    /// a Selector browses the sites, a Toggle readies the player and a Progress Button with a Timer deploys.
    /// Tween Animators run the rows, rings, route and intro. This keeps the pick in step across the list, the map and
    /// the Intel page, fills Intel from its data and pans the map to the picked objective with an Evo Tween.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoMissionBriefing : DemoScreenContent
    {
        [Serializable]
        public class Objective
        {
            public string title;
            public string distance;
            public string eta;
            [Range(1, 5)] public int threat = 1;
            public bool bonus;
            public string[] notes = new string[3];
            [Tooltip("Its marker on the map.")]
            public RectTransform marker;
            [Tooltip("The marker's second plate line, shown while it is picked.")]
            public GameObject details;
            [Tooltip("Its row's orders, typed in when picked.")]
            public TextEffects orders;
        }

        [Header("Pages")]
        [SerializeField] private TabManager pages;
        [Tooltip("Each page's component tags, in page order. Only the open page's tags show.")]
        [SerializeField] private GameObject[] pageTags;
        [Tooltip("Plays when the screen opens beside the info column.")]
        [SerializeField] private TweenAnimator intro;

        [Header("Objectives")]
        [SerializeField] private RadioButtonGroup objectiveList;
        [SerializeField] private Objective[] objectives;
        [SerializeField] private Color threatColor = new Color32(255, 74, 61, 255);
        [SerializeField] private Color bonusColor = new Color32(255, 176, 58, 255);
        [SerializeField] private string[] threatLevels = { "LOW", "GUARDED", "ELEVATED", "HIGH", "SEVERE" };

        [Header("Map")]
        [Tooltip("Moves with the map; the markers live in it.")]
        [SerializeField] private RectTransform layer;
        [SerializeField] private RectTransform focusMarker;
        [Tooltip("Ripples and ring that take the picked objective's color.")]
        [SerializeField] private ProceduralShape[] focusRings;
        [SerializeField] private CanvasGroup bonusMarker;
        [Tooltip("Where a picked marker is brought to, in map units from the top left.")]
        [SerializeField] private Vector2 panCenter = new(1060, 470);
        [SerializeField] private Vector2 panMin = new(-320, -220);
        [SerializeField] private Vector2 panMax = new(220, 220);
        [SerializeField, Min(0f)] private float panDuration = 0.75f;

        [Header("Intel")]
        [Tooltip("Its items are the site names, in objective order.")]
        [SerializeField] private Selector site;
        [SerializeField] private TextEffects siteEffects;
        [SerializeField] private TMP_Text siteIndex;
        [SerializeField] private TMP_Text siteObjective;
        [SerializeField] private TMP_Text distance;
        [SerializeField] private TMP_Text eta;
        [SerializeField] private ProgressBar threat;
        [SerializeField] private ProceduralShape threatFill;
        [SerializeField] private TMP_Text threatLevel;
        [SerializeField] private TextEffects[] notes;

        [Header("Squad")]
        [Tooltip("The player's card. The rest of the squad is ready.")]
        [SerializeField] private Toggle ready;
        [SerializeField] private ProceduralShape readyMark;
        [SerializeField] private TMP_Text readyLabel;
        [Tooltip("Readiness dots in the top bar, clipped to the ready count.")]
        [SerializeField] private ProceduralShape readyDots;
        [SerializeField] private TMP_Text readyCount;
        [SerializeField] private TMP_Text deployNote;
        [SerializeField, Min(1)] private int squadSize = 3;
        [SerializeField] private Color readyColor = new Color32(111, 211, 154, 255);
        [SerializeField] private Color waitingColor = new Color32(255, 176, 58, 255);

        const int PanProperty = 31901;
        const float BonusFade = 0.4f;

        string[] siteIndexes, objectiveLines, readyCounts;
        int picked;
        Vector2 pan, layerOrigin;
        TweenHandle panTween;

        void Awake()
        {
            // Every text a pick can show, built once
            siteIndexes = new string[objectives.Length];
            objectiveLines = new string[objectives.Length];
            for (int i = 0; i < objectives.Length; i++)
            {
                siteIndexes[i] = (i + 1).ToString("00") + " / " + objectives.Length.ToString("00");
                objectiveLines[i] = (objectives[i].bonus ? "BONUS / " : "OBJECTIVE / ") + objectives[i].title;
            }

            readyCounts = new string[squadSize + 1];
            for (int i = 0; i <= squadSize; i++)
                readyCounts[i] = i + "/" + squadSize;

            // The screen is saved showing the first pick; the map's rest position is what it was panned from
            picked = objectiveList.selectedIndex;
            pan = PanFor(picked);
            layerOrigin = layer.anchoredPosition - pan;

            pages.onTabChanged.AddListener(ShowPage);
            objectiveList.onSelectionChanged.AddListener(ShowObjective);
            site.onSelectionChanged.AddListener(PickSite);
            ready.onValueChanged.AddListener(SetReady);
        }

        void OnDisable() => panTween.Complete();

        protected override void OnFocusChanged(bool value)
        {
            if (value && intro != null)
                intro.Play();
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pageTags.Length; i++)
                pageTags[i].SetActive(i == index);
        }

        void PickSite(int index) => objectiveList.SetButton(index);

        void ShowObjective(int index)
        {
            if (index < 0 || index == picked)
                return;

            objectives[picked].details.SetActive(false);
            picked = index;

            Objective objective = objectives[index];
            Color accent = objective.bonus ? bonusColor : threatColor;
            objective.details.SetActive(true);
            objective.orders.PlayTypewriter();

            // The ring and ripples move to the marker, then the map brings it to the middle
            focusMarker.anchoredPosition = objective.marker.anchoredPosition;
            foreach (ProceduralShape ring in focusRings)
                ring.OutlineColor = accent;

            panTween.Kill();
            panTween = EvoTween.Create(pan, PanFor(index), panDuration)
                .WithEase(Ease.InOutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (briefing, offset) => briefing.Pan(offset), PanProperty);

            // Intel follows the pick, even while another page is open
            site.SetSelection(index, true, false);
            siteEffects.PlayScramble();
            siteIndex.SetText(siteIndexes[index]);
            siteObjective.SetText(objectiveLines[index]);
            distance.SetText(objective.distance);
            eta.SetText(objective.eta);
            threat.Value = objective.threat;
            threatFill.PatternColor = accent;
            threatLevel.SetText(threatLevels[objective.threat - 1]);
            threatLevel.color = accent;

            for (int i = 0; i < notes.Length; i++)
                notes[i].SetText(objective.notes[i], true);
        }

        // In map units, the move that brings the objective's marker to the pan center
        Vector2 PanFor(int index)
        {
            Vector2 marker = objectives[index].marker.anchoredPosition;
            Vector2 offset = new(panCenter.x - marker.x, -panCenter.y - marker.y);
            return Vector2.Max(panMin, Vector2.Min(panMax, offset));
        }

        void Pan(Vector2 offset)
        {
            pan = offset;
            layer.anchoredPosition = layerOrigin + offset;
        }

        void SetReady(bool value)
        {
            Color color = value ? readyColor : waitingColor;
            int count = squadSize - (value ? 0 : 1);

            readyMark.FillColor = value ? readyColor : Color.clear;
            readyMark.OutlineColor = color;
            readyLabel.SetText(value ? "READY" : "NOT READY");
            readyLabel.color = color;

            readyDots.ClipAmount = (float)count / squadSize;
            readyCount.SetText(readyCounts[count]);
            readyCount.color = count == squadSize ? readyColor : waitingColor;
            deployNote.SetText(value ? "SQUAD READY" : "WAITING FOR YOU");
            deployNote.color = color;
        }
    }
}
