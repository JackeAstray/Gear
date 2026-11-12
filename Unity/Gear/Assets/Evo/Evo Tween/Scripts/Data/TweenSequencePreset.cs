using System.Collections.Generic;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Shareable ordered clip definitions used by any <see cref="TweenSequence"/>.
    /// Targets remain local to each consuming sequence.
    /// </summary>
    [CreateAssetMenu(fileName = "Tween Sequence Preset", menuName = "Evo/Tween/Sequence Preset")]
    public sealed class TweenSequencePreset : ScriptableObject
    {
        [SerializeField]
        private List<TweenClip> clips = new();

        /// <summary>
        /// Ordered clip definitions shared by every sequence referencing this asset.
        /// </summary>
        public List<TweenClip> Clips => clips ??= new List<TweenClip>();

        /// <summary>
        /// Number of clip definitions stored by this preset.
        /// </summary>
        public int Count => Clips.Count;

        /// <summary>
        /// Replaces this preset's definitions with independent copies. Targets are discarded.
        /// </summary>
        public void CopyFrom(IReadOnlyList<TweenClip> source)
        {
            clips = new List<TweenClip>(source?.Count ?? 0);
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                TweenClip clip = source[i]?.Clone();
                if (clip != null)
                    clip.Target = null;

                clips.Add(clip);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            clips ??= new List<TweenClip>();
            for (int i = 0; i < clips.Count; i++)
            {
                TweenClip clip = clips[i];
                if (clip == null)
                    continue;

                clip.Target = null;
                clip.Validate();
            }
        }
#endif
    }
}