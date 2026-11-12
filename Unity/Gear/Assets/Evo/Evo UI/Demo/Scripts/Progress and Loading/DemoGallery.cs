using System;
using UnityEngine;
using Evo.Tween;
using Random = UnityEngine.Random;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Spinner (Radial) tab: an image gallery. Each tile shows its spinner until its picture arrives, each at its own time;
    /// then the spinner fades out and the art fades in. Refresh loads them all again.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoGallery : MonoBehaviour
    {
        [Serializable]
        class Tile
        {
            public CanvasGroup spinner;
            public CanvasGroup art;

            [NonSerialized] public float arrives;
            [NonSerialized] public bool loaded;
            [NonSerialized] public bool spinnerOff;
            [NonSerialized] public TweenHandle spinnerFade;
            [NonSerialized] public TweenHandle artFade;
            [NonSerialized] public TweenHandle artScale;
        }

        [SerializeField] private Tile[] tiles;
        [SerializeField] private Vector2 loadTime = new(1.1f, 3.1f);
        [SerializeField] private float jitter = 0.3f;

        const float SpinnerFade = 0.2f;
        const float ArtDuration = 0.35f;
        const float ArtScale = 0.96f;

        int[] order;
        float elapsed;

        void Awake()
        {
            order = new int[tiles.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
        }

        void OnEnable() => Load();

        void OnDisable()
        {
            foreach (Tile tile in tiles)
                Kill(tile);
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;

            foreach (Tile tile in tiles)
            {
                // A hidden spinner would keep rebuilding its shape every frame. Once off, it stays off until the next load
                if (tile.loaded && !tile.spinnerOff && tile.spinner.alpha <= 0)
                {
                    tile.spinnerOff = true;
                    tile.spinner.gameObject.SetActive(false);
                }

                if (tile.loaded || elapsed < tile.arrives)
                    continue;

                tile.loaded = true;
                tile.spinnerFade = DemoUtils.Fade(tile.spinner, 0, SpinnerFade);
                tile.artFade = DemoUtils.Fade(tile.art, 1, ArtDuration);
                tile.artScale = EvoTween.Create(Vector3.one * ArtScale, Vector3.one, ArtDuration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToScale(tile.art.transform);
            }
        }

        // The tiles arrive spread over the load time, in a new order each time
        public void Load()
        {
            if (!isActiveAndEnabled)
                return;

            elapsed = 0;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = tiles[i];
                Kill(tile);
                float step = tiles.Length > 1 ? (float)order[i] / (tiles.Length - 1) : 0;
                tile.arrives = Mathf.Lerp(loadTime.x, loadTime.y, step) + Random.Range(0, jitter);
                tile.loaded = false;
                tile.spinnerOff = false;
                tile.art.alpha = 0;
                tile.art.transform.localScale = Vector3.one * ArtScale;
                tile.spinner.gameObject.SetActive(true);
                tile.spinnerFade = DemoUtils.Fade(tile.spinner, 1, SpinnerFade);
            }
        }

        static void Kill(Tile tile)
        {
            tile.spinnerFade.Kill();
            tile.artFade.Kill();
            tile.artScale.Kill();
        }
    }
}
