using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 主题令牌资产：按 ID 保存颜色 / 数值，供 <see cref="ImageExThemeBinder"/> 绑定。
    /// <para>
    /// 语义参考"按 ID 取样式、换主题即换皮"的通用做法（Gear 自研实现，不依赖任何第三方 UI 插件）：
    /// 组件只认 tokenId，不认具体色值；换一套 Theme 资产即可整体换肤。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gear/UI/ImageEx Theme", fileName = "ImageExTheme")]
    public class ImageExThemeAsset : ScriptableObject
    {
        [Serializable]
        public struct ColorToken
        {
            public string id;
            public Color color;
        }

        [Serializable]
        public struct FloatToken
        {
            public string id;
            public float value;
        }

        [SerializeField] private List<ColorToken> colors = new List<ColorToken>();
        [SerializeField] private List<FloatToken> floats = new List<FloatToken>();

        /// <summary>颜色令牌列表（只读）。</summary>
        public IReadOnlyList<ColorToken> Colors => colors;

        /// <summary>数值令牌列表（只读）。</summary>
        public IReadOnlyList<FloatToken> Floats => floats;

        /// <summary>按 ID 取颜色；不存在返回 false。</summary>
        public bool TryGetColor(string id, out Color color)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < colors.Count; i++)
                {
                    if (string.Equals(colors[i].id, id, StringComparison.Ordinal))
                    {
                        color = colors[i].color;
                        return true;
                    }
                }
            }

            color = default;
            return false;
        }

        /// <summary>按 ID 取数值；不存在返回 false。</summary>
        public bool TryGetFloat(string id, out float value)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < floats.Count; i++)
                {
                    if (string.Equals(floats[i].id, id, StringComparison.Ordinal))
                    {
                        value = floats[i].value;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }

        /// <summary>该资产是否声明了指定颜色 ID。</summary>
        public bool HasColor(string id) => TryGetColor(id, out _);

        /// <summary>该资产是否声明了指定数值 ID。</summary>
        public bool HasFloat(string id) => TryGetFloat(id, out _);

#if UNITY_EDITOR
        /// <summary>编辑器用：写入/覆盖一个颜色令牌。</summary>
        public void EditorSetColor(string id, Color color)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < colors.Count; i++)
            {
                if (!string.Equals(colors[i].id, id, StringComparison.Ordinal)) continue;
                ColorToken token = colors[i];
                token.color = color;
                colors[i] = token;
                return;
            }
            colors.Add(new ColorToken { id = id, color = color });
        }

        /// <summary>编辑器用：写入/覆盖一个数值令牌。</summary>
        public void EditorSetFloat(string id, float value)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < floats.Count; i++)
            {
                if (!string.Equals(floats[i].id, id, StringComparison.Ordinal)) continue;
                FloatToken token = floats[i];
                token.value = value;
                floats[i] = token;
                return;
            }
            floats.Add(new FloatToken { id = id, value = value });
        }
#endif
    }
}
