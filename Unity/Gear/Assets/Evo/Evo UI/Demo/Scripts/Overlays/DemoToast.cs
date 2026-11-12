using System.Collections.Generic;
using Evo.Tween;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Toast Notification tab: a game view with floating pickups (each bobs on its own Tween Animator). Collecting one creates
    /// a toast from the hidden template with Notification.Create, and the pickup comes back somewhere else a moment later.
    /// Options change the template, so they apply to every toast sent after. A click on a toast closes it (an Event Trigger on the template).
    /// A pill beside the toast on screen counts the toasts waiting.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoToast : MonoBehaviour
    {
        const int PopKey = 1;

        [System.Serializable]
        public struct Message
        {
            public Sprite icon;
            public string title;
            [TextArea] public string description;
        }

        [System.Serializable]
        public class Pickup
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Message message;
            [HideInInspector] public int spot;
            [HideInInspector] public float respawnAt;
            [HideInInspector] public bool collected;
        }

        [SerializeField] private Notification template;
        [SerializeField] private Transform container;
        [SerializeField] private Pickup[] pickups;
        [Tooltip("Places a pickup can float at, in the game view's space.")]
        [SerializeField] private Vector2[] spots;
        [SerializeField] private float respawnDelay = 3.5f;

        [Header("Queue")]
        [SerializeField] private CanvasGroup queue;
        [SerializeField] private TMP_Text queueText;

        [Header("Options")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private CanvasGroup delayOption;

        readonly List<Notification> sent = new();
        // Each sent toast's Canvas Group, found once instead of every frame
        readonly List<CanvasGroup> sentGroups = new();
        readonly List<int> freeSpots = new();
        int shownWaiting = -1;

        void Awake()
        {
            DemoUtils.SetLocked(delayOption, !template.autoClose, false);

            for (int i = 0; i < pickups.Length; i++)
            {
                pickups[i].spot = i;
                pickups[i].rect.anchoredPosition = spots[i];
            }
        }

        void OnEnable()
        {
            queue.alpha = 0;
            UpdateInfo();
        }

        void Update()
        {
            float time = Time.unscaledTime;

            foreach (Pickup pickup in pickups)
            {
                if (pickup.collected && time >= pickup.respawnAt)
                    Respawn(pickup);
            }

            UpdateToasts();
        }

        // Called by each pickup's Interactive
        public void Collect(int index)
        {
            Pickup pickup = pickups[index];
            if (pickup.collected)
                return;

            pickup.collected = true;
            pickup.respawnAt = Time.unscaledTime + respawnDelay;
            pickup.group.blocksRaycasts = false;
            Send(pickup.message);

            EvoTween.Create(pickup.rect.localScale.x, 1.35f, 0.22f)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(pickup.rect, static (t, s) => t.localScale = new Vector3(s, s, 1), PopKey);
            DemoUtils.Fade(pickup.group, 0f, 0.22f);
        }

        void Respawn(Pickup pickup)
        {
            // Any spot no other pickup is on, other than the one it just left
            freeSpots.Clear();
            for (int s = 0; s < spots.Length; s++)
            {
                bool taken = s == pickup.spot;
                foreach (Pickup other in pickups)
                    taken |= other != pickup && other.spot == s;
                if (!taken)
                    freeSpots.Add(s);
            }

            if (freeSpots.Count > 0)
                pickup.spot = freeSpots[Random.Range(0, freeSpots.Count)];

            pickup.collected = false;
            pickup.group.blocksRaycasts = true;
            pickup.rect.anchoredPosition = spots[pickup.spot];
            EvoTween.Create(0.4f, 1f, 0.32f)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(pickup.rect, static (t, s) => t.localScale = new Vector3(s, s, 1), PopKey);
            EvoTween.Create(0f, 1f, 0.2f)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(pickup.group);
        }

        void Send(Message message)
        {
            // Without the queue, a new toast takes the place of the one on screen
            if (!template.enableStacking)
            {
                for (int i = 0; i < sent.Count; i++)
                {
                    if (sent[i] != null)
                        sent[i].Close();
                }
            }

            Notification toast = Notification.Create(template.gameObject, message.icon, message.title, message.description, container);
            if (toast != null)
            {
                sent.Add(toast);
                sentGroups.Add(toast.GetComponent<CanvasGroup>());
            }

            // The pill stays on top of the toasts
            queue.transform.SetAsLastSibling();
        }

        // Queued toasts are active but hidden until the one on screen closes
        bool IsWaiting(int index) => sent[index].gameObject.activeSelf && !sent[index].IsOpen && sentGroups[index].alpha <= 0;

        void UpdateToasts()
        {
            int waiting = 0;
            Notification open = null;
            for (int i = sent.Count - 1; i >= 0; i--)
            {
                // Created toasts destroy themselves once they close
                if (sent[i] == null)
                {
                    sent.RemoveAt(i);
                    sentGroups.RemoveAt(i);
                }
                else if (IsWaiting(i)) { waiting++; }
                else if (sent[i].IsOpen) { open = sent[i]; }
            }

            // The pill sits beside the toast on screen, on the side facing the middle of the view
            bool showQueue = waiting > 0 && open != null;
            queue.alpha = showQueue ? 1 : 0;
            if (showQueue)
            {
                var toast = (RectTransform)open.transform;
                var pill = (RectTransform)queue.transform;
                bool right = toast.pivot.x > 0.75f;
                bool bottom = toast.pivot.y < 0.25f;
                pill.anchorMin = pill.anchorMax = toast.anchorMin;
                pill.pivot = new Vector2(right ? 1 : 0, bottom ? 0 : 1);
                float x = right ? -toast.rect.width - 12 : toast.pivot.x > 0.25f ? toast.rect.width / 2 + 12 : toast.rect.width + 12;
                pill.anchoredPosition = toast.anchoredPosition + new Vector2(x, 0);
            }

            if (waiting != shownWaiting)
            {
                if (waiting > 0)
                    queueText.SetText("+{0}", waiting);
                UpdateInfo(waiting);
            }
        }

        // Top left, top, top right, bottom left, bottom or bottom right
        public void SetPosition(int index)
        {
            var corner = new Vector2(index % 3 * 0.5f, index < 3 ? 1 : 0);
            Place((RectTransform)template.transform, corner);

            for (int i = 0; i < sent.Count; i++)
            {
                if (sent[i] != null)
                    Place((RectTransform)sent[i].transform, corner);
            }
        }

        public void SetAutoClose(bool value)
        {
            template.autoClose = value;
            DemoUtils.SetLocked(delayOption, !value);
            UpdateInfo();
        }

        public void SetDelay(float seconds) => template.autoCloseDelay = seconds;

        public void SetQueue(bool value)
        {
            template.enableStacking = value;

            for (int i = 0; i < sent.Count; i++)
            {
                Notification toast = sent[i];
                if (toast == null)
                    continue;

                // Turning the queue off drops the toasts still waiting
                if (!value && IsWaiting(i)) { toast.ForceClose(); }
                else { toast.enableStacking = value; }
            }

            UpdateInfo();
        }

        public void SetSounds(bool value) => template.soundEffects = value;

        static void Place(RectTransform toast, Vector2 corner)
        {
            toast.anchorMin = toast.anchorMax = toast.pivot = corner;
            toast.anchoredPosition = Vector2.zero;
        }

        void UpdateInfo() => UpdateInfo(shownWaiting < 0 ? 0 : shownWaiting);

        void UpdateInfo(int waiting)
        {
            shownWaiting = waiting;

            if (waiting > 0) { info.SetText(waiting == 1 ? "1 toast waiting in the queue" : "{0} toasts waiting in the queue", waiting); }
            else if (!template.autoClose) { info.SetText("Auto close is off: click a toast to close it."); }
            else if (template.enableStacking) { info.SetText("Collect a few pickups in a row: toasts wait their turn."); }
            else { info.SetText("Queue is off: a new toast replaces the one on screen."); }
        }
    }
}
