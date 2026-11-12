using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A form card on the Input Field tabs: its confirm button lights up once every required field has text.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoInputForm : MonoBehaviour
    {
        [SerializeField] private TMP_InputField[] required;
        [Tooltip("The confirm button's accent layer; it fades in once the form is complete.")]
        [SerializeField] private CanvasGroup confirm;

        const float FadeDuration = 0.2f;

        bool complete;

        void Awake()
        {
            foreach (TMP_InputField input in required)
                input.onValueChanged.AddListener(OnValueChanged);

            complete = IsComplete();
            confirm.alpha = complete ? 1 : 0;
        }

        void OnValueChanged(string _)
        {
            if (IsComplete() == complete)
                return;

            complete = !complete;
            DemoUtils.Fade(confirm, complete ? 1f : 0f, FadeDuration);
        }

        bool IsComplete()
        {
            foreach (TMP_InputField input in required)
            {
                if (string.IsNullOrWhiteSpace(input.text))
                    return false;
            }

            return true;
        }
    }
}
