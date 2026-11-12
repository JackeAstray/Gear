using System;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Slider tabs: a list of rows, one slider each. Format, Whole numbers and Interactable apply to every row.
    /// On the Slider tab each row also has a Slider (Input), and Show picks how the value reads:
    /// None, the Slider's value bubble, or the Slider (Input) field. A row's icon chip lights while its slider is in use.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSlider : MonoBehaviour
    {
        [Serializable]
        class Row
        {
            [Tooltip("Slider, then Slider (Input) when the tab has the Show option.")]
            public Slider[] types;
            public GameObject bubble;
        }

        [SerializeField] private Row[] rows;
        [SerializeField] private TMP_Text info;
        [Tooltip("Info line for None, Bubble and Input.")]
        [SerializeField] private string[] hints;

        [Header("Options")]
        [SerializeField] private CanvasGroup formatOption;

        const float LitDuration = 0.15f;

        int show;

        // None, Bubble or Input, from what the scene shows
        void Awake()
        {
            if (rows.Length == 0)
                return;

            Row first = rows[0];
            show = first.types.Length > 1 && first.types[1].gameObject.activeSelf ? 2 : first.bubble != null && first.bubble.activeSelf ? 1 : 0;
            DemoUtils.SetLocked(formatOption, show == 0, false);
        }

        // The next type takes over each row's value and state before it shows
        public void SetShow(int index)
        {
            if (index == show)
                return;

            foreach (Row row in rows)
            {
                Slider current = Active(row);
                Slider next = row.types[index == 2 && row.types.Length > 1 ? 1 : 0];

                if (row.bubble != null)
                    row.bubble.SetActive(index == 1);

                if (next == current)
                    continue;

                next.SetValueWithoutNotify(current.value);
                next.interactable = current.interactable;
                next.gameObject.SetActive(true);
                current.gameObject.SetActive(false);
            }

            show = index;
            DemoUtils.SetLocked(formatOption, index == 0);

            if (info != null && index < hints.Length)
                info.text = hints[index];
        }

        // Number, Percent or Decimal: the 0-100 value as it is, with a percent sign, or as 0-1
        public void SetFormat(int index)
        {
            foreach (Row row in rows)
            {
                foreach (Slider slider in row.types)
                {
                    slider.textFormat = index == 1 ? "{0}%" : "{0}";
                    slider.displayMultiplier = index == 2 ? 0.01f : 1;
                    slider.displayFormat = index == 2 ? NumberDisplayFormat.Fixed2 : NumberDisplayFormat.Fixed0;
                    slider.SetText(slider.value);
                }
            }
        }

        public void SetWholeNumbers(bool value)
        {
            foreach (Row row in rows)
            {
                foreach (Slider slider in row.types)
                    slider.wholeNumbers = value;
            }
        }

        public void SetInteractable(bool value)
        {
            foreach (Row row in rows)
            {
                foreach (Slider slider in row.types)
                    slider.interactable = value;
            }
        }

        Slider Active(Row row)
        {
            foreach (Slider slider in row.types)
            {
                if (slider.gameObject.activeSelf)
                    return slider;
            }

            return row.types[0];
        }
    }
}
