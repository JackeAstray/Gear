using UnityEngine;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Text Effects tab. In Tagged words, each effect moves the highlighted words that carry its tag (one word can carry several),
    /// and those words take the Styler accent. In Entire text, Bounce and Wave lock each other, since both move letters up and down.
    /// The Typewriter and Scramble tiles turn off when their reveal ends, so one click plays it again. Speed sets both reveals.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoTextEffects : MonoBehaviour, IStylerHandler
    {
        [SerializeField] private TextEffects effects;
        [SerializeField] private TMP_Text text;
        [Tooltip("{0} opens a color tag around each tagged word, {1} closes it.")]
        [SerializeField, TextArea(3, 6)] private string source;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup scope;
        [Tooltip("Bounce, Palette, Pulse, Shake, Shimmer, Wave.")]
        [SerializeField] private Switch[] effectTiles;
        [SerializeField] private CanvasGroup bounceLock;
        [SerializeField] private CanvasGroup waveLock;
        [SerializeField] private Switch typewriter;
        [SerializeField] private Switch scramble;
        [SerializeField] private Slider speed;

        const int Bounce = 0, Wave = 5;

        TextEffects.Effect[] list;
        StylerPreset preset;
        bool entireText, typing, scrambling;

        // Both texts once built, and the accent the tagged one uses, so switching the scope or opening the tab again doesn't
        // format a new copy
        string plainText, taggedText;
        Color taggedAccent;

        void Awake()
        {
            list = new TextEffects.Effect[] { effects.Bounce, effects.Palette, effects.Pulse, effects.Shake, effects.Shimmer, effects.Wave };
            entireText = list[0].scope == TextEffects.EffectScope.EntireText;
            DemoUtils.Show(scope, entireText ? 1 : 0);
           
            for (int i = 0; i < list.Length; i++)
                DemoUtils.Show(effectTiles[i], list[i].enabled);
            
            DemoUtils.Show(speed, effects.Typewriter.speed);
            UpdateLocks(false);
        }

        void OnEnable()
        {
            Styler.Register(this);
            UpdateStyler();
            typing = true;
            scrambling = false;
            DemoUtils.Show(typewriter, true);
            DemoUtils.Show(scramble, false);
        }

        void OnDisable() => Styler.Unregister(this);

        void Update()
        {
            if (typing && !effects.IsTyping)
            {
                typing = false;
                DemoUtils.Show(typewriter, false);
            }

            if (scrambling && !effects.IsScrambling)
            {
                scrambling = false;
                DemoUtils.Show(scramble, false);
            }
        }

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => preset;
            set
            {
                preset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            StylerPreset active = preset != null ? preset : Styler.GetDefaultPreset(false);
            Color accent = default;
            bool tags = !entireText && active != null && active.TryGetColor("Accent", out accent);
            if (!tags)
            {
                plainText ??= string.Format(source, "", "");
                text.text = plainText;
                return;
            }

            if (taggedText == null || !accent.Equals(taggedAccent))
            {
                taggedText = string.Format(source, "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">", "</color>");
                taggedAccent = accent;
            }

            text.text = taggedText;
        }
        #endregion

        public void SetScope(int index)
        {
            entireText = index == 1;
            var value = entireText ? TextEffects.EffectScope.EntireText : TextEffects.EffectScope.TaggedCharacters;
            foreach (TextEffects.Effect effect in list)
                effect.scope = value;
          
            effects.Scramble.scope = value;

            // Bounce and Wave can both be on from Tagged words: keep Wave
            if (entireText && list[Bounce].enabled && list[Wave].enabled)
                effectTiles[Bounce].SetValue(false);

            UpdateLocks(true);
            UpdateStyler();
            effects.Refresh();
        }

        public void SetBounce(bool value) => SetEffect(0, value);
        public void SetPalette(bool value) => SetEffect(1, value);
        public void SetPulse(bool value) => SetEffect(2, value);
        public void SetShake(bool value) => SetEffect(3, value);
        public void SetShimmer(bool value) => SetEffect(4, value);
        public void SetWave(bool value) => SetEffect(5, value);

        void SetEffect(int index, bool value)
        {
            list[index].enabled = value;
            UpdateLocks(true);
            effects.Refresh();
        }

        // The tagged words keep Bounce and Wave apart; in Entire text both would move every letter up and down
        void UpdateLocks(bool animate)
        {
            DemoUtils.SetLocked(bounceLock, entireText && list[Wave].enabled, animate);
            DemoUtils.SetLocked(waveLock, entireText && list[Bounce].enabled, animate);
        }

        public void SetTypewriter(bool value)
        {
            typing = value;
            if (value) { effects.PlayTypewriter(); }
            else { effects.SkipTypewriter(); }
        }

        public void SetScramble(bool value)
        {
            scrambling = value;
            if (value) { effects.PlayScramble(); }
            else { effects.SkipScramble(); }
        }

        // Characters per second for both reveals: the scramble settles across the text in the time the typewriter takes
        public void SetSpeed(float value)
        {
            effects.Typewriter.speed = value;
            effects.Scramble.duration = text.textInfo.characterCount / value;
        }
    }
}