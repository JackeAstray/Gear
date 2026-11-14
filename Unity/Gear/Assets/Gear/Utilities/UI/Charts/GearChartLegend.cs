using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>图表图例：把名称/颜色写入预先摆好的 TMP 文本与色块数组。</summary>
    [AddComponentMenu("UI/Gear/Charts/Legend")]
    public sealed class GearChartLegend : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Graphic[] swatches;

        public void Configure(TMP_Text[] labelArray, Graphic[] swatchArray)
        {
            labels = labelArray;
            swatches = swatchArray;
        }

        public void SetEntries(IList<string> names, IList<Color> colors)
        {
            int count = Mathf.Max(labels != null ? labels.Length : 0, swatches != null ? swatches.Length : 0);
            for (int i = 0; i < count; i++)
            {
                bool active = names != null && i < names.Count;
                if (labels != null && i < labels.Length && labels[i] != null)
                {
                    labels[i].gameObject.SetActive(active);
                    if (active) labels[i].text = names[i];
                }

                if (swatches != null && i < swatches.Length && swatches[i] != null)
                {
                    swatches[i].gameObject.SetActive(active);
                    if (active && colors != null && i < colors.Count)
                        swatches[i].color = colors[i];
                }
            }
        }
    }
}
