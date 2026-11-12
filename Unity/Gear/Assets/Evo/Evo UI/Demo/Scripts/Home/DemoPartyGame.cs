using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample party game menu. Its blocks, stickers, planet, patterns and timer run on Tween Animators and a Timer;
    /// this moves the moon along the dashed orbit, in front of the planet on the near side and behind it on the far side.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoPartyGame : DemoScreenContent
    {
        [Tooltip("The dashed orbit; the moon follows the middle of its outline.")]
        [SerializeField] private ProceduralShape orbit;
        [SerializeField] private RectTransform moon;
        [SerializeField] private RectTransform planet;
        [SerializeField, Min(0.1f)] private float orbitDuration = 7;

        const int OrbitProperty = 31801;
        TweenHandle orbitTween;
        Vector2 orbitCenter, orbitRadius;
        float orbitCos, orbitSin;
        bool moonInFront;

        void Awake()
        {
            // The moon rides the middle of the dashed line, around the orbit's own rotation
            RectTransform orbitRect = orbit.rectTransform;
            float angle = orbitRect.localEulerAngles.z * Mathf.Deg2Rad;
            orbitCenter = orbitRect.anchoredPosition;
            orbitRadius = orbitRect.rect.size * 0.5f - Vector2.one * (orbit.OutlineWidth * 0.5f);
            orbitCos = Mathf.Cos(angle);
            orbitSin = Mathf.Sin(angle);
            moonInFront = moon.GetSiblingIndex() > planet.GetSiblingIndex();
        }

        void OnEnable()
        {
            orbitTween = EvoTween.Create(0f, Mathf.PI * 2, orbitDuration)
                .WithLoops(0)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (game, angle) => game.MoveMoon(angle), OrbitProperty);
        }

        void OnDisable() => orbitTween.Kill();

        void MoveMoon(float angle)
        {
            float x = Mathf.Cos(angle) * orbitRadius.x;
            float y = -Mathf.Sin(angle) * orbitRadius.y;
            moon.anchoredPosition = orbitCenter + new Vector2(x * orbitCos - y * orbitSin, x * orbitSin + y * orbitCos);

            // Lower half of the orbit passes in front of the planet, upper half behind it
            bool front = y < 0;
            if (front == moonInFront)
                return;

            moonInFront = front;
            if (front)
                moon.SetAsLastSibling();
            else
                moon.SetSiblingIndex(planet.GetSiblingIndex());
        }
    }
}
