using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ProceduralShape), true), CanEditMultipleObjects, NoAutoStaticsCleanup]
    public class ProceduralShapeEditor : UnityEditor.Editor
    {
        // Shape
        SerializedProperty shape, shapeAngle, shapeSkew;
        SerializedProperty polygonSides;
        SerializedProperty operandOnly, shapeOperations;
        ReorderableListOptions operationListOptions;
        static readonly System.Func<SerializedProperty, int, string> OperationTitleGetter = GetOperationTitle;
        static readonly System.Action<SerializedProperty> OperationDrawer = DrawOperation;

        // Base Graphic
        SerializedProperty sprite, material;
        SerializedProperty scaleMode;
        SerializedProperty color;
        SerializedProperty raycastMode, raycastTarget, raycastPadding;
        SerializedProperty maskable;

        // Clipping
        SerializedProperty clipMethod, clipOrigin, clipAmount, clipClockwise;
        SerializedProperty roundedClipEdges;

        // Fill
        SerializedProperty fillCenter;
        SerializedProperty softness, dither, backdropBlur;
        SerializedProperty fillColorMode, fillColor, fillStyler;
        SerializedProperty fillGradientStyler, patternGradientStyler, outlineGradientStyler, innerShadowGradientStyler, outerShadowGradientStyler;
        SerializedProperty fillGradient, fillGradientType, fillGradientSpace, fillGradientAngle, fillGradientZoom, fillGradientReverse;

        // Pattern
        SerializedProperty fillPattern, patternSource, patternSprite, patternShape;
        SerializedProperty patternPolygonSides, patternRadius, patternSize, patternGap;
        SerializedProperty patternOffset, patternAngle, patternFalloff, patternFalloffOrigin;
        SerializedProperty patternFalloffStart, patternFalloffEnd, patternColorMode;
        SerializedProperty patternColor, patternStyler, patternGradient, patternGradientType, patternGradientSpace;
        SerializedProperty patternGradientAngle, patternGradientZoom, patternGradientReverse;

        // Outline
        SerializedProperty outlineWidth;
        SerializedProperty outlineColorMode, outlineColor, outlineStyler;
        SerializedProperty outlineDashed, outlineDashLength, outlineDashGap, outlineDashOffset, outlineDashRounded;
        SerializedProperty outlineGradient, outlineGradientType, outlineGradientSpace, outlineGradientAngle, outlineGradientZoom, outlineGradientReverse;

        // Radius
        SerializedProperty radiusMode;
        SerializedProperty independentCorners;
        SerializedProperty squircleCorners;
        SerializedProperty cornerRadius;
        SerializedProperty radiusSyncMode, radiusSyncTarget, radiusSyncTargetName;

        // Inner Shadow
        SerializedProperty innerShadowStyler;
        SerializedProperty innerShadowOffset, innerShadowSize, innerShadowSoftness;
        SerializedProperty innerShadowColorMode, innerShadowColor, innerShadowGradient;
        SerializedProperty innerShadowGradientType, innerShadowGradientSpace, innerShadowGradientAngle, innerShadowGradientZoom, innerShadowGradientReverse;

        // Outer Shadow
        SerializedProperty outerShadowOutsideOnly;
        SerializedProperty outerShadowStyler;
        SerializedProperty outerShadowOffset, outerShadowSize, outerShadowSoftness;
        SerializedProperty outerShadowColorMode, outerShadowColor, outerShadowGradient;
        SerializedProperty outerShadowGradientType, outerShadowGradientSpace, outerShadowGradientAngle, outerShadowGradientZoom, outerShadowGradientReverse;

        // Styler
        SerializedProperty preset, useDefaultStylerPreset, shapeStyleID;
        SerializedProperty interactableObject;

        // Editor Stuff
        Styler.ShapeStyleItem activeShapeStyle;
        static readonly string[] HorizontalOriginLabels = { "Left", "Right" };
        static readonly string[] VerticalOriginLabels = { "Bottom", "Top" };
        static readonly string[] RadialOriginLabels = { "Bottom", "Right", "Top", "Left" };
        static GUIStyle cornerFloatStyle;
        static GUIStyle cornerUnitStyle;
        static bool cornerStylesProSkin;

        void OnEnable()
        {
            shape = serializedObject.FindProperty("shape");
            polygonSides = serializedObject.FindProperty("polygonSides");
            shapeAngle = serializedObject.FindProperty("shapeAngle");
            shapeSkew = serializedObject.FindProperty("shapeSkew");
            operandOnly = serializedObject.FindProperty("operandOnly");
            shapeOperations = serializedObject.FindProperty("shapeOperations");
            operationListOptions = ReorderableListOptions.WithControlName("ProceduralShape.Operations")
                .WithTitle("Operations")
                .WithEmptyMessage("Add an operation to combine this shape with another shape.")
                .WithAddLabel("Add Operation")
                .WithItemInitializer(InitializeOperation)
                .WithDeleteTooltip("Delete operation")
                .WithContentHeader(DrawOperationsHeader);

            sprite = serializedObject.FindProperty("sprite");
            material = serializedObject.FindProperty("m_Material");
            scaleMode = serializedObject.FindProperty("scaleMode");
            color = serializedObject.FindProperty("m_Color");
            raycastMode = serializedObject.FindProperty("raycastMode");
            raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            maskable = serializedObject.FindProperty("m_Maskable");

            clipMethod = serializedObject.FindProperty("clipMethod");
            clipOrigin = serializedObject.FindProperty("clipOrigin");
            clipAmount = serializedObject.FindProperty("clipAmount");
            clipClockwise = serializedObject.FindProperty("clipClockwise");
            roundedClipEdges = serializedObject.FindProperty("roundedClipEdges");

            fillCenter = serializedObject.FindProperty("fillCenter");
            backdropBlur = serializedObject.FindProperty("backdropBlur");
            softness = serializedObject.FindProperty("softness");
            dither = serializedObject.FindProperty("dither");
            fillColorMode = serializedObject.FindProperty("fillColorMode");
            fillColor = serializedObject.FindProperty("fillColor");
            fillStyler = serializedObject.FindProperty("fillStyler");
            fillGradient = serializedObject.FindProperty("fillGradient");
            fillGradientStyler = serializedObject.FindProperty("fillGradientStyler");
            fillGradientType = serializedObject.FindProperty("fillGradientType");
            fillGradientSpace = serializedObject.FindProperty("fillGradientSpace");
            fillGradientAngle = serializedObject.FindProperty("fillGradientAngle");
            fillGradientZoom = serializedObject.FindProperty("fillGradientZoom");
            fillGradientReverse = serializedObject.FindProperty("fillGradientReverse");

            fillPattern = serializedObject.FindProperty("fillPattern");
            patternSource = serializedObject.FindProperty("patternSource");
            patternSprite = serializedObject.FindProperty("patternSprite");
            patternShape = serializedObject.FindProperty("patternShape");
            patternPolygonSides = serializedObject.FindProperty("patternPolygonSides");
            patternRadius = serializedObject.FindProperty("patternRadius");
            patternSize = serializedObject.FindProperty("patternSize");
            patternGap = serializedObject.FindProperty("patternGap");
            patternOffset = serializedObject.FindProperty("patternOffset");
            patternAngle = serializedObject.FindProperty("patternAngle");
            patternFalloff = serializedObject.FindProperty("patternFalloff");
            patternFalloffOrigin = serializedObject.FindProperty("patternFalloffOrigin");
            patternFalloffStart = serializedObject.FindProperty("patternFalloffStart");
            patternFalloffEnd = serializedObject.FindProperty("patternFalloffEnd");
            patternColorMode = serializedObject.FindProperty("patternColorMode");
            patternColor = serializedObject.FindProperty("patternColor");
            patternStyler = serializedObject.FindProperty("patternStyler");
            patternGradient = serializedObject.FindProperty("patternGradient");
            patternGradientStyler = serializedObject.FindProperty("patternGradientStyler");
            patternGradientType = serializedObject.FindProperty("patternGradientType");
            patternGradientSpace = serializedObject.FindProperty("patternGradientSpace");
            patternGradientAngle = serializedObject.FindProperty("patternGradientAngle");
            patternGradientZoom = serializedObject.FindProperty("patternGradientZoom");
            patternGradientReverse = serializedObject.FindProperty("patternGradientReverse");

            outlineWidth = serializedObject.FindProperty("outlineWidth");
            outlineDashed = serializedObject.FindProperty("outlineDashed");
            outlineDashLength = serializedObject.FindProperty("outlineDashLength");
            outlineDashGap = serializedObject.FindProperty("outlineDashGap");
            outlineDashOffset = serializedObject.FindProperty("outlineDashOffset");
            outlineDashRounded = serializedObject.FindProperty("outlineDashRounded");
            outlineColorMode = serializedObject.FindProperty("outlineColorMode");
            outlineColor = serializedObject.FindProperty("outlineColor");
            outlineStyler = serializedObject.FindProperty("outlineStyler");
            outlineGradient = serializedObject.FindProperty("outlineGradient");
            outlineGradientStyler = serializedObject.FindProperty("outlineGradientStyler");
            outlineGradientType = serializedObject.FindProperty("outlineGradientType");
            outlineGradientSpace = serializedObject.FindProperty("outlineGradientSpace");
            outlineGradientAngle = serializedObject.FindProperty("outlineGradientAngle");
            outlineGradientZoom = serializedObject.FindProperty("outlineGradientZoom");
            outlineGradientReverse = serializedObject.FindProperty("outlineGradientReverse");

            radiusMode = serializedObject.FindProperty("radiusMode");
            independentCorners = serializedObject.FindProperty("independentCorners");
            squircleCorners = serializedObject.FindProperty("squircleCorners");
            cornerRadius = serializedObject.FindProperty("cornerRadius");
            radiusSyncMode = serializedObject.FindProperty("radiusSyncMode");
            radiusSyncTarget = serializedObject.FindProperty("radiusSyncTarget");
            radiusSyncTargetName = serializedObject.FindProperty("radiusSyncTargetName");

            innerShadowOffset = serializedObject.FindProperty("innerShadowOffset");
            innerShadowSize = serializedObject.FindProperty("innerShadowSize");
            innerShadowSoftness = serializedObject.FindProperty("innerShadowSoftness");
            innerShadowColorMode = serializedObject.FindProperty("innerShadowColorMode");
            innerShadowColor = serializedObject.FindProperty("innerShadowColor");
            innerShadowStyler = serializedObject.FindProperty("innerShadowStyler");
            innerShadowGradient = serializedObject.FindProperty("innerShadowGradient");
            innerShadowGradientStyler = serializedObject.FindProperty("innerShadowGradientStyler");
            innerShadowGradientType = serializedObject.FindProperty("innerShadowGradientType");
            innerShadowGradientSpace = serializedObject.FindProperty("innerShadowGradientSpace");
            innerShadowGradientAngle = serializedObject.FindProperty("innerShadowGradientAngle");
            innerShadowGradientZoom = serializedObject.FindProperty("innerShadowGradientZoom");
            innerShadowGradientReverse = serializedObject.FindProperty("innerShadowGradientReverse");

            outerShadowOffset = serializedObject.FindProperty("outerShadowOffset");
            outerShadowSize = serializedObject.FindProperty("outerShadowSize");
            outerShadowSoftness = serializedObject.FindProperty("outerShadowSoftness");
            outerShadowOutsideOnly = serializedObject.FindProperty("outerShadowOutsideOnly");
            outerShadowColorMode = serializedObject.FindProperty("outerShadowColorMode");
            outerShadowColor = serializedObject.FindProperty("outerShadowColor");
            outerShadowStyler = serializedObject.FindProperty("outerShadowStyler");
            outerShadowGradient = serializedObject.FindProperty("outerShadowGradient");
            outerShadowGradientStyler = serializedObject.FindProperty("outerShadowGradientStyler");
            outerShadowGradientType = serializedObject.FindProperty("outerShadowGradientType");
            outerShadowGradientSpace = serializedObject.FindProperty("outerShadowGradientSpace");
            outerShadowGradientAngle = serializedObject.FindProperty("outerShadowGradientAngle");
            outerShadowGradientZoom = serializedObject.FindProperty("outerShadowGradientZoom");
            outerShadowGradientReverse = serializedObject.FindProperty("outerShadowGradientReverse");

            useDefaultStylerPreset = serializedObject.FindProperty("useDefaultStylerPreset");
            preset = serializedObject.FindProperty("preset");
            shapeStyleID = serializedObject.FindProperty("shapeStyleID");
            interactableObject = serializedObject.FindProperty("interactableObject");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            activeShapeStyle = GetActiveShapeStyle();
            EvoEditorGUI.BeginCenteredInspector();

            BaseSection();
            ShapeSection();
            FillSection();
            OutlineSection();
            ShadowsSection();
            StylerSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void BaseSection()
        {
            bool styleControlsGraphic = IsControlledByShapeStyle(Styler.ShapeStyleFlags.Graphic);

            if (EvoEditorGUI.BeginFoldout(target, "Graphic", true, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    using (new EditorGUI.DisabledScope(styleControlsGraphic))
                        EvoEditorGUI.Property(sprite, "Source Image", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Property(color, "Base Color", FieldOptions.WithInvertedColor());

                    using (new EditorGUI.DisabledScope(styleControlsGraphic))
                        EvoEditorGUI.Property(scaleMode, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(clipMethod, FieldOptions.WithoutSpace().WithoutBackground());

                        var method = (ProceduralShape.ClipMethod)clipMethod.intValue;
                        if (method != ProceduralShape.ClipMethod.None)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            {
                                string[] labels = method switch
                                {
                                    ProceduralShape.ClipMethod.Horizontal => HorizontalOriginLabels,
                                    ProceduralShape.ClipMethod.Vertical => VerticalOriginLabels,
                                    ProceduralShape.ClipMethod.Radial360 => RadialOriginLabels,
                                    _ => HorizontalOriginLabels,
                                };

                                // Write only an out-of-range origin: an unconditional write copies the first shape's origin to every selected shape
                                int clampedOrigin = Mathf.Clamp(clipOrigin.intValue, 0, labels.Length - 1);
                                if (!clipOrigin.hasMultipleDifferentValues && clampedOrigin != clipOrigin.intValue)
                                    clipOrigin.intValue = clampedOrigin;
                                EvoEditorGUI.Dropdown(clipOrigin, labels, "Origin");
                                EvoEditorGUI.Property(clipAmount, "Amount");
                                EvoEditorGUI.Toggle(clipClockwise, "Clockwise", FieldOptions.WithoutSpace());
                                bool linearRectangle = shape.intValue == (int)ProceduralShape.ShapeType.Rectangle &&
                                    (method == ProceduralShape.ClipMethod.Horizontal || method == ProceduralShape.ClipMethod.Vertical);
                                if (linearRectangle || method == ProceduralShape.ClipMethod.Radial360)
                                {
                                    EvoEditorGUI.AddLayoutSpace();
                                    using (new EditorGUI.DisabledScope(!linearRectangle && fillCenter.boolValue))
                                        EvoEditorGUI.Toggle(roundedClipEdges, "Rounded Edges", FieldOptions.WithoutSpace());
                                }
                            }
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.Property(raycastMode, FieldOptions.WithoutSpace().WithoutBackground());
                        if (EditorGUI.EndChangeCheck())
                        {
                            // Bind our custom enum visually directly into the underlying Unity UI Hit-Test System
                            raycastTarget.boolValue = raycastMode.intValue != (int)ProceduralShape.RaycastMode.None;
                        }
                        if (raycastTarget.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.ArrayProperty(raycastPadding, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    using (new EditorGUI.DisabledScope(styleControlsGraphic))
                        EvoEditorGUI.Property(softness, "Edge Softness", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Toggle(dither, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(maskable, FieldOptions.WithoutSpace().WithInvertedColor());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void ShapeSection()
        {
            bool styleControlsShape = IsControlledByShapeStyle(Styler.ShapeStyleFlags.Shape);

            if (EvoEditorGUI.BeginFoldout(target, "Shape", true, FoldoutOptions.WithIcon("UI_Shape")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    using (new EditorGUI.DisabledScope(styleControlsShape))
                    {
                        EvoEditorGUI.Property(shape, "Type", FieldOptions.WithoutSpace().WithInvertedColor());
                        DrawShapeControls(shape, polygonSides, shapeAngle, shapeSkew);
                    }

                    if (shape.intValue != (int)ProceduralShape.ShapeType.Ellipse)
                    {
                        EvoEditorGUI.AddLayoutSpace();

                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Radius", nestDepth: 1);
                        {
                            // Draw sync mode
                            bool isRectangle = shape.intValue == (int)ProceduralShape.ShapeType.Rectangle;
                            if (isRectangle)
                            {
                                EvoEditorGUI.BeginColumn();
                                {
                                    EvoEditorGUI.Property(radiusSyncMode, "Sync Mode", FieldOptions.WithoutSpace().WithoutBackground());
                                    if (radiusSyncMode.intValue == (int)ProceduralShape.RadiusSyncMode.MatchTarget)
                                    {
                                        EvoEditorGUI.BeginContainer();
                                        EvoEditorGUI.Property(radiusSyncTarget, "Target", FieldOptions.WithoutSpace().WithInvertedColor());
                                        EvoEditorGUI.EndContainer();
                                    }
                                    else if (radiusSyncMode.intValue == (int)ProceduralShape.RadiusSyncMode.MatchAncestor)
                                    {
                                        bool showSource = AllTargetsEnabled();
                                        FieldOptions nameOptions = FieldOptions.WithInvertedColor().WithTooltip(radiusSyncTargetName.tooltip);
                                        EvoEditorGUI.BeginContainer();
                                        EvoEditorGUI.Property(radiusSyncTargetName, "Target Name", showSource ? nameOptions : nameOptions.WithoutSpace());
                                        if (showSource)
                                            DrawRadiusSyncSource();
                                        EvoEditorGUI.EndContainer();
                                    }
                                }
                                EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
                            }

                            bool radiusIsSynced = isRectangle && radiusSyncMode.intValue != (int)ProceduralShape.RadiusSyncMode.None;
                            using (new EditorGUI.DisabledScope(styleControlsShape))
                                DrawShapeRadiusControls(shape, radiusMode, cornerRadius, independentCorners, squircleCorners, synced: radiusIsSynced);
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();
                    }

                    EvoEditorGUI.AddLayoutSpace();
                    DrawOperations();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        // Prefab assets and disabled shapes resolve their source once enabled in a scene
        bool AllTargetsEnabled()
        {
            foreach (Object item in targets)
            {
                if (!((ProceduralShape)item).isActiveAndEnabled)
                    return false;
            }

            return true;
        }

        void DrawRadiusSyncSource()
        {
            ProceduralShape source = ((ProceduralShape)targets[0]).RadiusSyncSource;
            bool mixed = false;
            for (int i = 1; i < targets.Length && !mixed; i++)
                mixed = ((ProceduralShape)targets[i]).RadiusSyncSource != source;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.showMixedValue = mixed;
                EvoEditorGUI.ObjectField(source, typeof(ProceduralShape), "Found", true,
                    FieldOptions.WithoutSpace().WithInvertedColor().WithTooltip("The shape found in the current hierarchy. None uses this shape's own radius."));
                EditorGUI.showMixedValue = false;
            }
        }

        bool OperationsDisabled => operandOnly.boolValue && !operandOnly.hasMultipleDifferentValues;

        void DrawOperations()
        {
            bool guiEnabled = GUI.enabled;
            try
            {
                EvoEditorGUI.ReorderableList(serializedObject, shapeOperations, OperationTitleGetter, OperationDrawer,
                    OperationsDisabled ? operationListOptions.WithReadOnlyItems() : operationListOptions,
                    compactItems: true, compactHeader: true, invertColors: true, boldHeader: false);
            }
            finally
            {
                GUI.enabled = guiEnabled;
            }

            if (shapeOperations.arraySize > ProceduralShape.MaxShapeOperations)
                EvoEditorGUI.Callout($"Only the first {ProceduralShape.MaxShapeOperations} operations are evaluated.", CalloutType.Warning, spaceAfter: 3);

            if (shapeOperations.arraySize == 0 || OperationsDisabled)
                return;

            if (material.objectReferenceValue != null || material.hasMultipleDifferentValues)
                EvoEditorGUI.Callout("Shape operations require the automatic material. Clear Material to enable them.", CalloutType.Warning);
            if (outlineDashed.boolValue || outlineDashed.hasMultipleDifferentValues)
                EvoEditorGUI.Callout("Combined shapes use a solid outline; dash paths belong to individual primitives.");
        }

        void DrawOperationsHeader()
        {
            EvoEditorGUI.Toggle(operandOnly, "Operand Only", FieldOptions.WithTooltip(operandOnly.tooltip).WithoutSpace());
            EvoEditorGUI.AddPropertySpace();
            GUI.enabled &= !OperationsDisabled;
        }

        static string GetOperationTitle(SerializedProperty item, int index)
        {
            SerializedProperty op = item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.operation));
            SerializedProperty opTarget = item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.target));
            string opName = op.hasMultipleDifferentValues ? "Mixed" : ((ProceduralShape.OperationType)op.intValue).ToString();
            Object targetObject = opTarget.objectReferenceValue;
            string targetName = opTarget.hasMultipleDifferentValues ? "Mixed Targets" : targetObject != null ? targetObject.name : "No Target";
            string title = $"{index + 1}. {opName}: {targetName}";
            return index < ProceduralShape.MaxShapeOperations ? title : $"{title} (not evaluated)";
        }

        static void DrawOperation(SerializedProperty item)
        {
            SerializedProperty operationTarget = item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.target));
            SerializedProperty smoothing = item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.smoothing));

            EvoEditorGUI.BeginContainer(compactHeader: true);
            EvoEditorGUI.Property(operationTarget, FieldOptions.WithInvertedColor().WithTooltip(operationTarget.tooltip));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.operation)), FieldOptions.WithInvertedColor());
            EvoEditorGUI.Property(smoothing, FieldOptions.WithInvertedColor().WithoutSpace().WithTooltip(smoothing.tooltip));
            EvoEditorGUI.EndContainer();
        }

        static void InitializeOperation(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.target)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.operation)).intValue = (int)ProceduralShape.OperationType.Union;
            item.FindPropertyRelative(nameof(ProceduralShape.ShapeOperation.smoothing)).floatValue = 0f;
            item.isExpanded = true;
        }

        void FillSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Fill", false, FoldoutOptions.WithIcon("UI_Fill")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    using (new EditorGUI.DisabledScope(IsControlledByShapeStyle(Styler.ShapeStyleFlags.Fill)))
                        EvoEditorGUI.Toggle(fillCenter, FieldOptions.WithInvertedColor());

                    using (new EditorGUI.DisabledScope(!fillCenter.boolValue && !fillCenter.hasMultipleDifferentValues))
                    {
                        PatternSettings();
                        EvoEditorGUI.AddLayoutSpace();
                        BackdropBlurEditor.DrawSettings(backdropBlur);
                        EvoEditorGUI.AddLayoutSpace();
                        ColorSettings(fillColorMode, fillColor, fillGradient, fillGradientType, fillGradientSpace, fillGradientAngle,
                            fillGradientZoom, fillGradientReverse, fillStyler, fillGradientStyler, invertColor: true);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void PatternSettings()
        {
            if (EvoEditorGUI.BeginToggleFoldout(fillPattern, "Pattern", options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    EvoEditorGUI.Property(patternSource, "Source");
                    if (patternSource.hasMultipleDifferentValues || patternSource.intValue == (int)ProceduralShape.PatternSource.Sprite)
                    {
                        EvoEditorGUI.Property(patternSprite, "Sprite");
                        foreach (Object item in targets)
                        {
                            var graphic = (ProceduralShape)item;
                            if (graphic.PatternType == ProceduralShape.PatternSource.Sprite && !graphic.PatternSpriteCompatible)
                            {
                                EvoEditorGUI.Callout("Assign a rectangular, unrotated sprite. " +
                                    "When a fill sprite is assigned, both must share the same atlas texture and split-alpha texture. " +
                                    "The pattern stays hidden until compatible.",
                                    spaceBefore: 0, spaceAfter: 3);
                                break;
                            }
                        }
                    }

                    if (patternSource.hasMultipleDifferentValues || patternSource.intValue == (int)ProceduralShape.PatternSource.Shape)
                    {
                        EvoEditorGUI.Property(patternShape, "Shape");
                        if (patternShape.hasMultipleDifferentValues || patternShape.intValue == (int)ProceduralShape.ShapeType.Polygon)
                            EvoEditorGUI.Property(patternPolygonSides, "Sides");
                        if (patternShape.hasMultipleDifferentValues || patternShape.intValue != (int)ProceduralShape.ShapeType.Ellipse)
                            EvoEditorGUI.Property(patternRadius, "Radius");
                    }

                    EvoEditorGUI.Property(patternSize, "Size");
                    EvoEditorGUI.Property(patternGap, "Gap");
                    EvoEditorGUI.Property(patternOffset, "Offset");
                    EvoEditorGUI.Property(patternAngle, "Angle");

                    EvoEditorGUI.BeginColumn();
                    {
                        EvoEditorGUI.Toggle(patternFalloff, "Falloff", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (patternFalloff.boolValue || patternFalloff.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(patternFalloffOrigin, "Origin", FieldOptions.WithInvertedColor());
                            EvoEditorGUI.Property(patternFalloffStart, "Start Distance", FieldOptions.WithInvertedColor());
                            EvoEditorGUI.Property(patternFalloffEnd, "End Distance", FieldOptions.WithInvertedColor().WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    ColorSettings(patternColorMode, patternColor, patternGradient, patternGradientType, patternGradientSpace, patternGradientAngle,
                        patternGradientZoom, patternGradientReverse, patternStyler, patternGradientStyler);

                    if (material.objectReferenceValue != null || material.hasMultipleDifferentValues)
                        EvoEditorGUI.Callout("Patterns use the shared automatic material. Clear the explicit Material assignment to enable them.", spaceAfter: 0);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }

        void OutlineSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Outline", false, FoldoutOptions.WithIcon("UI_Outline")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    using (new EditorGUI.DisabledScope(IsControlledByShapeStyle(Styler.ShapeStyleFlags.Outline)))
                        EvoEditorGUI.Property(outlineWidth, "Width", FieldOptions.WithoutSpace().WithInvertedColor());

                    if (outlineWidth.floatValue > 0f)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        if (EvoEditorGUI.BeginToggleFoldout(outlineDashed, "Dashed", options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            {
                                EvoEditorGUI.Property(outlineDashLength, "Length");
                                EvoEditorGUI.Property(outlineDashGap, "Gap");
                                EvoEditorGUI.Property(outlineDashOffset, "Offset");
                                EvoEditorGUI.Toggle(outlineDashRounded, "Rounded Ends", FieldOptions.WithoutSpace());
                            }
                            EvoEditorGUI.EndContainer();
                        }
                        EvoEditorGUI.EndToggleFoldout(false);

                        EvoEditorGUI.AddLayoutSpace();
                        ColorSettings(outlineColorMode, outlineColor, outlineGradient, outlineGradientType, outlineGradientSpace, outlineGradientAngle,
                            outlineGradientZoom, outlineGradientReverse, outlineStyler, outlineGradientStyler, invertColor: true);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void ShadowsSection()
        {
            bool styleControlsInnerShadow = IsControlledByShapeStyle(Styler.ShapeStyleFlags.InnerShadow);
            bool styleControlsOuterShadow = IsControlledByShapeStyle(Styler.ShapeStyleFlags.OuterShadow);

            if (EvoEditorGUI.BeginFoldout(target, "Shadows", false, FoldoutOptions.WithIcon("UI_Shadow")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Inner Shadow", nestDepth: 1);
                    {
                        using (new EditorGUI.DisabledScope(styleControlsInnerShadow))
                        {
                            EvoEditorGUI.Property(innerShadowOffset, "Offset");
                            EvoEditorGUI.Property(innerShadowSize, "Size");
                            EvoEditorGUI.Property(innerShadowSoftness, "Softness", FieldOptions.WithoutSpace());
                        }

                        bool hasShadow = innerShadowSize.floatValue > 0.001f || innerShadowSoftness.floatValue > 0.001f || innerShadowOffset.vector2Value.sqrMagnitude > 0.001f;
                        if (hasShadow)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            ColorSettings(innerShadowColorMode, innerShadowColor, innerShadowGradient, innerShadowGradientType, innerShadowGradientSpace,
                                innerShadowGradientAngle, innerShadowGradientZoom, innerShadowGradientReverse, innerShadowStyler, innerShadowGradientStyler);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddPropertySpace();
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Outer Shadow", nestDepth: 1);
                    {
                        using (new EditorGUI.DisabledScope(styleControlsOuterShadow))
                        {
                            EvoEditorGUI.Property(outerShadowOffset, "Offset");
                            EvoEditorGUI.Property(outerShadowSize, "Size");
                            EvoEditorGUI.Property(outerShadowSoftness, "Softness", FieldOptions.WithoutSpace());
                        }

                        bool hasShadow = outerShadowSize.floatValue > 0.001f || outerShadowSoftness.floatValue > 0.001f || outerShadowOffset.vector2Value.sqrMagnitude > 0.001f;
                        if (hasShadow)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            using (new EditorGUI.DisabledScope(styleControlsOuterShadow))
                                EvoEditorGUI.Toggle(outerShadowOutsideOnly, "Outside Only");
                            ColorSettings(outerShadowColorMode, outerShadowColor, outerShadowGradient, outerShadowGradientType, outerShadowGradientSpace,
                                outerShadowGradientAngle, outerShadowGradientZoom, outerShadowGradientReverse, outerShadowStyler, outerShadowGradientStyler);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void StylerSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Styler", true, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        StylerEditor.DefaultPresetToggle(useDefaultStylerPreset, preset, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        StylerEditor.PresetField(useDefaultStylerPreset, preset, options: FieldOptions.WithoutSpace());
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    if (preset.objectReferenceValue != null)
                    {
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            StylerEditor.ShapeStyleDropdown(preset, shapeStyleID, "Shape Style ID", addSpace: false, addBackground: false);

                            if (activeShapeStyle != null)
                            {
                                EvoEditorGUI.BeginContainer(nestDepth: 1);
                                EvoEditorGUI.Callout("Properties controlled by this style will appear disabled in the Inspector.",
                                    spaceBefore: 0, spaceAfter: 0);
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
                    }

                    EvoEditorGUI.Property(interactableObject, "Interaction Target", FieldOptions.WithoutSpace().WithInvertedColor());
                    
                    if (interactableObject.objectReferenceValue != null && interactableObject.objectReferenceValue is not IStylerInteractable)
                        EvoEditorGUI.Callout("The selected target does not provide Styler interaction states.", invertColor: true, spaceAfter: 0);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        Styler.ShapeStyleItem GetActiveShapeStyle()
        {
            if (preset.hasMultipleDifferentValues || shapeStyleID.hasMultipleDifferentValues || string.IsNullOrEmpty(shapeStyleID.stringValue))
                return null;

            return preset.objectReferenceValue is StylerPreset stylerPreset &&
                stylerPreset.TryGetShapeStyleItem(shapeStyleID.stringValue, out Styler.ShapeStyleItem shapeStyle) ? shapeStyle : null;
        }

        bool IsControlledByShapeStyle(Styler.ShapeStyleFlags feature) => activeShapeStyle != null && (activeShapeStyle.flags & feature) != 0;

        void ColorSettings(SerializedProperty colorMode, SerializedProperty customColor, SerializedProperty gradient,
            SerializedProperty gradientType, SerializedProperty gradientSpace, SerializedProperty gradientAngle, SerializedProperty gradientZoom,
            SerializedProperty gradientReverse, SerializedProperty stylerStyle, SerializedProperty gradientStyle, bool invertColor = false)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColor));
            {
                EvoEditorGUI.Property(colorMode, "Color Mode", FieldOptions.WithoutSpace().WithoutBackground());
                var mode = (ProceduralShape.ColorMode)colorMode.intValue;
                if (mode == ProceduralShape.ColorMode.Base)
                {
                    EvoEditorGUI.EndColumn();
                    return;
                }

                EvoEditorGUI.BeginContainer();
                {
                    switch (mode)
                    {
                        case ProceduralShape.ColorMode.Custom:
                            StylerEditor.ColorStyle(preset, stylerStyle, interactableObject, !invertColor, customColor);
                            break;

                        case ProceduralShape.ColorMode.Gradient:
                        case ProceduralShape.ColorMode.StylerGradient:
                            if (mode == ProceduralShape.ColorMode.StylerGradient)
                            {
                                StylerEditor.GradientStyle(preset, gradientStyle, interactableObject, !invertColor);
                                EvoEditorGUI.AddLayoutSpace();
                            }
                            else
                                EvoEditorGUI.Property(gradient, "Gradient", FieldOptions.WithInvertedColor(!invertColor));
                            EvoEditorGUI.Property(gradientType, "Type", FieldOptions.WithInvertedColor(!invertColor));
                            EvoEditorGUI.Property(gradientSpace, "Space", FieldOptions.WithInvertedColor(!invertColor));
                            if (gradientType.hasMultipleDifferentValues || gradientType.intValue != (int)ProceduralShape.GradientType.Radial)
                                EvoEditorGUI.Property(gradientAngle, "Angle", FieldOptions.WithInvertedColor(!invertColor));
                            EvoEditorGUI.Property(gradientZoom, "Zoom", FieldOptions.WithInvertedColor(!invertColor));
                            EvoEditorGUI.Toggle(gradientReverse, "Reverse", FieldOptions.WithoutSpace().WithInvertedColor(!invertColor));
                            break;

                        case ProceduralShape.ColorMode.Styler:
                            StylerEditor.ColorStyle(preset, stylerStyle, interactableObject, !invertColor);
                            break;
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndColumn();
        }

        // When synced, Radius Sync supplies the values and only the corner link stays editable
        public static void DrawCornerRadiusControls(SerializedProperty radiusMode, SerializedProperty uniformRadius,
            SerializedProperty cornerRadii, SerializedProperty independentCorners, SerializedProperty squircleCorners,
            bool revertColor = false, bool synced = false)
        {
            bool linkEnabled = GUI.enabled;
            using (new EditorGUI.DisabledScope(synced))
            {
                EvoEditorGUI.Property(radiusMode, "Unit", FieldOptions.WithInvertedColor(revertColor));
                EvoEditorGUI.Toggle(squircleCorners, "Squircle", FieldOptions.WithInvertedColor(revertColor));

                bool isPct = radiusMode.intValue == (int)ProceduralShape.RadiusMode.Percentage;

                EvoEditorGUI.BeginColumn(revertColor ? LayoutOptions.WithInvertedColor() : default);
                DrawCornerRadiusGrid(uniformRadius, cornerRadii, independentCorners, isPct, synced, linkEnabled);
                EvoEditorGUI.EndColumn();
            }
        }

        public static void DrawShapeControls(SerializedProperty shape, SerializedProperty sides, SerializedProperty angle, SerializedProperty skew)
        {
            EvoEditorGUI.AddLayoutSpace();
            if (shape.hasMultipleDifferentValues || shape.intValue == (int)ProceduralShape.ShapeType.Polygon)
                EvoEditorGUI.Property(sides, FieldOptions.WithInvertedColor());
           
            EvoEditorGUI.Property(angle, FieldOptions.WithInvertedColor());
            EvoEditorGUI.Property(skew, "Skew", FieldOptions.WithInvertedColor().WithoutSpace());
        }

        public static void DrawShapeRadiusControls(SerializedProperty shape, SerializedProperty radiusMode,
            SerializedProperty cornerRadius, SerializedProperty independentCorners, SerializedProperty squircleCorners,
            bool revertColor = false, bool synced = false)
        {
            if (shape.intValue == (int)ProceduralShape.ShapeType.Rectangle)
            {
                DrawCornerRadiusControls(radiusMode, cornerRadius, cornerRadius, independentCorners, squircleCorners, revertColor, synced);
                return;
            }

            EvoEditorGUI.Property(radiusMode, "Unit", FieldOptions.WithInvertedColor(revertColor));
            bool isPercentage = radiusMode.intValue == (int)ProceduralShape.RadiusMode.Percentage;
            EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor(revertColor));
            EvoEditorGUI.AddPropertySpace();
            DrawUniformRadius(cornerRadius, isPercentage);
            EvoEditorGUI.EndRow();
        }

        static void DrawUniformRadius(SerializedProperty radius, bool isPct)
        {
            GUIContent label = new(isPct ? "Radius (%)" : "Radius (px)", radius.tooltip);
            Rect rect = EditorGUILayout.GetControlRect();

            using var propertyScope = new EditorGUI.PropertyScope(rect, label, radius);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = radius.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            float value = GetUniformRadius(radius);
            value = isPct
                ? EditorGUI.Slider(rect, propertyScope.content, value, 0f, 100f)
                : EditorGUI.FloatField(rect, propertyScope.content, value);

            if (EditorGUI.EndChangeCheck())
                SetUniformRadius(radius, ConstrainRadius(value, isPct));

            EditorGUI.showMixedValue = previousMixedValue;
        }

        static void DrawCornerRadiusGrid(SerializedProperty uniformRadius, SerializedProperty cornerRadii,
            SerializedProperty independentCorners, bool isPct, bool synced, bool linkEnabled)
        {
            EditorGUILayout.Space(6);

            Rect area = GUILayoutUtility.GetRect(0f, 48f);
            float cx = area.center.x;
            float cy = area.center.y;

            const float minFieldWidth = 50f;
            const float maxFieldWidth = 60f;
            const float iconWidth = 24f;
            const float centerGap = 28f;
            const float rowGap = 4f;
            float fieldWidth = Mathf.Clamp((area.width - iconWidth * 2f - centerGap) * 0.5f, minFieldWidth, maxFieldWidth);
            float fieldHeight = EditorGUIUtility.singleLineHeight;
            float halfCenterGap = centerGap * 0.5f;

            // Top Left [Icon] [Field]
            Rect topLeftField = new(cx - halfCenterGap - fieldWidth, cy - rowGap - fieldHeight, fieldWidth, fieldHeight);
            Rect topLeftIcon = new(topLeftField.x - iconWidth, topLeftField.y, iconWidth, fieldHeight);

            // Bottom Left [Icon] [Field]
            Rect bottomLeftField = new(cx - halfCenterGap - fieldWidth, cy + rowGap, fieldWidth, fieldHeight);
            Rect bottomLeftIcon = new(bottomLeftField.x - iconWidth, bottomLeftField.y, iconWidth, fieldHeight);

            // Top Right [Field] [Icon]
            Rect topRightField = new(cx + halfCenterGap, cy - rowGap - fieldHeight, fieldWidth, fieldHeight);
            Rect topRightIcon = new(topRightField.xMax, topRightField.y, iconWidth, fieldHeight);

            // Bottom Right [Field] [Icon]
            Rect bottomRightField = new(cx + halfCenterGap, cy + rowGap, fieldWidth, fieldHeight);
            Rect bottomRightIcon = new(bottomRightField.xMax, bottomRightField.y, iconWidth, fieldHeight);

            bool linked = !independentCorners.boolValue;

            // Vector4 mapping: X: TL, Y: TR, Z: BR, W: BL
            DraggableCorner(topLeftField, topLeftIcon, uniformRadius, cornerRadii, 0, isPct, linked);
            DraggableCorner(topRightField, topRightIcon, uniformRadius, cornerRadii, 1, isPct, linked);
            DraggableCorner(bottomLeftField, bottomLeftIcon, uniformRadius, cornerRadii, 3, isPct, linked);
            DraggableCorner(bottomRightField, bottomRightIcon, uniformRadius, cornerRadii, 2, isPct, linked);

            // Synced values stay locked, but the link still decides how corners fit the source
            bool enabled = GUI.enabled;
            GUI.enabled = linkEnabled;
            DrawCornerLinkButton(new Rect(cx - 10f, cy - 10f, 20f, 20f), uniformRadius, cornerRadii, independentCorners, isPct, synced);
            GUI.enabled = enabled;

            EditorGUILayout.Space(6);
        }

        static void DrawCornerLinkButton(Rect rect, SerializedProperty uniformRadius, SerializedProperty cornerRadii,
            SerializedProperty independentCorners, bool isPct, bool synced)
        {
            bool independent = independentCorners.boolValue;
            bool linked = !independent;
            string tooltip = synced
                ? independent
                    ? "Each synced corner fits its own padding. Click to share one radius."
                    : "Synced corners share one radius. Click to fit each corner to its own padding."
                : independent
                    ? "Corner radii are independent. Click to link them."
                    : "Corner radii are linked. Click to edit them independently.";
            IconDefinition icon = independent ? EditorTools.IconLibrary.Unlink : EditorTools.IconLibrary.Link;
            GUIContent content = new(EvoEditorGUI.GetIcon(icon), tooltip);
            ButtonOptions buttonOptions = ButtonOptions.WithTooltip(tooltip).WithIconSize(15);

            if (linked)
            {
                Color selection = EvoEditorGUI.Palette.Selection;
                buttonOptions = buttonOptions.WithColors(selection, Color.Lerp(selection, Color.white, 0.1f), Color.Lerp(selection, Color.black, 0.1f));
            }

            using (new EditorGUI.PropertyScope(rect, content, independentCorners))
            {
                bool clicked = EvoEditorGUI.Button(rect, string.Empty, icon, buttonOptions);
                if (!clicked)
                    return;

                float value = independent
                    ? ConstrainRadius(cornerRadii.vector4Value.x, isPct)
                    : ConstrainRadius(GetUniformRadius(uniformRadius), isPct);

                independentCorners.boolValue = !independent;
                SetUniformRadius(uniformRadius, value);
                cornerRadii.vector4Value = new Vector4(value, value, value, value);
            }
        }

        static void DraggableCorner(Rect fieldRect, Rect iconRect, SerializedProperty uniformRadius,
            SerializedProperty cornerRadii, int cornerIndex, bool isPct, bool linked)
        {
            EnsureCornerStyles();

            Rect propertyRect = Rect.MinMaxRect(Mathf.Min(fieldRect.xMin, iconRect.xMin), Mathf.Min(fieldRect.yMin, iconRect.yMin),
                Mathf.Max(fieldRect.xMax, iconRect.xMax), Mathf.Max(fieldRect.yMax, iconRect.yMax));
            SerializedProperty displayedRadius = linked ? uniformRadius : cornerRadii;
            GUIContent propertyContent = new("Corner Radius", displayedRadius.tooltip);

            using (new EditorGUI.PropertyScope(propertyRect, propertyContent, displayedRadius))
            {
                DrawDraggableCorner(fieldRect, iconRect, uniformRadius, cornerRadii, cornerIndex, isPct, linked);
            }
        }

        static void DrawDraggableCorner(Rect fieldRect, Rect iconRect, SerializedProperty uniformRadius,
            SerializedProperty cornerRadii, int cornerIndex, bool isPct, bool linked)
        {
            // Setup invisible drag zone
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;

            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.SlideArrow);

            // Fetch the specific Vector4 component value
            Vector4 v = cornerRadii.vector4Value;
            float currentVal = linked ? GetUniformRadius(uniformRadius) : v[cornerIndex];

            // Intercept Drag Logic
            switch (e.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (iconRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = controlID;
                        EditorGUIUtility.SetWantsMouseJumping(1); // Allow continuous dragging outside screen
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        float multiplier = 0.5f;
                        if (e.shift) { multiplier = 2f; } // Hold shift for faster scrub
                        if (e.alt) { multiplier = 0.1f; } // Hold alt for fine scrub

                        float newVal = currentVal + e.delta.x * multiplier;
                        SetCornerRadius(uniformRadius, cornerRadii, ref v, cornerIndex, ConstrainRadius(newVal, isPct), linked);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        EditorGUIUtility.SetWantsMouseJumping(0);
                        e.Use();
                    }
                    break;
            }

            // Draw crop marks inside the icon rect
            if (e.type == EventType.Repaint)
            {
                Color c = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
                if (GUIUtility.hotControl == controlID)
                    c = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f);

                float size = 10f;
                float thick = 1.5f;
                float margin = 6f;
                float cy = iconRect.center.y;

                // Position the icons neatly against their respective float fields
                if (cornerIndex == 0) // TL
                {
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 1) // TR
                {
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin, cy - size / 2, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin + size - thick, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 2) // BR (Z)
                {
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin, cy + size / 2 - thick, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin + size - thick, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 3) // BL (W)
                {
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy + size / 2 - thick, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, thick, size), c);
                }
            }

            // Draw float field manually to catch typing changes
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = (linked ? uniformRadius : cornerRadii).hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float val = EditorGUI.FloatField(fieldRect, GUIContent.none, currentVal, cornerFloatStyle);
            if (EditorGUI.EndChangeCheck())
            {
                SetCornerRadius(uniformRadius, cornerRadii, ref v, cornerIndex,
                    ConstrainRadius(val, isPct), linked);
            }
            EditorGUI.showMixedValue = previousMixedValue;

            // Overlay the symbol visually inside the field
            Rect unitRect = new(fieldRect.xMax - 22, fieldRect.y, 20, fieldRect.height);
            GUI.Label(unitRect, isPct ? "%" : "px", cornerUnitStyle);
        }

        static float ConstrainRadius(float value, bool isPct)
        {
            value = isPct ? Mathf.Clamp(value, 0f, 100f) : Mathf.Max(0f, value);
            return Mathf.Round(value * 10f) * 0.1f;
        }

        static void SetCornerValue(ref Vector4 values, int cornerIndex, float value, bool linked)
        {
            if (linked)
            {
                values = new Vector4(value, value, value, value);
                return;
            }

            values[cornerIndex] = value;
        }

        static void SetCornerRadius(SerializedProperty uniformRadius, SerializedProperty cornerRadii,
            ref Vector4 values, int cornerIndex, float value, bool linked)
        {
            SetCornerValue(ref values, cornerIndex, value, linked);
            cornerRadii.vector4Value = values;

            if (linked)
                SetUniformRadius(uniformRadius, value);
        }

        static float GetUniformRadius(SerializedProperty radius)
        {
            return radius.propertyType == SerializedPropertyType.Vector4 ? radius.vector4Value.x : radius.floatValue;
        }

        static void SetUniformRadius(SerializedProperty radius, float value)
        {
            if (radius.propertyType == SerializedPropertyType.Vector4)
                radius.vector4Value = new Vector4(value, value, value, value);
            else
                radius.floatValue = value;
        }

        static void EnsureCornerStyles()
        {
            if (cornerFloatStyle != null && cornerUnitStyle != null && cornerStylesProSkin == EditorGUIUtility.isProSkin)
                return;

            cornerStylesProSkin = EditorGUIUtility.isProSkin;
            cornerFloatStyle = new GUIStyle(EditorStyles.numberField);
            cornerFloatStyle.padding.right = 20;

            cornerUnitStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            cornerUnitStyle.normal.textColor = cornerStylesProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f);
        }
    }
}