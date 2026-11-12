using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evo.EditorTools
{
    public static partial class EvoEditorUITK
    {
        /// <summary>
        /// Category separator in a grid, like the UI Catalog's: an optional icon, a title, and a line to the edge.
        /// Add evo-section--link to make it look clickable.
        /// </summary>
        public class Section : VisualElement
        {
            readonly Label title;

            public Section(string text = null, Texture icon = null)
            {
                AddToClassList("evo-section");

                if (icon != null)
                {
                    var image = new Image { image = icon, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("evo-section__icon");
                    Add(image);
                }

                title = new Label(text ?? "") { pickingMode = PickingMode.Ignore };
                title.AddToClassList("evo-section__title");
                Add(title);

                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("evo-section__line");
                Add(line);
            }

            public string Text
            {
                get => title.text;
                set => title.text = value;
            }
        }

        /// <summary>
        /// Centered title, message, and optional action, shown when a window has nothing to list.
        /// </summary>
        public class EmptyState : VisualElement
        {
            readonly Label title;
            readonly Label message;
            readonly Button button;
            Action action;

            public EmptyState()
            {
                AddToClassList("evo-empty");

                title = new Label();
                title.AddToClassList("evo-empty__title");
                Add(title);

                message = new Label();
                message.AddToClassList("evo-empty__message");
                Add(message);

                button = CreateButton(null, null, () => action?.Invoke());
                button.AddToClassList("evo-empty__button");
                Add(button);
            }

            public EmptyState Set(string titleText, string messageText, string actionText = null, Action onAction = null)
            {
                title.text = titleText;
                message.text = messageText;
                button.text = actionText ?? "";
                button.style.display = onAction != null ? DisplayStyle.Flex : DisplayStyle.None;
                action = onAction;
                return this;
            }
        }

        /// <summary>
        /// Thin rounded track whose accent fill shows how far something has got, green once it's done and red if it failed.
        /// </summary>
        public class ProgressTrack : VisualElement
        {
            readonly VisualElement fill;

            public ProgressTrack()
            {
                AddToClassList("evo-progress");

                fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("evo-progress__fill");
                Add(fill);
            }

            /// <summary>
            /// Fills the track to a fraction between 0 and 1.
            /// </summary>
            public void SetProgress(float value, bool done = false, bool failed = false)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100);
                EnableInClassList("evo-progress--done", done && !failed);
                EnableInClassList("evo-progress--failed", failed);
            }
        }

        /// <summary>
        /// Progress track with a message. The track turns green or red once an operation finishes,
        /// and a finished message can be dismissed. It stays hidden until there's something to show.
        /// </summary>
        public class StatusView : VisualElement
        {
            readonly Label label;
            readonly ProgressTrack track;
            readonly Button dismiss;

            public StatusView(Action onDismiss)
            {
                AddToClassList("evo-status");
                style.display = DisplayStyle.None;

                var row = new VisualElement();
                row.AddToClassList("evo-status__row");
                Add(row);

                label = new Label();
                label.AddToClassList("evo-status__label");
                row.Add(label);

                dismiss = new Button(onDismiss) { text = "×", tooltip = "Dismiss" };
                dismiss.AddToClassList("evo-icon-button");
                dismiss.AddToClassList("evo-status__dismiss");
                row.Add(dismiss);

                track = new ProgressTrack();
                track.AddToClassList("evo-status__track");
                Add(track);
            }

            /// <summary>
            /// Shows the progress and message. A finished operation without a message hides the view.
            /// </summary>
            public void SetState(float progress, string text, bool running, bool failed)
            {
                style.display = running || !string.IsNullOrEmpty(text) ? DisplayStyle.Flex : DisplayStyle.None;
                label.text = text ?? "";
                track.SetProgress(progress, !running, !running && failed);
                dismiss.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
                EnableInClassList("evo-status--running", running);
            }

            public void Hide() => style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Soft shadow along the top of a scroll view that appears once content scrolls under the header.
        /// Add it after the scroll view in their shared parent, so it draws on top.
        /// </summary>
        public class ScrollShadow : VisualElement
        {
            public ScrollShadow(ScrollView scrollView)
            {
                AddToClassList("evo-scroll-shadow");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += DrawGradient;
                scrollView.verticalScroller.valueChanged += value => EnableInClassList("evo-scroll-shadow--visible", value > 1f);
            }

            static void DrawGradient(MeshGenerationContext context)
            {
                Rect rect = context.visualElement.contentRect;
                if (rect.width <= 0 || rect.height <= 0)
                    return;

                // A vertex-colored quad, so there's no texture to create or clean up
                Color32 top = new Color(0, 0, 0, EditorGUIUtility.isProSkin ? 0.3f : 0.12f);
                Color32 bottom = new Color(0, 0, 0, 0);
                MeshWriteData mesh = context.Allocate(4, 6);
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = bottom });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = bottom });
                mesh.SetNextIndex(0);
                mesh.SetNextIndex(1);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(0);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(3);
            }
        }

        /// <summary>
        /// Calls back when an asset's serialized values change, such as edits in the Inspector, without polling.
        /// Add it anywhere in the window, and call Stop from OnDisable.
        /// </summary>
        public class AssetTracker : VisualElement
        {
            SerializedObject serializedObject;
            VisualElement binding;

            public AssetTracker()
            {
                style.display = DisplayStyle.None;
                pickingMode = PickingMode.Ignore;
            }

            /// <summary>
            /// The asset being tracked, or null.
            /// </summary>
            public Object Target => serializedObject?.targetObject;

            /// <summary>
            /// Tracks the asset instead of the previous one. Passing null only stops tracking.
            /// </summary>
            public void Track(Object asset, Action onChanged)
            {
                Stop();

                if (asset == null)
                    return;

                // A tracked binding can't be removed, so each asset gets a fresh element to hold it
                serializedObject = new SerializedObject(asset);
                binding = new VisualElement();
                Add(binding);
                binding.TrackSerializedObjectValue(serializedObject, _ => onChanged());
            }

            public void Stop()
            {
                // Detach the binding before disposing what it watches
                binding?.RemoveFromHierarchy();
                binding = null;
                serializedObject?.Dispose();
                serializedObject = null;
            }
        }
    }
}