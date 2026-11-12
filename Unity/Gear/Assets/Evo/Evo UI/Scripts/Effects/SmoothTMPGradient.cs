using System;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Applies a horizontal gradient across TextMeshPro characters using the top two gradient colors.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [HelpURL(Constants.HelpUrl + "effects/smooth-tmp-gradient")]
    [AddComponentMenu("Evo/UI/Effects/Smooth TMP Gradient")]
    public class SmoothTMPGradient : MonoBehaviour
    {
        TMP_Text tmpComponent;
        Action<TMP_TextInfo> gradientCallback;

        void OnEnable()
        {
            if (tmpComponent == null && !TryGetComponent(out tmpComponent))
                return;

            gradientCallback ??= ApplyGradient;
            tmpComponent.OnPreRenderText += gradientCallback;
            RefreshGradient();
        }

        void OnDisable()
        {
            if (tmpComponent == null)
                return;

            tmpComponent.OnPreRenderText -= gradientCallback;
            RefreshGradient();
        }

        void ApplyGradient(TMP_TextInfo textInfo)
        {
            if (!isActiveAndEnabled || tmpComponent == null || !tmpComponent.enableVertexGradient)
                return;

            int count = textInfo.characterCount;
            if (count == 0)
                return;

            TMP_ColorGradient preset = tmpComponent.colorGradientPreset;
            Color start = preset != null ? preset.topLeft : tmpComponent.colorGradient.topLeft;
            Color end = preset != null ? preset.topRight : tmpComponent.colorGradient.topRight;
            float inverseCount = 1f / count;
            byte textAlpha = ((Color32)tmpComponent.color).a;

            for (int i = 0; i < count; i++)
            {
                ref TMP_CharacterInfo character = ref textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                Color left = Color.Lerp(start, end, i * inverseCount);
                Color right = Color.Lerp(start, end, (i + 1) * inverseCount);

                // Preserve text / rich-text opacity without multiplying TMP's per-glyph gradient twice.
                float alpha = Mathf.Min(textAlpha, character.color.a) / 255f;
                left.a *= alpha;
                right.a *= alpha;

                Color32[] colors = textInfo.meshInfo[character.materialReferenceIndex].colors32;
                int vertexIndex = character.vertexIndex;
                colors[vertexIndex] = colors[vertexIndex + 1] = left;      // Bottom-left, top-left.
                colors[vertexIndex + 2] = colors[vertexIndex + 3] = right; // Top-right, bottom-right.
            }
        }

        /// <summary>
        /// Requests a gradient refresh on the next TMP mesh rebuild.
        /// Call this after modifying a shared gradient preset directly at runtime.
        /// </summary>
        public void RefreshGradient()
        {
            if (tmpComponent == null)
                return;

            tmpComponent.havePropertiesChanged = true;
            tmpComponent.SetVerticesDirty();
        }
    }
}