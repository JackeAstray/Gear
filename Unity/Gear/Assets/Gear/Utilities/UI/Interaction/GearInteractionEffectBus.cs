using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>
    /// 交互效果总线：订阅 GearInteractive.StateChanged，按 Priority 降序分发给效果；
    /// 相同 Channel 默认只执行优先级最高的一项，避免重复写同一 uniform。
    /// </summary>
    [AddComponentMenu("UI/Gear/Interaction Effect Bus")]
    [DisallowMultipleComponent]
    public sealed class GearInteractionEffectBus : MonoBehaviour
    {
        [SerializeField] private GearInteractive source;
        [SerializeField] private List<MonoBehaviour> serializedEffects = new List<MonoBehaviour>();
        [SerializeField] private bool dedupeChannels = true;

        private readonly List<IGearInteractionEffect> effects = new List<IGearInteractionEffect>();
        private readonly HashSet<string> usedChannels = new HashSet<string>();

        public GearInteractionState LastState { get; private set; } = GearInteractionState.Normal;
        public int DispatchCount { get; private set; }
        public IReadOnlyList<IGearInteractionEffect> Effects => effects;
        public bool DedupeChannels { get => dedupeChannels; set => dedupeChannels = value; }

        public GearInteractive Source
        {
            get => source;
            set { Unsubscribe(); source = value; Subscribe(); }
        }

        private void OnEnable()
        {
            Collect();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (source == null) source = GetComponent<GearInteractive>();
            if (source == null) return;
            source.StateChanged -= HandleState;
            source.StateChanged += HandleState;
            LastState = source.State;
        }

        private void Unsubscribe()
        {
            if (source != null) source.StateChanged -= HandleState;
        }

        public void Register(IGearInteractionEffect effect)
        {
            if (effect == null || effects.Contains(effect)) return;
            effects.Add(effect);
            effects.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void Unregister(IGearInteractionEffect effect)
        {
            if (effect != null) effects.Remove(effect);
        }

        /// <summary>手动派发；测试或外部状态机可用。</summary>
        public void Dispatch(GearInteractionState state)
        {
            LastState = state;
            usedChannels.Clear();
            for (int i = 0; i < effects.Count; i++)
            {
                IGearInteractionEffect effect = effects[i];
                if (effect == null) continue;
                string channel = effect.Channel;
                if (dedupeChannels && !string.IsNullOrEmpty(channel))
                {
                    if (usedChannels.Contains(channel)) continue;
                    usedChannels.Add(channel);
                }

                effect.OnInteraction(state, source);
            }

            DispatchCount++;
        }

        private void HandleState(GearInteractionState state) => Dispatch(state);

        private void Collect()
        {
            effects.Clear();
            for (int i = 0; i < serializedEffects.Count; i++)
            {
                if (serializedEffects[i] is IGearInteractionEffect effect) effects.Add(effect);
            }

            if (effects.Count == 0)
            {
                MonoBehaviour[] found = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] is IGearInteractionEffect effect && !ReferenceEquals(found[i], this))
                        effects.Add(effect);
                }
            }

            effects.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}
