using System.Collections.Generic;
using Gear.Utilities.UI.ImageExtensions;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// <see cref="GearPickerWheel"/> 的循环/行视觉状态。
    /// 方法仍放在主文件；这里只保留循环相关的序列化字段与缓存，
    /// 避免主文件超过 600 行的架构守卫线。
    /// </summary>
    public sealed partial class GearPickerWheel
    {
        [SerializeField] private ImageEx highlight;
        [SerializeField, Range(0f, 1f)] private float minRowAlpha = 0.26f;
        [SerializeField, Min(0.1f)] private float fadeRange = 2.4f;

        private sealed class RowVisual
        {
            public RectTransform Rect;
            public CanvasGroup Group;
        }

        private readonly List<RectTransform> sourceRows = new List<RectTransform>();
        private readonly List<GameObject> wrapClones = new List<GameObject>();
        private readonly List<RowVisual> rowVisuals = new List<RowVisual>();
        private ImageEx runtimeHighlight;
        private bool wrapRowsBuilt;
        private int wrapRowCount;
        private float wrapItemHeight;
        private int wrapCycles;

        private int BaseCycle => wrapCycles > 0 ? wrapCycles / 2 : 1;
    }
}
