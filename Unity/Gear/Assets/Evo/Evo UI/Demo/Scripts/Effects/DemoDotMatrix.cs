using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dot Matrix Loader tab: the loader runs in a matchmaking card, next to a status pill whose dot blinks on a Tween Animator.
    /// Four of the loader's patterns, dot shape and size, grid size and motion.
    /// Trail locks for Equalizer, which doesn't use it. The options start from the loader's own values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoDotMatrix : MonoBehaviour
    {
        [SerializeField] private DotMatrixLoader loader;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup pattern;
        [SerializeField] private RadioButtonGroup shape;
        [SerializeField] private Slider gridSize;
        [SerializeField] private Slider dotSize;
        [SerializeField] private Slider speed;
        [SerializeField] private Slider trail;
        [Tooltip("Locked while the pattern is Equalizer.")]
        [SerializeField] private CanvasGroup trailOption;

        static readonly DotMatrixLoader.PatternType[] Patterns =
        {
            DotMatrixLoader.PatternType.Rings, DotMatrixLoader.PatternType.Radar,
            DotMatrixLoader.PatternType.Spiral, DotMatrixLoader.PatternType.Equalizer,
        };

        void Awake()
        {
            DemoUtils.Show(pattern, System.Array.IndexOf(Patterns, loader.Pattern));
            DemoUtils.Show(shape, (int)loader.Shape);
            DemoUtils.Show(gridSize, loader.GridSize.x);
            DemoUtils.Show(dotSize, loader.DotSize * 100f);
            DemoUtils.Show(speed, loader.Speed);
            DemoUtils.Show(trail, loader.TrailLength * 100f);
            DemoUtils.SetLocked(trailOption, loader.Pattern == DotMatrixLoader.PatternType.Equalizer, false);
        }

        public void SetPattern(int index)
        {
            loader.Pattern = Patterns[index];
            DemoUtils.SetLocked(trailOption, loader.Pattern == DotMatrixLoader.PatternType.Equalizer);
        }

        public void SetShape(int index) => loader.Shape = (DotMatrixLoader.DotShape)index;

        public void SetGridSize(float value)
        {
            int size = Mathf.RoundToInt(value);
            loader.GridSize = new Vector2Int(size, size);
        }

        // Dot size and trail are percent bars
        public void SetDotSize(float value) => loader.DotSize = value / 100f;
        public void SetTrail(float value) => loader.TrailLength = value / 100f;
    }
}