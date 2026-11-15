using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// StylePage 预算诊断：统计一个 Canvas/根节点下 Graphic 的唯一材质数量，
    /// 用于判断是否值得做单 Canvas 单材质/图集优化（M4.3 的“先量后做”）。
    /// </summary>
    [AddComponentMenu("UI/Gear/Style Page Budget")]
    [DisallowMultipleComponent]
    public sealed class GearStylePageBudget : MonoBehaviour
    {
        public readonly struct Report
        {
            public readonly int Graphics;
            public readonly int Materials;
            public readonly int Textures;

            public Report(int graphics, int materials, int textures)
            {
                Graphics = graphics;
                Materials = materials;
                Textures = textures;
            }

            public override string ToString()
            {
                return $"graphics={Graphics}, materials={Materials}, textures={Textures}";
            }
        }

        [SerializeField] private Canvas canvas;

        public Canvas Canvas
        {
            get => canvas != null ? canvas : GetComponentInParent<Canvas>();
            set => canvas = value;
        }

        public Report Analyze()
        {
            Canvas target = Canvas;
            Graphic[] graphics = target != null
                ? target.GetComponentsInChildren<Graphic>(true)
                : GetComponentsInChildren<Graphic>(true);

            var materials = new HashSet<int>();
            var textures = new HashSet<int>();
            int count = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;
                count++;
                Material material = graphic.material;
                if (material != null) materials.Add(material.GetInstanceID());
                Texture texture = graphic.mainTexture;
                if (texture != null) textures.Add(texture.GetInstanceID());
            }

            return new Report(count, materials.Count, textures.Count);
        }
    }
}
