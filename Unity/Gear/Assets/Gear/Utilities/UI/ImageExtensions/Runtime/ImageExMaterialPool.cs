using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Gear.Common;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 的材质签名池：把“最终 uniform 完全一致”的实例指向同一份材质，
    /// 让同设置的卡片/列表项可以合批（<see cref="MaterialMode.Pooled"/> 专用）。
    /// <para>
    /// 签名来自材质本身（shader 属性逐个取值 + 关键字），不依赖组件字段列表，
    /// 因此新增 shader 属性时不需要同步维护一份哈希清单。
    /// </para>
    /// </summary>
    internal static class ImageExMaterialPool
    {
        private const int MaxEntries = 4096;

        private static readonly Dictionary<(int ShaderId, ulong Signature), Material> s_pool =
            new Dictionary<(int, ulong), Material>();

        private static bool s_warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_pool.Clear();
            s_warned = false;
        }

        /// <summary>
        /// 接收一份已经写好 uniform 的“样本材质”：签名相同则复用池里的旧材质并销毁样本，
        /// 否则把样本收进池。池满后返回样本本身（退化为独立材质，但不再登记）。
        /// </summary>
        internal static Material Acquire(Material probe)
        {
            if (probe == null) return null;

            Shader shader = probe.shader;
            if (shader == null) return probe;

            var key = (shader.GetInstanceID(), ComputeSignature(probe));
            if (s_pool.TryGetValue(key, out Material existing) && existing != null)
            {
                DestroyProbe(probe);
                return existing;
            }

            if (s_pool.Count >= MaxEntries)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Log.Warning("[ImageEx] Pooled 材质的唯一设置超过 " + MaxEntries
                        + " 个，后续实例将回退为独立材质；请减少每实例独有的设置，或改用 Dynamic 模式。");
                }
                return probe;
            }

            probe.name = "ImageEx Pooled";
            s_pool[key] = probe;
            return probe;
        }

        private static void DestroyProbe(Material probe)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(probe);
            else
                UnityEngine.Object.DestroyImmediate(probe);
        }

        private static ulong ComputeSignature(Material material)
        {
            Shader shader = material.shader;
            ulong hash = 14695981039346656037UL;

            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                int id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(material.GetFloat(id)));
                        break;
                    case ShaderPropertyType.Int:
                        hash = Mix(hash, (uint)material.GetInt(id));
                        break;
                    case ShaderPropertyType.Color:
                    {
                        Color color = material.GetColor(id);
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(color.r));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(color.g));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(color.b));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(color.a));
                        break;
                    }
                    case ShaderPropertyType.Vector:
                    {
                        Vector4 vector = material.GetVector(id);
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(vector.x));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(vector.y));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(vector.z));
                        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(vector.w));
                        break;
                    }
                    case ShaderPropertyType.Texture:
                    {
                        Texture texture = material.GetTexture(id);
                        hash = Mix(hash, (ulong)(texture != null ? texture.GetInstanceID() : 0));
                        break;
                    }
                }
            }

            string[] keywords = material.shaderKeywords;
            if (keywords != null && keywords.Length > 0)
            {
                Array.Sort(keywords, StringComparer.Ordinal);
                for (int i = 0; i < keywords.Length; i++)
                {
                    hash = MixString(hash, keywords[i]);
                }
            }

            return hash;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        private static ulong MixString(ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value)) return Mix(hash, uint.MaxValue);
            for (int i = 0; i < value.Length; i++)
            {
                hash = Mix(hash, value[i]);
            }
            return Mix(hash, (uint)value.Length);
        }
    }
}
