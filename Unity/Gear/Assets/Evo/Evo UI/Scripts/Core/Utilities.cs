using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Evo.UI
{
    [NoAutoStaticsCleanup]
    public class Utilities
    {
        #region Animation
        public static Vector4 PremultiplyColor(Color color)
        {
            return new Vector4(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
        }

        public static Color UnpremultiplyColor(Vector4 color)
        {
            float alpha = color.w;
            return alpha > 0f ? new Color(color.x / alpha, color.y / alpha, color.z / alpha, alpha) : Color.clear;
        }

        public static float EaseOutQuadratic(float value)
        {
            float t = Mathf.Clamp01(value);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// Smoothly cross fade graphic color to target color.
        /// </summary>
        public static IEnumerator CrossFadeGraphic(Graphic graphic, Color targetColor, float duration,
            bool unscaledTime = true, bool destroyOnCull = false)
        {
            if (graphic == null)
                yield break;

            if (duration <= 0f)
            {
                graphic.color = targetColor;
                yield break;
            }

            Color startColor = graphic.color;
            Vector4 startP = PremultiplyColor(startColor);
            Vector4 targetP = PremultiplyColor(targetColor);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = EaseOutQuadratic(elapsed / duration);
                Vector4 lerped = Vector4.Lerp(startP, targetP, t);
                graphic.color = UnpremultiplyColor(lerped);
                yield return null;
            }

            graphic.color = targetColor;
            if (destroyOnCull && graphic.color.a == 0)
                GameObject.Destroy(graphic.gameObject);
        }

        /// <summary>
        /// Smoothly cross fade graphic color to target color.
        /// </summary>
        public static IEnumerator CrossFadeAlpha(Graphic graphic, float targetAlpha, float duration,
            bool unscaledTime = true, bool destroyOnCull = false)
        {
            if (graphic == null || duration <= 0f)
            {
                if (graphic != null)
                    graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, targetAlpha);

                yield break;
            }

            float startAlpha = graphic.color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing
                float alphaOut = Mathf.Lerp(startAlpha, targetAlpha, t);
                graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, alphaOut);
                yield return null;
            }

            graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, targetAlpha);
            if (destroyOnCull && graphic.color.a == 0)
                GameObject.Destroy(graphic.gameObject);
        }

        /// <summary>
        /// Animate a single CanvasGroup to a target alpha value.
        /// </summary>
        public static IEnumerator CrossFadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration, bool unscaledTime = true)
        {
            if (canvasGroup == null || duration <= 0f)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = targetAlpha;

                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        /// <summary>
        /// Animate multiple CanvasGroups to target alpha values.
        /// </summary>
        public static IEnumerator CrossFadeCanvasGroup(Dictionary<CanvasGroup, float> targets, float duration, bool unscaledTime = true)
        {
            if (targets == null || targets.Count == 0)
                yield break;

            int count = targets.Count;
            CanvasGroup[] groups = new CanvasGroup[count];
            float[] startAlphas = new float[count];
            float[] targetAlphas = new float[count];
            int validCount = 0;

            foreach (var kvp in targets)
            {
                if (kvp.Key != null)
                {
                    groups[validCount] = kvp.Key;
                    startAlphas[validCount] = kvp.Key.alpha;
                    targetAlphas[validCount] = kvp.Value;
                    validCount++;
                }
            }

            // Early exit if no valid targets
            if (validCount == 0)
                yield break;

            // Instant transition if duration is zero or negative
            if (duration <= 0f)
            {
                for (int i = 0; i < validCount; i++)
                    groups[i].alpha = targetAlphas[i];

                yield break;
            }

            // Animate
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing

                for (int i = 0; i < validCount; i++)
                    groups[i].alpha = Mathf.Lerp(startAlphas[i], targetAlphas[i], t);

                yield return null;
            }

            for (int i = 0; i < validCount; i++)
                groups[i].alpha = targetAlphas[i];
        }

        /// <summary>
        /// Smoothly animates a RectTransform's anchoredPosition and a CanvasGroup's alpha simultaneously.
        /// </summary>
        public static IEnumerator SlideAndFade(RectTransform targetRect, CanvasGroup canvasGroup, Vector2 startPos, Vector2 targetPos,
            float startAlpha, float targetAlpha, float duration, AnimationCurve curve = null, bool unscaledTime = true)
        {
            if (targetRect == null && canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                if (targetRect != null)
                    targetRect.anchoredPosition = targetPos;
                if (canvasGroup != null)
                    canvasGroup.alpha = targetAlpha;

                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = curve != null ? curve.Evaluate(t) : (1f - Mathf.Pow(1f - t, 2f));

                if (targetRect != null)
                    targetRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveT);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);

                yield return null;
            }

            if (targetRect != null)
                targetRect.anchoredPosition = targetPos;
            if (canvasGroup != null)
                canvasGroup.alpha = targetAlpha;
        }

        /// <summary>
        /// Smoothly animates a RectTransform's localScale and a CanvasGroup's alpha simultaneously.
        /// </summary>
        public static IEnumerator ScaleAndFade(RectTransform targetRect, CanvasGroup canvasGroup, Vector3 startScale, Vector3 targetScale,
            float startAlpha, float targetAlpha, float duration, AnimationCurve curve = null, bool unscaledTime = true)
        {
            if (targetRect == null && canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                if (targetRect != null)
                    targetRect.localScale = targetScale;
                if (canvasGroup != null)
                    canvasGroup.alpha = targetAlpha;

                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = curve != null ? curve.Evaluate(t) : (1f - Mathf.Pow(1f - t, 2f));
              
                if (targetRect != null)
                    targetRect.localScale = Vector3.Lerp(startScale, targetScale, curveT);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);

                yield return null;
            }

            if (targetRect != null)
                targetRect.localScale = targetScale;
            if (canvasGroup != null)
                canvasGroup.alpha = targetAlpha;
        }
        #endregion

        #region Input
        /// <summary>
        /// Returns the current mouse position based on the active input handler.
        /// </summary>
        public static Vector2 GetPointerPosition() => Pointer.current?.position.value ?? Vector2.zero;

        /// <summary>
        /// Checks if the current mouse was pressed based on the active input handler.
        /// </summary>
        public static bool WasPointerPressed()
        {
            return (Pointer.current?.press.wasPressedThisFrame ?? false) || (Mouse.current?.rightButton.wasPressedThisFrame ?? false);
        }

        /// <summary>
        /// Checks if the current mouse was released based on the active input handler.
        /// </summary>
        public static bool WasPointerReleased()
        {
            return (Pointer.current?.press.wasReleasedThisFrame ?? false) || (Mouse.current?.rightButton.wasReleasedThisFrame ?? false);
        }

        /// <summary>
        /// Checks if the specified mouse key was pressed based on the active input handler.
        /// </summary>
        public static bool WasMouseKeyPressed(int targetButton)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            return targetButton switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => false
            };
        }

        /// <summary>
        /// Checks if the ESC key was pressed based on the active input handler.
        /// </summary>
        public static bool WasEscapeKeyPressed() => Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;

        /// <summary>
        /// Checks if the ESC key was pressed based on the active input handler.
        /// </summary>
        public static bool WasEnterKeyPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        /// <summary>
        /// Checks if the Shift key was pressed based on the active input handler.
        /// </summary>
        public static bool WasShiftKeyPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return keyboard.shiftKey.isPressed;
        }

        /// <summary>
        /// Checks if the submit button is pressed.
        /// </summary>
        public static bool WasSubmitPressed()
        {
            if (EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule inputModule)
            {
                if (inputModule.submit != null && inputModule.submit.action != null)
                    return inputModule.submit.action.IsPressed();
            }
            return false;
        }

        /// <summary>
        /// Checks for tab navigation combos.
        /// </summary>
        public static bool HandleTabNavigation(out bool reverseTab)
        {
            bool tabPressed = Keyboard.current?.tabKey.wasPressedThisFrame ?? false;
            bool shiftHeld = (Keyboard.current?.leftShiftKey.isPressed ?? false) || (Keyboard.current?.rightShiftKey.isPressed ?? false);
            reverseTab = tabPressed && shiftHeld;
            return tabPressed;
        }
        #endregion

        #region Navigation
        /// <summary>
        /// Helper method to select target object as selected event system object.
        /// </summary>
        public static GameObject GetSelectedObject()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return null;

            return eventSystem.currentSelectedGameObject;
        }

        /// <summary>
        /// Helper method to select target object as selected event system object.
        /// </summary>
        public static void SetSelectedObject(GameObject targetObject)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting)
                eventSystem.SetSelectedGameObject(targetObject);
        }
        #endregion

        #region Text
        public static string GetNumberFormatString(NumberDisplayFormat displayFormat, string fallback = "F0")
        {
            return displayFormat switch
            {
                NumberDisplayFormat.Fixed0 => "F0",
                NumberDisplayFormat.Fixed1 => "F1",
                NumberDisplayFormat.Fixed2 => "F2",
                NumberDisplayFormat.Fixed3 => "F3",
                NumberDisplayFormat.Fixed4 => "F4",
                NumberDisplayFormat.Fixed5 => "F5",
                NumberDisplayFormat.Number0 => "N0",
                NumberDisplayFormat.Number1 => "N1",
                NumberDisplayFormat.Number2 => "N2",
                NumberDisplayFormat.Number3 => "N3",
                _ => fallback,
            };
        }

        /// <summary>
        /// Appends to a reusable character buffer, growing it when needed.
        /// </summary>
        internal static void Append(ref char[] buffer, ref int length, ReadOnlySpan<char> value)
        {
            if (length + value.Length > buffer.Length)
                Array.Resize(ref buffer, Mathf.Max(buffer.Length * 2, length + value.Length));

            value.CopyTo(buffer.AsSpan(length));
            length += value.Length;
        }

        internal static void Append(ref char[] buffer, ref int length, char value)
        {
            if (length == buffer.Length)
                Array.Resize(ref buffer, buffer.Length * 2);

            buffer[length++] = value;
        }

        /// <summary>
        /// Returns the cached string while it matches the text, otherwise replaces it with a new one.
        /// </summary>
        internal static string ReuseString(ref string cached, ReadOnlySpan<char> text)
        {
            if (cached == null || !text.SequenceEqual(cached.AsSpan()))
                cached = text.ToString();

            return cached;
        }

        /// <summary>
        /// Caches uniform digit spacing until the text's font, style or spacing settings change.
        /// TextMesh Pro spaces digits with an mspace tag. Providers with tabular figures switch them on in play mode instead.
        /// </summary>
        public struct DigitSpacingCache
        {
            Graphic cachedText;
            TMP_FontAsset cachedFont;
            FontStyles cachedStyle;
            FontWeight cachedWeight;
            float cachedScale;
            bool cachedEnabled;
            bool cachedRichText;
            bool cachedPlaying;
            string openTag;

            /// <summary>
            /// Tag that opens the spaced number, or empty when the text needs none.
            /// </summary>
            public readonly string OpenTag => openTag ?? string.Empty;

            /// <summary>
            /// Returns true when the cached settings change.
            /// </summary>
            public bool Update(Graphic textObject, bool enabled, float widthScale)
            {
                var tmp = textObject as TMP_Text;
                TMP_FontAsset font = tmp != null ? tmp.font : null;
                FontStyles style = tmp != null ? tmp.fontStyle : FontStyles.Normal;
                FontWeight weight = tmp != null ? tmp.fontWeight : FontWeight.Regular;
                bool richText = tmp != null && tmp.richText;

                // Tabular figures only apply in play mode, so entering it refreshes them
                bool playing = Application.isPlaying;

                if (openTag != null && cachedText == textObject && cachedFont == font && cachedStyle == style
                    && cachedWeight == weight && cachedScale.Equals(widthScale)
                    && cachedEnabled == enabled && cachedRichText == richText && cachedPlaying == playing)
                    return false;

                cachedText = textObject;
                cachedFont = font;
                cachedStyle = style;
                cachedWeight = weight;
                cachedScale = widthScale;
                cachedEnabled = enabled;
                cachedRichText = richText;
                cachedPlaying = playing;

                if (tmp != null) { openTag = enabled && richText ? BuildDigitSpacingOpenTag(tmp, widthScale) : string.Empty; }
                else
                {
                    TextProvider.Find(textObject)?.SetTabularDigits(textObject, enabled);
                    openTag = string.Empty;
                }

                return true;
            }
        }

        /// <summary>
        /// Builds an em-based spacing tag using the widest digit, including font fallbacks.
        /// </summary>
        public static string BuildDigitSpacingOpenTag(TMP_Text textObject, float widthScale = 1f)
        {
            float width = GetWidestDigitWidthEm(textObject) * widthScale;
            return $"<mspace={width.ToString("0.###", CultureInfo.InvariantCulture)}em duospace=1>";
        }

        static float GetWidestDigitWidthEm(TMP_Text textObject)
        {
            if (textObject == null || textObject.font == null)
                return 1f;

            float widest = 0f;
            TMP_FontAsset sourceFont = textObject.font;

            for (uint unicode = '0'; unicode <= '9'; unicode++)
            {
                TMP_Character character = TMP_FontAssetUtilities.GetCharacterFromFontAsset(unicode, sourceFont,
                    true, textObject.fontStyle, textObject.fontWeight, out _);
           
                if (character?.glyph == null)
                    continue;

                TMP_FontAsset characterFont = character.textAsset as TMP_FontAsset;
                if (characterFont == null)
                    characterFont = sourceFont;

                float pointSize = Mathf.Max(1f, characterFont.faceInfo.pointSize);
                float glyphWidth = Mathf.Max(character.glyph.metrics.width, character.glyph.metrics.horizontalAdvance);
                float glyphScale = characterFont.faceInfo.scale * character.scale * character.glyph.scale;
                widest = Mathf.Max(widest, glyphWidth * glyphScale / pointSize);
            }

            return Mathf.Max(0.01f, widest);
        }
        #endregion

        #region Canvas
        static Canvas resolvedCanvas;

        /// <summary>
        /// Used with dynamic objects where Canvas is required but missing.
        /// </summary>
        public static Canvas ResolveCanvas()
        {
            if (resolvedCanvas == null)
            {
                GameObject canvasGO = new("Canvas [Generated]");

                Canvas canvas = canvasGO.AddComponent<Canvas>();
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.vertexColorAlwaysGammaSpace = true;
                canvas.sortingOrder = 1000;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2560, 1440);
                scaler.matchWidthOrHeight = 0.5f;

                resolvedCanvas = canvas;
            }

            return resolvedCanvas;
        }
        #endregion

        #region Layout
        /// <summary>
        /// The axis a bar grows its fill along when it drives the fill's anchorMax: 0 for horizontal, 1 for vertical.
        /// </summary>
        internal static int GetFillAxis(RectTransform fill)
        {
            // The fill stays stretched across the axis the bar doesn't drive
            Vector2 anchorMax = fill.anchorMax;
            bool stretchedX = anchorMax.x >= 1f, stretchedY = anchorMax.y >= 1f;
            if (stretchedX != stretchedY)
                return stretchedY ? 0 : 1;

            // Empty, full or not stretched: the area it fills decides. The fill's own size follows the value,
            // so a bar saved empty or nearly empty would read as the other axis
            Vector2 size = fill.parent is RectTransform area ? area.rect.size : fill.rect.size;
            return size.x >= size.y ? 0 : 1;
        }
        #endregion
    }
}