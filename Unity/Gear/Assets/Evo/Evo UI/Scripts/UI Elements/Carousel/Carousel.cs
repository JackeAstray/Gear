using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, ExecuteAlways]
    [AddComponentMenu("Evo/UI/UI Elements/Carousel"), HelpURL(Constants.HelpUrl + "ui-elements/carousel")]
    public class Carousel : MonoBehaviour
    {
        // Content
        [SerializeField] private int currentIndex;
        public List<Item> items = new();

        // Settings
        public bool useUnscaledTime = false;
        public bool autoSlide = true;
        [Range(0.5f, 30f)] public float autoSlideTimer = 3f;
        public Ease animationEase = Ease.OutQuart;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0.1f, 2f)] public float animationDuration = 0.4f;
        [Range(0.1f, 1f), Tooltip("Scale applied to non-selected indicators.")]
        public float indicatorShrink = 0.5f;
        [Tooltip("Axis items move along. Later items come from the right or the bottom.")]
        public Orientation orientation = Orientation.Horizontal;

        // Neighbor Items
        [Tooltip("Shows neighboring items and scales the visible cards to fit the item parent.")]
        public bool showNeighbors = false;
        [Range(0f, 1f)] public float neighborOpacity = 0.5f;
        [Min(0), Tooltip("Maximum neighbors on each side. Items are never duplicated.")]
        public int maxVisibleNeighbors = 1;
        [Min(0f)] public float itemSpacing = 12f;

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        public Transform itemParent;
        public GameObject itemPreset;
        public Transform indicatorParent;
        public GameObject indicatorPreset;

        // Serialized Ownership
        [SerializeField, HideInInspector] List<CarouselPreset> itemObjects = new();
        [SerializeField, HideInInspector] List<CarouselIndicator> indicatorObjects = new();
        [SerializeField, HideInInspector] GameObject generatedItemPreset;
        [SerializeField, HideInInspector] GameObject generatedIndicatorPreset;

        // Cache
        readonly GeneratedNames itemNames = new();
        readonly GeneratedNames indicatorNames = new();
        TweenHandle contentTween;
        Action<float> animatePosition;
        Action completeTransition;
        Vector2 parentSize;
        Vector2 layoutStep;

        // State
        bool isInitialized;
        float position;
        float targetPosition;
        float timer;

        // Properties
        public int CurrentIndex => currentIndex;
        public bool IsTransitioning => contentTween.IsActive;
        TimeMode AnimationTimeMode => useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled;
        int NeighborCount => Mathf.Clamp(maxVisibleNeighbors, 0, itemObjects.Count / 2);

        public enum Orientation { Horizontal, Vertical }

        [Serializable]
        public class Item
        {
            public string title = "Item title";
            [TextArea(2, 4)] public string description = "Item description";
            public Sprite background;
            public UnityEvent onClick = new();

#if EVO_LOCALIZATION
            public string titleKey;
            public string descriptionKey;
#endif
        }

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
                return;

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization, refreshImmediately: false);
#endif
            Refresh();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
            refreshPending = false;
#endif
            StopAnimation();
            UnbindObjects();
            isInitialized = false;

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
        }

        void OnDestroy()
        {
            OnDisable();

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.isLoaded)
                return;
#endif
            ClearObjects(itemObjects);
            ClearObjects(indicatorObjects);
        }

        void Update()
        {
            if (!isInitialized)
                return;

            if (itemParent is RectTransform rect && parentSize != rect.rect.size)
                UpdateLayout();

            if (IsTransitioning || currentIndex < 0 || itemObjects.Count <= 1)
                return;

            if (autoSlide)
            {
                timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (timer >= Mathf.Max(0.01f, autoSlideTimer))
                {
                    NextItem();
                    return;
                }
            }

            if (currentIndex < indicatorObjects.Count && indicatorObjects[currentIndex] != null)
                indicatorObjects[currentIndex].SetProgress(autoSlide ? timer / Mathf.Max(0.01f, autoSlideTimer) : 0f);
        }

        #region Setup
        public void Initialize() => Refresh();

        /// <summary>
        /// Reuses generated objects and refreshes the editor preview or runtime content.
        /// Call after changing items or references from script.
        /// </summary>
        public void Refresh()
        {
#if UNITY_EDITOR
            if (refreshPending)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
                refreshPending = false;
            }

            if (UnityEditor.EditorUtility.IsPersistent(this) || (Application.isPlaying && !Application.IsPlaying(gameObject)))
                return;
