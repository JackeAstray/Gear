using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Base for a sample screen's own script. The deck turns hidden screens off, so it only runs while its screen shows.
    /// </summary>
    public abstract class DemoScreenContent : MonoBehaviour
    {
        bool focused;

        /// <summary>
        /// Seconds the screen has been visible.
        /// </summary>
        protected float Elapsed { get; private set; }

        /// <summary>
        /// Open beside the info column instead of in the deck.
        /// </summary>
        public bool Focused
        {
            get => focused;
            set
            {
                if (focused == value)
                    return;

                focused = value;
                OnFocusChanged(value);
            }
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            Elapsed += delta;
            Tick(delta);
        }

        protected virtual void Tick(float delta) { }

        /// <summary>
        /// Called when the screen opens beside the info column and when it goes back to the deck.
        /// </summary>
        protected virtual void OnFocusChanged(bool value) { }
    }
}