using System;
using UnityEngine;
using Evo.UI;

namespace Evo.Tween.Modules.EvoUI
{
    /// <summary>
    /// Event-driven Evo UI binding. The Tween core never references Evo UI types.
    /// </summary>
    sealed class TweenAnimatorInteraction : IInteractionBinding
    {
        readonly TweenAnimator animator;
        readonly Action<InteractionState> stateChanged;
        IStylerInteractable interactable;
        Component source;
        InteractionState previousState;
        bool hasState;

        TweenAnimatorInteraction(TweenAnimator animator)
        {
            this.animator = animator;
            stateChanged = OnStateChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => TweenAnimator.InteractionBindingFactory = Create;

        static IInteractionBinding Create(TweenAnimator animator) => new TweenAnimatorInteraction(animator);

        public void Bind(Component source)
        {
            Unbind();

            if (source == null || source is not IStylerInteractable next)
                return;

            this.source = source;
            interactable = next;
            interactable.OnStateChanged += stateChanged;
            OnStateChanged(interactable.InteractionState);
        }

        public void Unbind()
        {
            if (interactable != null)
                interactable.OnStateChanged -= stateChanged;

            interactable = null;
            source = null;
            hasState = false;
        }

        void OnStateChanged(InteractionState state)
        {
            if (source == null || animator == null || !animator.isActiveAndEnabled || (hasState && state == previousState))
                return;

            previousState = state;
            hasState = true;
            TweenAnimator.InteractionTrigger trigger = state switch
            {
                InteractionState.Normal => TweenAnimator.InteractionTrigger.Normal,
                InteractionState.Highlighted => TweenAnimator.InteractionTrigger.Highlighted,
                InteractionState.Pressed => TweenAnimator.InteractionTrigger.Pressed,
                InteractionState.Selected => TweenAnimator.InteractionTrigger.Selected,
                InteractionState.Disabled => TweenAnimator.InteractionTrigger.Disabled,
                _ => TweenAnimator.InteractionTrigger.None
            };
            animator.TriggerInteraction(trigger);
        }
    }
}