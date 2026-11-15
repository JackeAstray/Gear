using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 按钮组悬停变暗：指针进入某一项时，其余成员降到 DimAlpha；离开后恢复。
    /// 支持直接 SetHover，便于手柄焦点或外部状态驱动。
    /// </summary>
    [AddComponentMenu("UI/Gear/Button Group Dimmer")]
    [DisallowMultipleComponent]
    public sealed class GearButtonGroupDimmer : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private List<Graphic> members = new List<Graphic>();
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.4f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.12f;

        private readonly List<float> baseAlphas = new List<float>();
        private int hoveredIndex = -1;

        public IReadOnlyList<Graphic> Members => members;
        public int HoveredIndex => hoveredIndex;

        private void OnEnable()
        {
            CacheBaseAlphas();
        }

        public void Configure(IList<Graphic> graphics)
        {
            members.Clear();
            if (graphics != null)
            {
                for (int i = 0; i < graphics.Count; i++)
                {
                    if (graphics[i] != null) members.Add(graphics[i]);
                }
            }

            CacheBaseAlphas();
            ApplyImmediate();
        }

        public void SetHover(int index, bool hovered)
        {
            hoveredIndex = hovered ? Mathf.Clamp(index, 0, Mathf.Max(0, members.Count - 1)) : -1;
            if (fadeDuration <= 0f) ApplyImmediate();
        }

        public void Tick(float delta)
        {
            if (members.Count == 0) return;
            float speed = fadeDuration <= 0f ? 1f : Mathf.Max(0f, delta) / fadeDuration;
            for (int i = 0; i < members.Count; i++)
            {
                Graphic member = members[i];
                if (member == null) continue;
                float baseAlpha = i < baseAlphas.Count ? baseAlphas[i] : 1f;
                float target = hoveredIndex >= 0 && i != hoveredIndex ? baseAlpha * dimAlpha : baseAlpha;
                Color color = member.color;
                color.a = Mathf.MoveTowards(color.a, target, speed);
                member.color = color;
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            int index = FindMember(eventData != null && eventData.pointerEnter != null
                ? eventData.pointerEnter.transform
                : null);
            if (index >= 0) SetHover(index, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            int index = FindMember(eventData != null && eventData.pointerEnter != null
                ? eventData.pointerEnter.transform
                : null);
            if (index >= 0 && index == hoveredIndex) SetHover(index, false);
        }

        private int FindMember(Transform hovered)
        {
            if (hovered == null) return -1;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && hovered.IsChildOf(members[i].transform)) return i;
            }

            return -1;
        }

        private void CacheBaseAlphas()
        {
            baseAlphas.Clear();
            for (int i = 0; i < members.Count; i++)
                baseAlphas.Add(members[i] != null ? members[i].color.a : 1f);
        }

        private void ApplyImmediate()
        {
            for (int i = 0; i < members.Count; i++)
            {
                Graphic member = members[i];
                if (member == null) continue;
                float baseAlpha = i < baseAlphas.Count ? baseAlphas[i] : 1f;
                Color color = member.color;
                color.a = hoveredIndex >= 0 && i != hoveredIndex ? baseAlpha * dimAlpha : baseAlpha;
                member.color = color;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            dimAlpha = Mathf.Clamp01(dimAlpha);
            fadeDuration = Mathf.Max(0f, fadeDuration);
            CacheBaseAlphas();
        }
#endif
    }
}
