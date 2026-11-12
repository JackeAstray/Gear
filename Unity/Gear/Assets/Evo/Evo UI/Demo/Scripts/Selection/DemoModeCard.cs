using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dropdown tab: a game mode card. The art shows the picked mode's icon, which pops in on a change.
    /// Each mode keeps its icon here, so the Icons option only changes the list.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoModeCard : MonoBehaviour
    {
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private RectTransform art;
        [SerializeField] private Image icon;
        [SerializeField] private float popScale = 0.85f;
        [SerializeField] private float popDuration = 0.35f;

        readonly Dictionary<Dropdown.Item, Sprite> icons = new();
        TweenHandle pop;

        void Awake()
        {
            foreach (Dropdown.Item item in dropdown.items)
                icons[item] = item.icon;

            dropdown.onItemSelected.AddListener(OnItemSelected);
        }

        void OnDisable()
        {
            pop.Kill();
            art.localScale = Vector3.one;
        }

        void OnItemSelected(int index)
        {
            if (dropdown.SelectedItem == null || !icons.TryGetValue(dropdown.SelectedItem, out Sprite sprite) || icon.sprite == sprite)
                return;

            icon.sprite = sprite;
            pop.Kill();
            pop = EvoTween.Create(Vector3.one * popScale, Vector3.one, popDuration)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToScale(art);
        }
    }
}
