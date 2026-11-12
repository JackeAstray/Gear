using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Shadows tab: four 3D keys, each one shape whose outer shadow is its side and whose inner shadow presses it in.
    /// The options set the side's height and softness and the pressed shadow's depth and softness on every key,
    /// starting from the first key's values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeShadows : MonoBehaviour
    {
        [SerializeField] private DemoShapeKey[] keys;

        [Header("Options")]
        [SerializeField] private Slider height;
        [SerializeField] private Slider softness;
        [SerializeField] private Slider depth;
        [SerializeField] private Slider pressedSoftness;

        void Awake()
        {
            DemoShapeKey first = keys[0];
            DemoUtils.Show(height, first.Height);
            DemoUtils.Show(softness, first.Softness);
            DemoUtils.Show(depth, first.Depth);
            DemoUtils.Show(pressedSoftness, first.PressedSoftness);
        }

        public void SetHeight(float value)
        {
            foreach (DemoShapeKey key in keys)
                key.Height = value;
        }

        public void SetSoftness(float value)
        {
            foreach (DemoShapeKey key in keys)
                key.Softness = value;
        }

        public void SetDepth(float value)
        {
            foreach (DemoShapeKey key in keys)
                key.Depth = value;
        }

        public void SetPressedSoftness(float value)
        {
            foreach (DemoShapeKey key in keys)
                key.PressedSoftness = value;
        }
    }
}
