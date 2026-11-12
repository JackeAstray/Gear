using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Button tab: a connected accounts list with one button per row, Regular or Outline by the Type option.
    /// Each button connects or disconnects its account; the label and icon follow the state.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoAccounts : DemoButtonPage
    {
        [Serializable]
        class Account
        {
            public bool connected;
            public RectTransform chip;
            [Tooltip("Accent chip and check badge, shown while connected.")]
            public CanvasGroup connectedLayer;
        }

        [SerializeField] private Account[] accounts;
        [SerializeField] private string connectText = "Connect";
        [SerializeField] private Sprite connectIcon;
        [SerializeField] private string disconnectText = "Disconnect";
        [SerializeField] private Sprite disconnectIcon;

        const float FadeDuration = 0.2f;
        const float PopDuration = 0.35f;
        const float PopScale = 0.85f;

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < accounts.Length; i++)
                Show(i, false);
        }

        public void Toggle(int index)
        {
            accounts[index].connected = !accounts[index].connected;
            Show(index, true);
        }

        void Show(int index, bool animate)
        {
            Account account = accounts[index];
            SetLabel(index, account.connected ? disconnectText : connectText, account.connected ? disconnectIcon : connectIcon);

            float alpha = account.connected ? 1f : 0f;
            if (!animate)
            {
                account.connectedLayer.alpha = alpha;
                return;
            }

            DemoUtils.Fade(account.connectedLayer, alpha, FadeDuration);

            if (account.connected)
            {
                EvoTween.Create(Vector3.one * PopScale, Vector3.one, PopDuration)
                    .WithEase(Ease.OutBack)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToScale(account.chip);
            }
        }
    }
}