#endif
            StopAnimation();
            bool runtime = Application.IsPlaying(gameObject);
            isInitialized = false;
            bool changed = items == null || itemObjects == null || indicatorObjects == null;
            items ??= new List<Item>();
            itemObjects ??= new List<CarouselPreset>();
            indicatorObjects ??= new List<CarouselIndicator>();

            changed |= RefreshObjects(itemObjects, itemParent, itemPreset, ref generatedItemPreset, itemNames);
            changed |= RefreshObjects(indicatorObjects, indicatorParent, indicatorPreset, ref generatedIndicatorPreset, indicatorNames);

            int index = items.Count > 0 && itemObjects.Count > 0 ? Mathf.Clamp(currentIndex, 0, items.Count - 1) : -1;
            changed |= currentIndex != index;
            currentIndex = index;
            position = targetPosition = currentIndex;
            timer = 0f;

            for (int i = 0; i < itemObjects.Count; i++)
            {
                changed |= itemObjects[i].RefreshContent(items[i], runtime && isActiveAndEnabled);

                if (runtime)
                    itemObjects[i].FadeContent(showNeighbors || i == currentIndex, 0f, AnimationTimeMode);
            }

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                CarouselIndicator indicator = indicatorObjects[i];

                if (runtime && isActiveAndEnabled)
                    indicator.Bind(this, i);
                else
                    indicator.Unbind();

                changed |= indicator.ResetProgress();
                changed |= indicator.SetSelected(i == currentIndex, indicatorShrink, AnimationTimeMode, false);
           
                if (!indicator.gameObject.activeSelf)
                {
                    indicator.gameObject.SetActive(true);
                    changed = true;
                }
            }

            changed |= UpdateLayout();
            isInitialized = runtime && isActiveAndEnabled && currentIndex >= 0;
            if (isInitialized)
            {
                animatePosition ??= AnimatePosition;
                completeTransition ??= CompleteTransition;
#if EVO_LOCALIZATION
                UpdateLocalization();
#endif
            }

            if (changed && indicatorParent is RectTransform indicatorRect)
                LayoutRebuilder.MarkLayoutForRebuild(indicatorRect);

