using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    /// <summary>
    /// Controls a shader-based "Pixel Curtains" transition effect on a UI Image.
    /// </summary>
    [AddComponentMenu("Evo/UI/Effects/Pixel Curtain"), HelpURL(Constants.HelpUrl)]
    [ExecuteInEditMode, DisallowMultipleComponent, RequireComponent(typeof(Image), typeof(RectTransform))]
    public class PixelCurtain : UIBehaviour, IMeshModifier
    {
        // Appearance
        [Tooltip("Dynamically slightly stretches the cells to ensure whole numbers fit the rect, preventing cut-off edges.")]
        public bool adaptToRectSize = true;
        [Min(1f), Tooltip("Target size of each pixel square. The grid adjusts to keep perfect squares based on the RectTransform size.")]
        public float cellSize = 64f;
        [Tooltip("Direction of the transition wave. e.g. (0, -1) is Top-to-Bottom. (0, 0) for random noise reveal.")]
        public Vector2 direction = new(0f, -1f);
        [Range(0f, 2f), Tooltip("How much randomness to apply to individual pixel appearances.")]
        public float randomness = 0.5f;
        [Range(0f, 1f), Tooltip("How soft the fade is for each pixel. Lower values make it pop instantly.")]
        public float fadeWidth = 0.25f;

        // Animation
        [Range(0f, 1f)] public float progress = 0f;
        [Min(0f)] public float transitionDuration = 1f;

        // Behavior
        [Tooltip("Action to perform automatically when this component is enabled.")]
        public EnableAction actionOnEnable = EnableAction.None;
        [Tooltip("Fade out will go from the start point to the end point.")]
        public bool invertFadeOutDirection = true;
        [Tooltip("Disables the GameObject when the fade out is complete, and enables it when a transition starts.")]
        public bool cullObject = true;
        [Tooltip("Automatically hold and fade out after PlayTransition fills the screen.")]
        public bool autoFadeOut = false;
        [Min(0f), Tooltip("How long to wait when the screen is fully filled before starting the fade out.")]
        public float holdDuration = 0.5f;

        // Button Integration
        [Tooltip("Hook up a button to paly the transitions based on its state.")]
        public Button button;
        [Tooltip("Target state to play the fade-in transition.")]
        public InteractionState targetState = InteractionState.Selected;

        // Events
        public UnityEvent onFadeInComplete = new();
        public UnityEvent onFadeOutComplete = new();

        // Enums
        public enum EnableAction { None, FadeIn, FadeOut, FullTransition }
        enum Phase { Idle, FadeIn, Hold, FadeOut }

        // Cache
        Image targetImage;
        RectTransform rectTransform;
        Material instancedMaterial;
        Material originalMaterial;

        // State
        Phase phase;
        Vector2 activeDirection;
        float startProgress;
        float phaseDuration;
        float elapsed;
        bool fadeOutAfterIn;
        bool activating;
        uint transitionVersion;
#if UNITY_EDITOR
        bool propertiesDirty;
#endif

        // Shader
        [SerializeField, HideInInspector] Shader embeddedShader;
        const string ShaderName = "Evo/UI/Pixel Curtain";
        static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int DirectionId = Shader.PropertyToID("_Direction");
        static readonly int RandomnessId = Shader.PropertyToID("_Randomness");
        static readonly int FadeWidthId = Shader.PropertyToID("_FadeWidth");
        static readonly int DirectionParamsId = Shader.PropertyToID("_DirectionParams");

        protected override void Awake()
        {
            base.Awake();

            if (Application.isPlaying && button != null)
                button.OnStateChanged += OnButtonStateChanged;
        }

        protected override void Start()
        {
            base.Start();

            if (Application.isPlaying && cullObject && progress <= 0f && phase == Phase.Idle)
                gameObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            activeDirection = direction;

            if (ValidateResources())
            {
                UpdateMaterialProperties();
                targetImage.SetVerticesDirty();
            }

            if (!Application.isPlaying || activating)
                return;

            switch (actionOnEnable)
            {
                case EnableAction.FadeIn: 
                    PlayIn();
                    break;
                case EnableAction.FadeOut: 
                    PlayOut(); 
                    break;
                case EnableAction.FullTransition:
                    PlayTransition();
                    break;
            }
        }

        protected override void OnDisable()
        {
            phase = Phase.Idle;
            transitionVersion++;
            RestoreMaterial();

            if (targetImage != null)
                targetImage.SetVerticesDirty();

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (button != null)
                button.OnStateChanged -= OnButtonStateChanged;
          
            RestoreMaterial();
           
            if (instancedMaterial != null)
            {
                if (Application.isPlaying) 
                    Destroy(instancedMaterial);
                else 
                    DestroyImmediate(instancedMaterial);
               
                instancedMaterial = null;
            }

            base.OnDestroy();
        }

        void Update()
        {
#if UNITY_EDITOR
            if (propertiesDirty)
            {
                propertiesDirty = false;
                if (ValidateResources())
                    UpdateMaterialProperties();
            }
#endif
            if (!Application.isPlaying)
                return;

            Tick(Time.deltaTime);
        }

        void RestoreMaterial()
        {
            if (targetImage != null && instancedMaterial != null && targetImage.material == instancedMaterial)
                targetImage.material = originalMaterial;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
          
            if (IsActive())
                UpdateMaterialProperties();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();

            if (IsActive())
                EnsureCanvasShaderChannels();
        }

        void EnsureCanvasShaderChannels()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();

            Canvas canvas = targetImage != null ? targetImage.canvas : null;
            if (canvas != null && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        public void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive())
                return;
            if (rectTransform == null)
                rectTransform = transform as RectTransform;
            if (rectTransform == null)
                return;

            Rect rect = rectTransform.rect;
            float inverseWidth = 1f / Mathf.Max(rect.width, 0.0001f);
            float inverseHeight = 1f / Mathf.Max(rect.height, 0.0001f);
            UIVertex vertex = default;

            // Capture local rect coordinates before Canvas batching transforms positions.
            // UV0 remains untouched for sliced, tiled, and atlased sprite sampling.
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.uv1.x = (vertex.position.x - rect.xMin) * inverseWidth;
                vertex.uv1.y = (vertex.position.y - rect.yMin) * inverseHeight;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        public void ModifyMesh(Mesh mesh)
        {
            if (!IsActive())
                return;

            using var vertexHelper = new VertexHelper(mesh);
            ModifyMesh(vertexHelper);
            vertexHelper.FillMesh(mesh);
        }

        bool ValidateResources()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (rectTransform == null)
                rectTransform = transform as RectTransform;
            if (targetImage == null || rectTransform == null)
                return false;

            EnsureCanvasShaderChannels();

            if (instancedMaterial == null)
            {
                Shader shader = embeddedShader != null ? embeddedShader : Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning("[Evo UI] Shader 'Evo/UI/Pixel Curtain' not found.", this);
                    return false;
                }
#if UNITY_EDITOR
                embeddedShader = shader;
#endif
                instancedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (targetImage.material != instancedMaterial)
            {
                originalMaterial = targetImage.material;
                targetImage.material = instancedMaterial;
            }

            return true;
        }

        void OnButtonStateChanged(InteractionState state)
        {
            // Stay subscribed while culled so the button can wake the effect
            if (!enabled)
                return;    

            if (state == targetState)
                PlayIn();
            else
                PlayOut();
        }

        void Tick(float deltaTime)
        {
            if (phase == Phase.Idle)
                return;

            elapsed += deltaTime;

            if (phase == Phase.Hold)
            {
                if (elapsed >= phaseDuration)
                    BeginFade(Phase.FadeOut);

                return;
            }

            float target = phase == Phase.FadeIn ? 1f : 0f;
            progress = phaseDuration > 0f ? Mathf.SmoothStep(startProgress, target, Mathf.Clamp01(elapsed / phaseDuration)) : target;
            UpdateMaterialProperties();

            if (elapsed >= phaseDuration)
                CompleteFade();
        }

        void BeginFade(Phase nextPhase)
        {
            phase = nextPhase;
            startProgress = Mathf.Clamp01(progress);
            float target = phase == Phase.FadeIn ? 1f : 0f;
            phaseDuration = Mathf.Max(0f, transitionDuration) * Mathf.Abs(target - startProgress);
            elapsed = 0f;
            activeDirection = phase == Phase.FadeOut && invertFadeOutDirection ? -direction : direction;
            progress = startProgress;            
            UpdateMaterialProperties();
        }

        void CompleteFade()
        {
            bool fadedIn = phase == Phase.FadeIn;
            bool continueOut = fadedIn && fadeOutAfterIn;
            uint version = transitionVersion;
            phase = Phase.Idle;

            if (fadedIn)
                onFadeInComplete?.Invoke();
            else
                onFadeOutComplete?.Invoke();

            // A listener can replace the transition, disable, or destroy this component
            if (this == null || !isActiveAndEnabled || version != transitionVersion)
                return;

            if (continueOut && autoFadeOut)
            {
                phase = Phase.Hold;
                phaseDuration = Mathf.Max(0f, holdDuration);
                elapsed = 0f;

                if (phaseDuration <= 0f)
                    BeginFade(Phase.FadeOut);
            }
            else if (!fadedIn && cullObject)
                gameObject.SetActive(false);
        }

        bool PreparePlayback()
        {
            if (!Application.isPlaying || !enabled)
                return false;

            transitionVersion++;
            phase = Phase.Idle;
            activating = true;
           
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
          
            activating = false;
            return isActiveAndEnabled && ValidateResources();
        }

        void ApplyMaterialProperties(Material material)
        {
            float size = Mathf.Max(cellSize, 1f);
            Rect rect = rectTransform.rect;
            Vector2 gridSize = new(rect.width / size, rect.height / size);

            if (adaptToRectSize)
            {
                gridSize.x = Mathf.Max(1f, Mathf.Round(gridSize.x));
                gridSize.y = Mathf.Max(1f, Mathf.Round(gridSize.y));
            }
            else
            {
                gridSize.x = Mathf.Max(0.0001f, gridSize.x);
                gridSize.y = Mathf.Max(0.0001f, gridSize.y);
            }

            material.SetVector(GridSizeId, gridSize);
            material.SetFloat(ProgressId, Mathf.Clamp01(progress));

            float sqrMag = activeDirection.sqrMagnitude;
            if (sqrMag < 0.001f)
            {
                material.SetVector(DirectionParamsId, Vector4.zero);
                material.SetVector(DirectionId, Vector2.zero);
            }
            else
            {
                Vector2 normalizedDir = activeDirection / Mathf.Sqrt(sqrMag);
                float minProj = Mathf.Min(0f, normalizedDir.x) + Mathf.Min(0f, normalizedDir.y);
                float maxProj = Mathf.Max(0f, normalizedDir.x) + Mathf.Max(0f, normalizedDir.y);
                float projScale = 1f / Mathf.Max(maxProj - minProj, 0.0001f);
                material.SetVector(DirectionParamsId, new Vector4(minProj, projScale, 1f, 0f));
                material.SetVector(DirectionId, normalizedDir);
            }

            material.SetFloat(RandomnessId, Mathf.Clamp(randomness, 0f, 2f));
            material.SetFloat(FadeWidthId, Mathf.Clamp01(fadeWidth));
        }

        /// <summary>
        /// Fades in, then optionally holds and fades out when autoFadeOut is enabled.
        /// </summary>
        public void PlayTransition(bool force = false)
        {
            if (!PreparePlayback())
                return;
            if (force)
                progress = 0f;

            fadeOutAfterIn = true;
            BeginFade(Phase.FadeIn);
        }

        /// <summary>
        /// Fades in from the current progress. Force restarts from empty.
        /// </summary>
        public void PlayIn(bool force = false)
        {
            if (!PreparePlayback())
                return;
            if (force)
                progress = 0f;

            fadeOutAfterIn = false;
          
            if (progress >= 1f)
            {
                UpdateMaterialProperties();
                return;
            }

            BeginFade(Phase.FadeIn);
        }

        /// <summary>
        /// Fades out from the current progress. Force restarts from full.
        /// </summary>
        public void PlayOut(bool force = false)
        {
            if (!Application.isPlaying || !enabled)
                return;
           
            if (!force && progress <= 0f)
            {
                transitionVersion++;
                phase = Phase.Idle;
                UpdateMaterialProperties();
               
                if (cullObject && gameObject.activeSelf)
                    gameObject.SetActive(false);
              
                return;
            }
           
            if (!PreparePlayback())
                return;
            if (force)
                progress = 1f;
            
            fadeOutAfterIn = false;
            BeginFade(Phase.FadeOut);
        }

        /// <summary>
        /// Refreshes the effect after changing its public appearance fields at runtime.
        /// </summary>
        public void UpdateMaterialProperties()
        {
            if (instancedMaterial == null || rectTransform == null || targetImage == null)
                return;
            if (phase == Phase.Idle)
                activeDirection = direction;

            ApplyMaterialProperties(instancedMaterial);

            // uGUI masks render a cached material copy; updating only the source leaves it frozen
            CanvasRenderer renderer = targetImage.canvasRenderer;
            Material renderedMaterial = renderer.materialCount > 0 ? renderer.GetMaterial(0) : null;
            if (renderedMaterial != null && renderedMaterial != instancedMaterial
                && renderedMaterial.shader == instancedMaterial.shader)
                ApplyMaterialProperties(renderedMaterial);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            cellSize = Mathf.Max(1f, cellSize);
            progress = Mathf.Clamp01(progress);
            transitionDuration = Mathf.Max(0f, transitionDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            randomness = Mathf.Clamp(randomness, 0f, 2f);
            fadeWidth = Mathf.Clamp01(fadeWidth);
            propertiesDirty = true;
        }

        protected override void Reset()
        {
            base.Reset();
            embeddedShader = Shader.Find(ShaderName);
        }
#endif
    }
}