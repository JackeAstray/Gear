using Evo.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Counter tab: a wallet balance. The balance gains or loses a random amount every interval, and Add and Send
    /// change it by hand. A pill under the number shows the last change, in Accent for gains and Negative for losses.
    /// The options set the format and roll settings.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCounter : MonoBehaviour
    {
        const int PopKey = 0x44434E54; // DCNT
        const float PopDuration = 0.3f;
        const float PopScale = 0.85f;
        const float PopAlpha = 0.3f;

        [SerializeField] private Counter counter;
        [SerializeField] private RectMask2D mask;
        [SerializeField] private float interval = 2;
        [SerializeField] private float step = 100;
        [SerializeField, Range(0, 1)] private float gainChance = 0.65f;
        [SerializeField] private Vector2 liveGain = new(1000, 12000);
        [SerializeField] private Vector2 liveLoss = new(1000, 8000);
        [Tooltip("Live changes turn back toward this range once the balance leaves it.")]
        [SerializeField] private Vector2 liveRange = new(20000, 250000);
        [SerializeField] private Vector2 addAmount = new(2000, 10000);
        [SerializeField] private Vector2 sendAmount = new(1500, 8000);
        [SerializeField] private float startChange = 4800;

        [Header("Change Pill")]
        [SerializeField] private RectTransform pill;
        [SerializeField] private CanvasGroup pillGroup;
        [SerializeField] private ProceduralShape pillBackground;
        [SerializeField] private TMP_Text pillText;
        [SerializeField] private StylerObject pillTextStyler;
        [SerializeField] private Sprite gainIcon;
        [SerializeField] private Sprite lossIcon;
        [Tooltip("Losses use Negative at this alpha, matching Accent (Soft) for gains.")]
        [SerializeField, Range(0, 1)] private float lossAlpha = 0.0588f;

        [Header("Options")]
        [SerializeField] private string[] formats = { "{0}", "${0}", "{0} XP" };

        TweenHandle pop;
        float elapsed;
        float lastChange;
        readonly char[] amountBuffer = new char[64];
        readonly char[] pillBuffer = new char[128];

        void Awake() => ShowChange(startChange, false);

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < interval)
                return;

            elapsed = 0;
            bool gain = counter.value < liveRange.x || (counter.value <= liveRange.y && Random.value < gainChance);
            Change(gain ? Amount(liveGain) : -Amount(liveLoss));
        }

        // Button events: a manual change also restarts the live interval
        public void Add()
        {
            elapsed = 0;
            Change(Amount(addAmount));
        }

        public void Send()
        {
            elapsed = 0;
            Change(-Amount(sendAmount));
        }

        public void SetFormat(int index)
        {
            counter.textFormat = formats[index];
            counter.SetValueInstant(counter.value);
            ShowChange(lastChange, false);
        }

        // Without the cross-fade, a mask clips the rolling digits to the counter instead
        public void SetFade(bool value)
        {
            counter.fadeAmount = value ? 1 : 0;
            mask.enabled = !value;
        }

        public void SetRollDigits(bool value) => counter.rollThroughDigits = value;
        public void SetDuration(float value) => counter.counterDuration = value;
        public void SetStagger(float value) => counter.digitStagger = value;

        float Amount(Vector2 range) => Mathf.Max(step, Mathf.Round(Random.Range(range.x, range.y) / step) * step);

        // The balance never goes below zero
        void Change(float delta)
        {
            float value = Mathf.Max(0, counter.value + delta);
            if (Mathf.Approximately(value, counter.value))
                return;

            ShowChange(value - counter.value, true);
            counter.SetValue(value);
        }

        void ShowChange(float change, bool animate)
        {
            lastChange = change;
            bool loss = change < 0;
            if (!WriteChange(loss ? '-' : '+', Mathf.Abs(change), counter.textFormat))
                pillText.text = (loss ? "-" : "+") + string.Format(counter.textFormat, Mathf.Abs(change).ToString("N0"));

            string color = loss ? "Negative" : "Accent";
            if (pillTextStyler.colorID != color)
            {
                pillTextStyler.colorID = color;
                pillTextStyler.UpdateStyler();

                pillBackground.fillStyler.color.stylerID = loss ? "Negative" : "Accent (Soft)";
                pillBackground.fillStyler.color.overrideAlpha = loss;
                pillBackground.fillStyler.color.alphaOverride = lossAlpha;
                pillBackground.UpdateStyler();
            }

            pop.Kill();
            if (!animate)
            {
                Pop(1);
                return;
            }

            pop = EvoTween.Create(0f, 1f, PopDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, t) => demo.Pop(t), PopKey);
        }

        void Pop(float t)
        {
            pill.localScale = Vector3.one * Mathf.LerpUnclamped(PopScale, 1, t);
            pillGroup.alpha = Mathf.LerpUnclamped(PopAlpha, 1, t);
        }

        // The sign, then the amount (N0) where the format has {0}, written into a buffer so a change doesn't allocate.
        // Formats with anything beyond {0}, {{ and }} return false and go through string.Format instead.
        bool WriteChange(char sign, float amount, string format)
        {
            if (format == null || !amount.TryFormat(amountBuffer, out int amountLength, "N0"))
                return false;

            int length = 0;
            pillBuffer[length++] = sign;
            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                bool escaped = (c == '{' || c == '}') && i + 1 < format.Length && format[i + 1] == c;
                if (c == '{' && !escaped)
                {
                    if (i + 2 >= format.Length || format[i + 1] != '0' || format[i + 2] != '}' || length + amountLength > pillBuffer.Length)
                        return false;

                    System.Array.Copy(amountBuffer, 0, pillBuffer, length, amountLength);
                    length += amountLength;
                    i += 2;
                    continue;
                }

                if ((c == '}' && !escaped) || length == pillBuffer.Length)
                    return false;

                pillBuffer[length++] = c;
                if (escaped)
                    i++;
            }

            pillText.SetCharArray(pillBuffer, 0, length);
            return true;
        }
    }
}