#if UNITY_EDITOR
            if (!runtime && changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);

                for (int i = 0; i < itemObjects.Count; i++)
                    itemObjects[i].RecordPrefabChanges();

                for (int i = 0; i < indicatorObjects.Count; i++)
                    indicatorObjects[i].RecordPrefabChanges();

                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        bool RefreshObjects<T>(List<T> objects, Transform parent, GameObject preset, ref GameObject generatedPreset, GeneratedNames names) where T : Component
        {
            bool valid = parent != null && preset != null && preset.TryGetComponent<T>(out _);
            int count = valid ? items.Count : 0;
            string linkPath = valid ? GeneratedObjects.GetLinkPath(this, preset) : null;
            bool changed = false;

            // Adopt legacy preview objects once, leaving unrelated children alone
            if (valid && generatedPreset == null && objects.Count == 0)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (child.gameObject != preset && child.TryGetComponent<T>(out var component))
                        objects.Add(component);
                }
                changed |= objects.Count > 0;
            }

            bool presetChanged = generatedPreset != null && generatedPreset != preset;
            changed |= generatedPreset != preset || objects.Count != count;
            generatedPreset = preset;

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                T instance = objects[i];
                if (presetChanged || i >= count || instance == null || GeneratedObjects.NeedsRelink(instance, preset, linkPath))
                {
                    DestroyGeneratedObject(instance);
                    objects[i] = null;
                    changed = true;
                }
            }

            if (objects.Count > count)
                objects.RemoveRange(count, objects.Count - count);

            while (objects.Count < count)
                objects.Add(null);

            if (linkPath != null)
                changed |= GeneratedObjects.ClaimOrphans(objects, count, parent, preset.GetComponent<T>(), linkPath, DestroyGeneratedObject);

            for (int i = 0; i < count; i++)
            {
                T instance = objects[i];
                if (instance == null)
                {
                    objects[i] = instance = GeneratedObjects.Create(preset, parent, linkPath).GetComponent<T>();
                    changed = true;
                }
                else if (instance.transform.parent != parent)
                {
                    instance.transform.SetParent(parent, false);
                    changed = true;
                }

                changed |= names.Apply(i, instance, items[i]?.title ?? string.Empty);
                if (instance.transform.GetSiblingIndex() != i)
                {
                    instance.transform.SetSiblingIndex(i);
                    changed = true;
                }
            }

            return changed;
        }

        static void DestroyGeneratedObject(Component instance)
        {
            if (instance == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(instance.gameObject); }
            else
            {
                instance.gameObject.SetActive(false);
                Destroy(instance.gameObject);
            }
        }

        static void ClearObjects<T>(List<T> objects) where T : Component
        {
            if (objects == null)
                return;

            for (int i = objects.Count - 1; i >= 0; i--)
                DestroyGeneratedObject(objects[i]);
            
            objects.Clear();
        }

        void UnbindObjects()
        {
            for (int i = 0; i < itemObjects.Count; i++)
            {
                if (itemObjects[i] != null)
                    itemObjects[i].Unbind();
            }

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                if (indicatorObjects[i] != null)
                    indicatorObjects[i].Unbind();
            }
        }
        #endregion

        #region Layout
        bool UpdateLayout()
        {
            int count = itemObjects.Count;
            RectTransform preset = itemPreset != null ? itemPreset.transform as RectTransform : null;
            if (count == 0 || itemParent == null || preset == null)
                return false;

            RectTransform parentRect = itemParent as RectTransform;
            parentSize = parentRect != null ? parentRect.rect.size : Vector2.zero;
            Vector3 scale = preset.localScale;
            Vector2 size = preset.sizeDelta;

            // A single item never moves, so only neighbors need a step. They keep their size across the axis
            // and only get shorter along it when the visible ones don't fit the item parent.
            if (showNeighbors)
            {
                int axis = (int)orientation;
                int visibleCount = NeighborCount * 2 + 1;
                float spacing = Mathf.Max(0f, itemSpacing);
                float itemScale = Mathf.Max(0.0001f, Mathf.Abs(scale[axis]));
                float length = ((preset.anchorMax[axis] - preset.anchorMin[axis]) * parentSize[axis] + size[axis]) * itemScale;
                float fit = Mathf.Max(0f, (parentSize[axis] - (visibleCount - 1) * spacing) / visibleCount);

                if (parentRect != null && length > fit)
                {
                    size[axis] -= (length - fit) / itemScale;
                    length = fit;
                }

                layoutStep = (axis == 0 ? Vector2.right : Vector2.down) * (length + spacing);
            }

            bool changed = false;
            for (int i = 0; i < count; i++)
                changed |= itemObjects[i].PrepareLayout(scale, size, parentSize);

            return ApplyLayout() || changed;
        }

        bool ApplyLayout()
        {
            int count = itemObjects.Count;
            if (!showNeighbors)
                return ApplySingleLayout(count);

            int neighbors = NeighborCount;
            bool changed = false;
            float left = (count - 1) / 2;

            for (int i = 0; i < count; i++)
            {
                CarouselPreset item = itemObjects[i];
                float offset = Mathf.Repeat(i - position + left + 0.5f, count) - left - 0.5f;
                float distance = Mathf.Abs(offset);
                float alpha = Mathf.Lerp(1f, neighbors > 0 ? Mathf.Clamp01(neighborOpacity) : 0f, Mathf.Clamp01(distance));

                if (neighbors > 0)
                    alpha *= Mathf.Clamp01(neighbors + 1f - distance);

                // Fade the farthest item before wrapping it to the other edge
                if (count > 1)
                    alpha *= Mathf.Clamp01(Mathf.Min(offset + left + 0.5f, count - left - 0.5f - offset) * 2f);

                changed |= item.SetLayout(layoutStep * offset, alpha, i == currentIndex && !IsTransitioning);
                item.SetReveal(1f, 0, false, false);
            }

            return changed;
        }

        // Items stay in place and share the item parent: the later one grows in from the right or the bottom
        // while the earlier one shrinks away from it, so nothing leaves the item parent and no mask is needed
        bool ApplySingleLayout(int count)
        {
            float step = Mathf.Floor(position);
            float reveal = position - step;
            int below = (int)Mathf.Repeat(step, count);
            int above = reveal > 0f ? (below + 1) % count : -1;
            int axis = (int)orientation;
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                CarouselPreset item = itemObjects[i];
                changed |= item.SetLayout(Vector2.zero, i == below || i == above ? 1f : 0f, i == currentIndex && !IsTransitioning);

                // Right is the max edge horizontally, bottom is the min edge vertically
                if (i == above)
                    item.SetReveal(reveal, axis, axis == 0, true);
                else
                    item.SetReveal(i == below ? 1f - reveal : 1f, axis, axis != 0, false);
            }

            // The later item is drawn above the one that reaches under it
            if (above >= 0)
            {
                Transform cover = itemObjects[above].transform;
                int index = itemObjects[below].transform.GetSiblingIndex();

                if (cover.GetSiblingIndex() < index)
                    cover.SetSiblingIndex(index);
            }

            return changed;
        }
        #endregion

        #region Animation
        void AnimatePosition(float value)
        {
            position = value;
            ApplyLayout();
        }

        void CompleteTransition()
        {
            contentTween = default;
            position = targetPosition = currentIndex;
            timer = 0f;
            ApplyLayout();
        }

        void StopAnimation()
        {
            contentTween.Kill();
            contentTween = default;
        }

        void SelectItem(int index, int distance)
        {
            if (!isInitialized || index < 0 || index >= itemObjects.Count || index == currentIndex)
                return;

            StopAnimation();
            int previousIndex = currentIndex;
            currentIndex = index;
            targetPosition += distance;
            timer = 0f;

            if (previousIndex >= 0 && previousIndex < indicatorObjects.Count && indicatorObjects[previousIndex] != null)
                indicatorObjects[previousIndex].SetSelected(false, indicatorShrink, AnimationTimeMode, true);
           
            if (currentIndex < indicatorObjects.Count && indicatorObjects[currentIndex] != null)
            {
                indicatorObjects[currentIndex].SetProgress(0f);
                indicatorObjects[currentIndex].SetSelected(true, indicatorShrink, AnimationTimeMode, true);
            }

            // Content fades on its own timing, so text doesn't follow the background's ease
            float duration = Mathf.Max(0.01f, animationDuration);
            if (!showNeighbors)
            {
                for (int i = 0; i < itemObjects.Count; i++)
                    itemObjects[i].FadeContent(i == currentIndex, duration, AnimationTimeMode);
            }

            contentTween = EvoTween.Create(position, targetPosition, duration)
                .WithEase(animationEase, animationCurve)
                .WithTimeMode(AnimationTimeMode)
                .OnComplete(completeTransition)
                .Bind(animatePosition);

            ApplyLayout();
        }
        #endregion

        #region Public API
        public void NextItem()
        {
            if (itemObjects.Count > 1)
                SelectItem((currentIndex + 1) % itemObjects.Count, 1);
        }

        public void PreviousItem()
        {
            if (itemObjects.Count > 1)
                SelectItem((currentIndex - 1 + itemObjects.Count) % itemObjects.Count, -1);
        }

        public void GoToItem(int index)
        {
            int distance = index - currentIndex;
            int count = itemObjects.Count;
            if (distance > count / 2)
                distance -= count;
            else if (distance < -count / 2)
                distance += count;

            SelectItem(index, distance);
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || items == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(item.titleKey))
                    item.title = localizedObject.GetString(item.titleKey) ?? item.title;
                if (!string.IsNullOrEmpty(item.descriptionKey))
                    item.description = localizedObject.GetString(item.descriptionKey) ?? item.description;
                if (i < itemObjects.Count && itemObjects[i] != null)
                    itemObjects[i].SetContent(item);
            }
        }
#endif

#if UNITY_EDITOR
        bool refreshPending;

        void OnRectTransformDimensionsChange()
        {
            if (!Application.IsPlaying(gameObject))
                OnValidate();
        }

        void OnValidate()
        {
            GeneratedObjects.Watch(this, OnValidate);
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        void RefreshDelayed()
        {
            refreshPending = false;

            if (this == null || UnityEditor.EditorApplication.isCompiling
                || (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying))
                return;

            Refresh();
        }
#endif
    }
}