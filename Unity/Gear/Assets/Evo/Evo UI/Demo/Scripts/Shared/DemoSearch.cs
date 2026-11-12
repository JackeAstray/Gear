using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The header search. Typing in the field drops a list of matching demo pages under it, and picking one
    /// opens its category and variant the same way the dock and the sidebar do. Pages are read from the scene
    /// (each Content tab's Variants and Tabs), so new pages show up without extra setup.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSearch : MonoBehaviour
    {
        [Serializable]
        public class Keywords
        {
            public string page;
            [Tooltip("Comma separated. Other names people may type for this page.")]
            public string words;
        }

        [Header("Field")]
        [SerializeField] private TMP_InputField input;
        [SerializeField] private RectTransform field;
        [SerializeField] private GameObject shortcutHint;
        [SerializeField] private TMP_Text shortcutText;
        [SerializeField] private GameObject clearButton;

        [Header("Results")]
        [SerializeField] private RectTransform panel;
        [Tooltip("The root canvas, for its event camera.")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private UnityEngine.UI.ScrollRect scrollRect;
        [SerializeField] private RectTransform list;
        [SerializeField] private RectTransform highlight;
        [SerializeField] private DemoSearchResult resultPrefab;
        [SerializeField] private RectTransform groupPrefab;
        [SerializeField] private float maxListHeight = 660f;
        [SerializeField] private RectTransform footer;

        [Header("Message")]
        [SerializeField] private VerticalLayoutGroup message;
        [SerializeField] private TMP_Text messageTitle;

        [Header("Pages")]
        [SerializeField] private TabManager pages;
        [SerializeField] private List<Keywords> keywords = new();

        [Header("Motion")]
        [SerializeField, Min(0f)] private float slideDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.1f;
        [SerializeField, Min(0f)] private float openOffset = 20f;

        const int PageStep = 6;
        const int WidthKey = 1, HighlightKey = 2, PanelKey = 3;
        const float RepeatDelay = 0.4f;
        const float RepeatInterval = 0.06f;

        readonly DemoSearchIndex index = new();
        readonly List<DemoSearchIndex.Result> results = new();
        readonly List<DemoSearchIndex.Page> shown = new();
        readonly List<DemoSearchResult> rows = new();
        readonly List<DemoSearchResult> rowPool = new();
        readonly List<RectTransform> groupPool = new();
        readonly List<Image> groupIcons = new();
        readonly List<TMP_Text> groupLabels = new();
        readonly List<int> categoryOrder = new();
        readonly StringBuilder text = new();
        readonly StringBuilder detail = new();

        bool indexed, isOpen, refocus, caretToEnd, hasAccent;
        int selected = -1, usedGroups, slashFrame = -10;
        float closedWidth, fromHighlightY, toHighlightY, fromHighlightH, toHighlightH, panelFrom, panelTo;
        Vector2 panelPosition;
        string accentTag = "<color=#64B4FF>";
        string searchAllText;
        Color accentColor;
        Key repeatKey = Key.None;
        float repeatTime;
        TweenHandle widthTween, highlightTween, panelTween;
        Action hideClosedPanel;

        void Awake()
        {
            hideClosedPanel = () => { if (!isOpen) panel.gameObject.SetActive(false); };
            closedWidth = field.sizeDelta.x;
            panelPosition = panel.anchoredPosition;
            input.onFocusSelectAll = false;
            input.onSelect.AddListener(OnFieldSelect);
            input.onDeselect.AddListener(OnFieldDeselect);
            input.onValueChanged.AddListener(OnValueChanged);
            input.onSubmit.AddListener(OnSubmit);

            if (shortcutText != null)
                shortcutText.text = Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer ? "Cmd K" : "Ctrl K";

            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
            SyncField();
        }

        void Start() => BuildIndex();

        void OnDisable()
        {
            widthTween.Kill();
            highlightTween.Kill();
            panelTween.Kill();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (ShortcutPressed(keyboard))
            {
                Focus();
                return;
            }

            if (!isOpen)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (!input.isFocused)
                return;

            int step = 0;
            if (Repeat(keyboard, Key.DownArrow)) { step = 1; }
            else if (Repeat(keyboard, Key.UpArrow)) { step = -1; }
            else if (keyboard.pageDownKey.wasPressedThisFrame) { step = PageStep; }
            else if (keyboard.pageUpKey.wasPressedThisFrame) { step = -PageStep; }

            if (step != 0 && rows.Count > 0)
                Select(Mathf.Clamp(selected + step, 0, rows.Count - 1), true);

            // The field moves its caret to the start or end on arrows, key repeats included; put it back after it does
            if (step != 0 || keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed)
                caretToEnd = true;
        }

        void LateUpdate()
        {
            // A click inside the panel takes selection from the field for a moment; give it back so typing goes on
            if (refocus)
            {
                refocus = false;
                if (isOpen)
                {
                    Utilities.SetSelectedObject(input.gameObject);
                    input.ActivateInputField();
                    caretToEnd = true;
                }
            }
            else if (caretToEnd && input.isFocused)
            {
                caretToEnd = false;
                input.MoveTextEnd(false);
            }
        }

        #region Public API
        public bool IsOpen => isOpen;

        /// <summary>
        /// Puts the caret in the field. Ctrl+K (Cmd+K) and / call it from anywhere.
        /// </summary>
        public void Focus()
        {
            if (!isActiveAndEnabled)
                return;

            Utilities.SetSelectedObject(input.gameObject);
            input.ActivateInputField();
            Open();
        }

        /// <summary>
        /// Called by the field's clear button.
        /// </summary>
        public void Clear()
        {
            input.text = string.Empty;
            refocus = true;
        }

        public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            refocus = false;
            repeatKey = Key.None;
            input.SetTextWithoutNotify(string.Empty);
            input.DeactivateInputField();

            if (Utilities.GetSelectedObject() == input.gameObject)
                Utilities.SetSelectedObject(null);

            panelGroup.blocksRaycasts = false;
            AnimatePanel(false);
            AnimateField(closedWidth);
            SyncField();
        }

        // Called by result rows
        public void Hover(int row)
        {
            if (row != selected)
                Select(row, false);
        }

        public void Pick(int row)
        {
            if (row < 0 || row >= shown.Count)
                return;

            var page = shown[row];
            Close();
            OpenPage(index.categories[page.category], page.variant);
        }

        /// <summary>
        /// Opens a category by its tab ID on the variant with the given tab ID, the same way a result does. Home's links
        /// call it. Without that variant the category opens on the one it shows.
        /// </summary>
        public void OpenPage(string page, string variant)
        {
            BuildIndex();

            foreach (var category in index.categories)
            {
                if (pages.tabs[category.tabIndex].tabID != page)
                    continue;

                List<TabManager.Tab> tabs = category.tabs.tabs;
                for (int v = 0; v < tabs.Count; v++)
                {
                    if (tabs[v].tabID == variant)
                    {
                        OpenPage(category, v);
                        return;
                    }
                }

                break;
            }

            pages.OpenTab(page);
        }
        #endregion

        #region Field
        void OnFieldSelect(string _) => Open();

        void OnFieldDeselect(string _)
        {
            if (!isOpen)
                return;

            if (PointerPressedInside()) { refocus = true; }
            else { Close(); }
        }

        void OnValueChanged(string value)
        {
            // "/" opens the search; don't let the same key press land in the field
            if (value == "/" && Time.frameCount - slashFrame <= 2)
            {
                input.SetTextWithoutNotify(string.Empty);
                value = string.Empty;
            }

            if (!isOpen)
            {
                if (!string.IsNullOrEmpty(value)) Open();
                return;
            }

            Refresh();
        }

        void OnSubmit(string _)
        {
            if (isOpen && selected >= 0) { Pick(selected); }
            else { Close(); }
        }

        void Open()
        {
            if (isOpen)
                return;

            isOpen = true;
            BuildIndex();
            panel.gameObject.SetActive(true);
            panelGroup.blocksRaycasts = true;
            Refresh();
            AnimatePanel(true);
            AnimateField(panel.rect.width);
            SyncField();
        }

        void SyncField()
        {
            bool hasText = !string.IsNullOrEmpty(input.text);
            if (shortcutHint != null) { shortcutHint.SetActive(!isOpen && !hasText); }
            if (clearButton != null) { clearButton.SetActive(isOpen && hasText); }
        }

        bool ShortcutPressed(Keyboard keyboard)
        {
            if (keyboard.kKey.wasPressedThisFrame && (keyboard.ctrlKey.isPressed
                || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed))
                return true;

            // "/" only when no other field is taking text
            if (!keyboard.slashKey.wasPressedThisFrame || input.isFocused)
                return false;

            GameObject current = Utilities.GetSelectedObject();
            if (current != null && current.TryGetComponent(out TMP_InputField other) && other.isFocused)
                return false;

            slashFrame = Time.frameCount;
            return true;
        }

        bool Repeat(Keyboard keyboard, Key key)
        {
            var control = keyboard[key];
            if (control.wasPressedThisFrame)
            {
                repeatKey = key;
                repeatTime = Time.unscaledTime + RepeatDelay;
                return true;
            }

            if (repeatKey != key || !control.isPressed)
            {
                if (repeatKey == key) repeatKey = Key.None;
                return false;
            }

            if (Time.unscaledTime < repeatTime)
                return false;

            repeatTime = Time.unscaledTime + RepeatInterval;
            return true;
        }

        bool PointerPressedInside()
        {
            if (!Utilities.WasPointerPressed())
                return false;

            Vector2 pointer = Utilities.GetPointerPosition();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, pointer, eventCamera)
                || RectTransformUtility.RectangleContainsScreenPoint(field, pointer, eventCamera);
        }
        #endregion

        #region Results
        void Refresh()
        {
            SyncField();
            UpdateAccent();

            for (int i = 0; i < rows.Count; i++)
                rows[i].gameObject.SetActive(false);
            for (int i = 0; i < usedGroups; i++)
                groupPool[i].gameObject.SetActive(false);

            rows.Clear();
            shown.Clear();
            usedGroups = 0;

            // The query trimmed in place, like Trim() without the copy
            string value = input.text;
            int start = 0, end = value.Length;
            while (start < end && char.IsWhiteSpace(value[start]))
                start++;
            while (end > start && char.IsWhiteSpace(value[end - 1]))
                end--;

            if (start == end)
            {
                searchAllText ??= $"Search {index.pages.Count} pages";
                ShowMessage(searchAllText);
            }
            else
            {
                index.Search(value.AsSpan(start, end - start), results);
                categoryOrder.Clear();
                foreach (var result in results)
                {
                    if (!categoryOrder.Contains(result.page.category))
                        categoryOrder.Add(result.page.category);
                }

                // Grouped by category, each group where its best match ranks
                foreach (int category in categoryOrder)
                {
                    AddGroup(index.categories[category].icon, index.categories[category].name);
                    foreach (var result in results)
                    {
                        if (result.page.category != category)
                            continue;

                        MarkName(result);
                        Detail(result);
                        AddRow(result.page);
                    }
                }

                if (results.Count == 0)
                {
                    text.Clear().Append("Nothing matches '").Append(value, start, end - start).Append('\'');
                    messageTitle.SetText(text);
                    message.gameObject.SetActive(true);
                }
                else
                {
                    message.gameObject.SetActive(false);
                }
            }

            highlight.SetAsFirstSibling();

            // Twice: the example chips size themselves during the first pass, and the list only reads them on the second
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            float listHeight = Mathf.Min(LayoutUtility.GetPreferredHeight(list), maxListHeight);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, listHeight + footer.rect.height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;

            selected = -1;
            Select(rows.Count > 0 ? 0 : -1, false, false);
        }

        // The row takes the name and detail built in text and detail
        void AddRow(DemoSearchIndex.Page page)
        {
            DemoSearchResult row;
            if (rows.Count < rowPool.Count) { row = rowPool[rows.Count]; }
            else
            {
                row = Instantiate(resultPrefab, list);
                rowPool.Add(row);
            }

            row.Bind(this, rows.Count, text, detail);
            row.gameObject.SetActive(true);
            row.transform.SetAsLastSibling();
            rows.Add(row);
            shown.Add(page);
        }

        void AddGroup(Sprite icon, string label)
        {
            RectTransform group;
            if (usedGroups < groupPool.Count) { group = groupPool[usedGroups]; }
            else
            {
                group = Instantiate(groupPrefab, list);
                groupPool.Add(group);
                groupIcons.Add(group.Find("Icon").GetComponent<Image>());
                groupLabels.Add(group.Find("Label").GetComponent<TMP_Text>());
            }

            groupIcons[usedGroups].sprite = icon;
            groupLabels[usedGroups].text = label;
            usedGroups++;
            group.gameObject.SetActive(true);
            group.SetAsLastSibling();
        }

        // Only the title changes; the text under it stays as set in the scene
        void ShowMessage(string title)
        {
            messageTitle.text = title;
            message.gameObject.SetActive(true);
        }

        void Select(int row, bool reveal, bool animate = true)
        {
            selected = row;
            highlightTween.Kill();

            if (row < 0)
            {
                highlight.gameObject.SetActive(false);
                return;
            }

            var rect = rows[row].Rect;
            fromHighlightY = highlight.anchoredPosition.y;
            fromHighlightH = highlight.sizeDelta.y;
            toHighlightY = RowTop(rect);
            toHighlightH = rect.rect.height;

            bool wasHidden = !highlight.gameObject.activeSelf;
            highlight.gameObject.SetActive(true);
            if (!animate || wasHidden || slideDuration <= 0f) { ApplyHighlight(1f); }
            else
            {
                highlightTween = EvoTween.Create(0f, 1f, slideDuration).WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled).Bind(this, static (s, t) => s.ApplyHighlight(t), HighlightKey);
            }

            if (reveal)
                Reveal(rect);
        }

        void ApplyHighlight(float t)
        {
            highlight.anchoredPosition = new Vector2(highlight.anchoredPosition.x, Mathf.LerpUnclamped(fromHighlightY, toHighlightY, t));
            highlight.sizeDelta = new Vector2(highlight.sizeDelta.x, Mathf.LerpUnclamped(fromHighlightH, toHighlightH, t));
        }

        // Rows are laid out from the list's top edge, so their top is a negative offset from it
        static float RowTop(RectTransform row) => row.anchoredPosition.y + row.rect.height * (1f - row.pivot.y);

        void Reveal(RectTransform row)
        {
            const float margin = 10f;
            float viewHeight = scrollRect.viewport.rect.height;
            float overflow = list.rect.height - viewHeight;
            if (overflow <= 0f)
                return;

            float top = -RowTop(row);
            float bottom = top + row.rect.height;
            float scroll = list.anchoredPosition.y;
            if (top - margin < scroll) { scroll = top - margin; }
            else if (bottom + margin > scroll + viewHeight) { scroll = bottom + margin - viewHeight; }

            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(scroll / overflow);
        }
        #endregion

        #region Text
        // Match highlights use the theme the panel is drawn with, which the theme selector swaps.
        // The tag is only rebuilt when the color changes, so typing doesn't allocate it again.
        void UpdateAccent()
        {
            StylerPreset preset = Styler.GetDefaultPreset(false);
            if (preset == null || !preset.TryGetColor("Accent", out Color accent) || (hasAccent && accent.Equals(accentColor)))
                return;

            hasAccent = true;
            accentColor = accent;
            accentTag = "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">";
        }

        // Writes the name into text, with its hit letters in the accent
        void MarkName(in DemoSearchIndex.Result result)
        {
            string name = result.page.name;
            text.Clear();
            bool open = false;
            for (int i = 0; i < name.Length; i++)
            {
                bool hit = i < 64 && (result.nameMarks & (1UL << i)) != 0;
                if (hit && !open) { text.Append(accentTag); open = true; }
                else if (!hit && open) { text.Append("</color>"); open = false; }
                text.Append(name[i]);
            }

            if (open)
                text.Append("</color>");
        }

        // Writes the second line into detail: why a page matched when its name doesn't show it
        void Detail(in DemoSearchIndex.Result result)
        {
            detail.Clear();
            switch (result.reason)
            {
                case DemoSearchIndex.Reason.Option:
                    detail.Append("Option: ");
                    Mark(result.reasonText, 0, result.reasonText.Length, result.reasonStart, result.reasonLength);
                    break;
                case DemoSearchIndex.Reason.Alias:
                    detail.Append("Also called ").Append(result.reasonText);
                    break;
                case DemoSearchIndex.Reason.Description:
                    Snippet(result.reasonText, result.reasonStart, result.reasonLength);
                    break;
                default:
                    detail.Append(result.page.description);
                    break;
            }
        }

        // Appends value[from..to] with the hit in the accent; start and length are clamped to that piece
        void Mark(string value, int from, int to, int start, int length)
        {
            int count = to - from;
            start = Mathf.Clamp(start, 0, count);
            length = Mathf.Clamp(length, 0, count - start);
            detail.Append(value, from, start).Append(accentTag).Append(value, from + start, length).Append("</color>")
                .Append(value, from + start + length, count - start - length);
        }

        // A short piece of the description around the hit
        void Snippet(string value, int at, int length, int room = 44)
        {
            int from = Mathf.Max(0, at - (room - length) / 2);
            if (from > 0)
            {
                int space = value.LastIndexOf(' ', from);
                if (space >= 0 && at - space < room) from = space + 1;
            }

            int to = Mathf.Min(value.Length, from + room);
            if (to < value.Length)
            {
                int space = value.IndexOf(' ', to);
                if (space > 0 && space - from < room + 12) to = space;
            }

            // The piece without trailing punctuation, like TrimEnd('.', ',', ';')
            int end = to;
            while (end > from && (value[end - 1] == '.' || value[end - 1] == ',' || value[end - 1] == ';'))
                end--;

            if (from > 0)
                detail.Append('…');

            Mark(value, from, end, at - from, length);
            if (to < value.Length)
                detail.Append('…');
        }
        #endregion

        #region Pages
        // Reads every category tab's sidebar and pages. Home and other tabs without Variants are skipped.
        void BuildIndex()
        {
            if (indexed || pages == null)
                return;

            indexed = true;
            for (int t = 0; t < pages.tabs.Count; t++)
            {
                RectTransform tab = pages.tabs[t].tabObject;
                if (tab == null)
                    continue;

                Transform variantsRoot = tab.Find("Categories/Variants");
                Transform tabsRoot = tab.Find("Tabs");
                if (variantsRoot == null || tabsRoot == null || !variantsRoot.TryGetComponent(out RadioButtonGroup variants) || !tabsRoot.TryGetComponent(out TabManager tabs))
                    continue;

                Transform head = tab.Find("Categories/Category");
                Sprite icon = head != null && head.Find("Icon") is Transform iconRoot && iconRoot.TryGetComponent(out Image image) ? image.sprite : null;
                int category = index.categories.Count;
                index.AddCategory(ReadText(head, "Name") ?? tab.name, icon, t, variants, tabs);

                // Button order is the order the group's events use, the same one the sidebar clicks with
                Transform buttons = variants.targetParent != null ? variants.targetParent : variants.transform;
                for (int v = 0; v < buttons.childCount; v++)
                {
                    Transform button = buttons.GetChild(v);
                    if (!button.TryGetComponent(out Button _))
                        continue;

                    string name = ReadText(button, "Text") ?? button.name;
                    Transform page = v < tabs.tabs.Count ? tabs.tabs[v].tabObject : null;
                    index.AddPage(category, v, name, ReadText(page, "Content/Header/Description"), FindKeywords(name), ReadOptions(page));
                }
            }
        }

        static string ReadText(Transform root, string path)
        {
            Transform target = root != null ? root.Find(path) : null;
            return target != null && target.TryGetComponent(out TMP_Text label) ? label.text : null;
        }

        // Labels of the Customize options: tile and slider names, segment choices. Section headers and values are left out.
        static string[] ReadOptions(Transform page)
        {
            Transform options = page != null ? page.Find("Customize/Content") : null;
            if (options == null)
                return Array.Empty<string>();

            var labels = new List<string>();
            foreach (Transform section in options)
            {
                Transform header = section.Find("Header");
                foreach (var label in section.GetComponentsInChildren<TMP_Text>(true))
                {
                    string value = label.text;
                    if (label.transform != header && IsLabel(value) && !labels.Contains(value))
                        labels.Add(value);
                }
            }

            return labels.ToArray();
        }

        static bool IsLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 24)
                return false;

            bool letter = false;
            foreach (char c in value)
            {
                if (char.IsDigit(c)) return false;
                letter |= char.IsLetter(c);
            }

            return letter;
        }

        string[] FindKeywords(string page)
        {
            foreach (var entry in keywords)
            {
                if (entry.page != page || string.IsNullOrWhiteSpace(entry.words))
                    continue;

                string[] words = entry.words.Split(',');
                for (int i = 0; i < words.Length; i++)
                    words[i] = words[i].Trim();
                return words;
            }

            return null;
        }

        // Picks the variant first, so a category that opens already shows it
        void OpenPage(DemoSearchIndex.Category category, int variant)
        {
            RadioButtonGroup variants = category.variants;

            if (pages.CurrentTabIndex == category.tabIndex)
            {
                if (variants.selectedIndex != variant)
                    variants.SetButton(variant);
                return;
            }

            if (variants.GetButton(variant) != null) { variants.SetButton(variant, false, true); }
            else
            {
                // Never opened yet: both pick these up when they initialize
                variants.selectedIndex = variant;
                category.tabs.OpenTab(variant, false);
            }

            pages.OpenTab(category.tabIndex);
        }
        #endregion

        #region Motion
        void SetFieldWidth(float width)
        {
            field.sizeDelta = new Vector2(width, field.sizeDelta.y);
            LayoutRebuilder.MarkLayoutForRebuild(field);
        }

        void AnimateField(float width)
        {
            widthTween.Kill();
            if (slideDuration <= 0f || !isActiveAndEnabled) { SetFieldWidth(width); }
            else
            {
                widthTween = EvoTween.Create(field.sizeDelta.x, width, slideDuration).WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled).Bind(this, static (s, width) => s.SetFieldWidth(width), WidthKey);
            }
        }

        void AnimatePanel(bool show)
        {
            panelTween.Kill();
            panelFrom = panelGroup.alpha;
            panelTo = show ? 1f : 0f;
            float length = show ? Mathf.Max(slideDuration, fadeDuration) : fadeDuration;
            if (length <= 0f || !isActiveAndEnabled)
            {
                ApplyPanel(length);
                if (!show) panel.gameObject.SetActive(false);
                return;
            }

            // Runs in seconds, so the fade and the slide each keep their own duration and ease
            panelTween = EvoTween.Create(0f, length, length).WithEase(Ease.Linear).WithTimeMode(TimeMode.Unscaled)
                .OnComplete(hideClosedPanel)
                .Bind(this, static (s, time) => s.ApplyPanel(time), PanelKey);
        }

        // Fades in while settling down from OpenOffset, and fades out where it is
        void ApplyPanel(float time)
        {
            float fade = Progress(time, fadeDuration);
            panelGroup.alpha = Mathf.LerpUnclamped(panelFrom, panelTo, TweenEase.Evaluate(Ease.InOutSine, fade));
            float rise = isOpen
                ? openOffset * (1f - TweenEase.Evaluate(Ease.OutCubic, Progress(time, slideDuration)))
                : -openOffset * (0f - TweenEase.Evaluate(Ease.OutSine, Progress(time, slideDuration)));
            panel.anchoredPosition = panelPosition + new Vector2(0f, rise);
        }

        static float Progress(float time, float duration) => duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
        #endregion
    }
}