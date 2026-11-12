using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Pixel Curtain tab: three face-down cards. A click reveals a card or hides it again; Shuffle hides all three
    /// and deals new art. On its own, the tab reveals the cards one by one, holds, then shuffles; a card click pauses that until
    /// the next Shuffle. The options start from the first curtain.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoPixelCurtain : MonoBehaviour
    {
        [SerializeField] private PixelCurtain[] curtains;
        [SerializeField] private Image[] images;
        [SerializeField] private Sprite[] sprites;

        [Header("Auto Shuffle")]
        [SerializeField] private float firstDelay = 1f;
        [SerializeField] private float revealDelay = 0.6f;
        [SerializeField] private float holdDuration = 2.2f;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup direction;
        [SerializeField] private Slider cellSize;
        [SerializeField] private Slider randomness;
        [SerializeField] private Slider fadeWidth;
        [SerializeField] private Slider duration;

        static readonly Vector2[] Directions = { new(0f, -1f), new(1f, 0f), new Vector2(1f, -1f).normalized, Vector2.zero };

        int deal;
        Coroutine routine;
        readonly List<Deal> deals = new();

        void Awake()
        {
            PixelCurtain first = curtains[0];
            DemoUtils.Show(direction, System.Array.FindIndex(Directions, d => d == first.direction));
            DemoUtils.Show(cellSize, first.cellSize);
            DemoUtils.Show(randomness, first.randomness);
            DemoUtils.Show(fadeWidth, first.fadeWidth * 100f);
            DemoUtils.Show(duration, first.transitionDuration);
        }

        void OnEnable()
        {
            // Cards always come back face down, so a reveal cut short by leaving the tab never stays half drawn
            foreach (PixelCurtain curtain in curtains)
            {
                curtain.progress = 0f;
                curtain.UpdateMaterialProperties();
            }

            Restart(AutoShuffle());
        }

        void OnDisable()
        {
            routine = null;

            // Closing the page stopped every coroutine here, deals included
            if (!gameObject.activeInHierarchy)
            {
                foreach (Deal pending in deals)
                    pending.running = false;
            }
        }

        // Card buttons
        public void Toggle(int index)
        {
            Restart(null);
            PixelCurtain curtain = curtains[index];
            if (curtain.progress > 0.5f) { curtain.PlayOut(); }
            else { curtain.PlayIn(); }
        }

        public void Shuffle() => Restart(ShuffleThenContinue());

        void Restart(IEnumerator next)
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = next != null && isActiveAndEnabled ? StartCoroutine(next) : null;
        }

        IEnumerator ShuffleThenContinue()
        {
            yield return HideAndDeal();
            yield return AutoShuffle();
        }

        // The waits are written out instead of nested, so the running loop allocates nothing
        IEnumerator AutoShuffle()
        {
            while (true)
            {
                float wait = firstDelay;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                    yield return null;

                for (int i = 0; i < curtains.Length; i++)
                {
                    if (i > 0)
                    {
                        wait = revealDelay;
                        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                            yield return null;
                    }

                    curtains[i].PlayIn();
                    while (curtains[i].progress < 1f)
                        yield return null;
                }

                wait = holdDuration;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                    yield return null;

                yield return HideAndDeal();
            }
        }

        // A deal still runs as its own nested coroutine, so one that a restart cuts off finishes like before;
        // the enumerators are reused once done
        Deal HideAndDeal()
        {
            Deal next = null;
            foreach (Deal pending in deals)
            {
                if (!pending.running)
                {
                    next = pending;
                    break;
                }
            }

            if (next == null)
            {
                next = new Deal(this);
                deals.Add(next);
            }

            next.Begin();
            return next;
        }

        // Every card hides, then the art moves along by one so each deal shows a new set
        sealed class Deal : IEnumerator
        {
            readonly DemoPixelCurtain owner;
            int waitingOn;
            public bool running;

            public Deal(DemoPixelCurtain owner) => this.owner = owner;

            public object Current => null;

            public void Begin()
            {
                running = true;
                waitingOn = -1;
            }

            public bool MoveNext()
            {
                PixelCurtain[] curtains = owner.curtains;
                if (waitingOn < 0)
                {
                    foreach (PixelCurtain curtain in curtains)
                        curtain.PlayOut();

                    waitingOn = 0;
                }

                for (; waitingOn < curtains.Length; waitingOn++)
                {
                    if (curtains[waitingOn].progress > 0f)
                        return true;
                }

                owner.deal++;
                for (int i = 0; i < owner.images.Length; i++)
                    owner.images[i].sprite = owner.sprites[(i + owner.deal) % owner.sprites.Length];

                running = false;
                return false;
            }

            public void Reset() { }
        }

        public void SetDirection(int index)
        {
            foreach (PixelCurtain curtain in curtains)
            {
                curtain.direction = Directions[index];
                curtain.UpdateMaterialProperties();
            }
        }

        public void SetCellSize(float value)
        {
            foreach (PixelCurtain curtain in curtains)
            {
                curtain.cellSize = value;
                curtain.UpdateMaterialProperties();
            }
        }

        public void SetRandomness(float value)
        {
            foreach (PixelCurtain curtain in curtains)
            {
                curtain.randomness = value;
                curtain.UpdateMaterialProperties();
            }
        }

        // Percent bar
        public void SetFadeWidth(float value)
        {
            foreach (PixelCurtain curtain in curtains)
            {
                curtain.fadeWidth = value / 100f;
                curtain.UpdateMaterialProperties();
            }
        }

        public void SetDuration(float value)
        {
            foreach (PixelCurtain curtain in curtains)
                curtain.transitionDuration = value;
        }
    }
}
