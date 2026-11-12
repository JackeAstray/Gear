using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Rect Parallax tab: each floating piece has its own Rect Parallax with a depth factor, so nearer pieces move further.
    /// Depth sets the multiplier of every layer through its factor. The options start from the first layer.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRectParallax : MonoBehaviour
    {
        [SerializeField] private RectParallax[] layers;
        [Tooltip("Depth factor of each layer, in the same order.")]
        [SerializeField] private float[] factors;

        [Header("Options")]
        [SerializeField] private Slider depth;
        [SerializeField] private Slider follow;

        void Awake()
        {
            DemoUtils.Show(depth, layers[0].depthMultiplier / factors[0]);
            DemoUtils.Show(follow, layers[0].followSpeed);
        }

        public void SetDepth(float value)
        {
            for (int i = 0; i < layers.Length; i++)
                layers[i].depthMultiplier = value * factors[i];
        }

        public void SetFollow(float value)
        {
            foreach (RectParallax layer in layers)
                layer.followSpeed = value;
        }
    }
}