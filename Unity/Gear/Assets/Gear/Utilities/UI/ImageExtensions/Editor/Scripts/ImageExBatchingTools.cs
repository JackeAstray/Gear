using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageEx 合批工具（编辑器侧）：把"特效设置完全一致"的多个 ImageEx 分到同一组，
    /// 让它们共用一份材质，从而在 Canvas 内合批、减少 draw call 段数。
    /// <para>
    /// 设计说明：
    /// 1. 判定"设置一致"用的是 SerializedObject 逐属性比对（等价于预制体/场景序列化结果），
    ///    只排除"每个实例本来就不同、且不参与材质 uniform"的字段（Sprite / Material / Raycast 事件 / Script）。
    ///    因此组内实例写入共享材质的 uniform 完全相同，不存在 ImageEx Shared 模式的串扰；
    /// 2. 赋值走 SerializedObject 直接写 <c>m_Material</c> 与 <c>materialMode</c>，
    ///    刻意绕开 <c>MaterialMode</c>/<c>material</c> 属性 setter：那两个 setter 在切换 Shared 时会
    ///    回读共享材质并把组件字段改成材质默认值，等于把用户的效果设置清空；
    /// 3. 生成的材质是工程内资产（默认放在 <c>Resources/Materials/UI/ImageExShared</c>），可随工程提交；
    ///    本工具生成的资产名统一带 <c>ImageExShared_</c> 前缀，便于一键清理。
    /// </para>
    /// <para>
    /// 与第三方 UI 插件无关：本实现为 Gear 自研，仅借鉴"按样式分组共用材质"的通用思路。
    /// </para>
    /// </summary>
    internal static class ImageExBatchingTools
    {
        public const string DefaultMaterialFolder = "Assets/Gear/Resources/Materials/UI/ImageExShared";
        public const string GeneratedMaterialPrefix = "ImageExShared_";

        private static readonly string[] s_ignoredPropertyPaths =
        {
            "m_Script",
            "m_Material",
            "materialMode",
            "m_Sprite",
            "m_OverrideSprite",
            "m_OnCullStateChanged",
        };

        // ============================================================
        // 分组
        // ============================================================

        /// <summary>按"材质可见设置完全一致"把实例分组；每组至少 1 个，组内成员可安全共用一份材质。</summary>
        public static List<List<ImageEx>> GroupBySettings(IList<ImageEx> images)
        {
            List<List<ImageEx>> groups = new List<List<ImageEx>>();
            foreach (ImageEx image in images)
            {
                if (image == null) continue;

                bool merged = false;
                foreach (List<ImageEx> group in groups)
                {
                    if (SameSettings(group[0], image))
                    {
                        group.Add(image);
                        merged = true;
                        break;
                    }
                }

                if (!merged) groups.Add(new List<ImageEx> { image });
            }

            return groups;
        }

        /// <summary>逐 SerializedProperty 比对两个 ImageEx 的材质相关设置。</summary>
        public static bool SameSettings(ImageEx a, ImageEx b)
        {
            if (a == null || b == null) return a == b;

            SerializedObject objectA = new SerializedObject(a);
            SerializedObject objectB = new SerializedObject(b);

            SerializedProperty propertyA = objectA.GetIterator();
            SerializedProperty propertyB = objectB.GetIterator();

            while (propertyA.NextVisible(true) && propertyB.NextVisible(true))
            {
                if (IsIgnoredProperty(propertyA.propertyPath)) continue;
                if (!SameValue(propertyA, propertyB)) return false;
            }

            return true;
        }

        private static bool SameValue(SerializedProperty a, SerializedProperty b)
        {
            if (a.propertyType != b.propertyType) return false;

            switch (a.propertyType)
            {
                case SerializedPropertyType.Generic: return true; // 子属性会在下一次 NextVisible(true) 中逐个比较
                case SerializedPropertyType.Integer: return a.intValue == b.intValue;
                case SerializedPropertyType.Boolean: return a.boolValue == b.boolValue;
                case SerializedPropertyType.Float: return Mathf.Approximately(a.floatValue, b.floatValue);
                case SerializedPropertyType.String: return a.stringValue == b.stringValue;
                case SerializedPropertyType.Color: return a.colorValue == b.colorValue;
                case SerializedPropertyType.Enum: return a.enumValueIndex == b.enumValueIndex;
                case SerializedPropertyType.Vector2: return a.vector2Value == b.vector2Value;
                case SerializedPropertyType.Vector3: return a.vector3Value == b.vector3Value;
                case SerializedPropertyType.Vector4: return a.vector4Value == b.vector4Value;
                case SerializedPropertyType.Quaternion: return a.quaternionValue == b.quaternionValue;
                case SerializedPropertyType.Rect: return a.rectValue == b.rectValue;
                case SerializedPropertyType.Bounds: return a.boundsValue == b.boundsValue;
                case SerializedPropertyType.Vector2Int: return a.vector2IntValue == b.vector2IntValue;
                case SerializedPropertyType.Vector3Int: return a.vector3IntValue == b.vector3IntValue;
                case SerializedPropertyType.Gradient: return SameGradient(a.gradientValue, b.gradientValue);
                case SerializedPropertyType.AnimationCurve: return SameCurve(a.animationCurveValue, b.animationCurveValue);
                case SerializedPropertyType.ObjectReference:
                    return SameObjectReference(a.objectReferenceValue, b.objectReferenceValue);
                case SerializedPropertyType.LayerMask: return a.intValue == b.intValue;
                case SerializedPropertyType.ArraySize: return a.intValue == b.intValue;
                case SerializedPropertyType.Character: return a.intValue == b.intValue;
                default:
                    // 未知类型：保守起见判为不同，宁可不合并
                    return false;
            }
        }

        private static bool SameGradient(Gradient a, Gradient b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.mode != b.mode) return false;

            GradientColorKey[] colorKeysA = a.colorKeys;
            GradientColorKey[] colorKeysB = b.colorKeys;
            if (colorKeysA.Length != colorKeysB.Length) return false;
            for (int i = 0; i < colorKeysA.Length; i++)
            {
                if (!Mathf.Approximately(colorKeysA[i].time, colorKeysB[i].time)) return false;
                if (colorKeysA[i].color != colorKeysB[i].color) return false;
            }

            GradientAlphaKey[] alphaKeysA = a.alphaKeys;
            GradientAlphaKey[] alphaKeysB = b.alphaKeys;
            if (alphaKeysA.Length != alphaKeysB.Length) return false;
            for (int i = 0; i < alphaKeysA.Length; i++)
            {
                if (!Mathf.Approximately(alphaKeysA[i].time, alphaKeysB[i].time)) return false;
                if (!Mathf.Approximately(alphaKeysA[i].alpha, alphaKeysB[i].alpha)) return false;
            }

            return true;
        }

        private static bool SameCurve(AnimationCurve a, AnimationCurve b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.preWrapMode != b.preWrapMode || a.postWrapMode != b.postWrapMode) return false;

            Keyframe[] keysA = a.keys;
            Keyframe[] keysB = b.keys;
            if (keysA.Length != keysB.Length) return false;
            for (int i = 0; i < keysA.Length; i++)
            {
                if (!Mathf.Approximately(keysA[i].time, keysB[i].time)) return false;
                if (!Mathf.Approximately(keysA[i].value, keysB[i].value)) return false;
                if (!Mathf.Approximately(keysA[i].inTangent, keysB[i].inTangent)) return false;
                if (!Mathf.Approximately(keysA[i].outTangent, keysB[i].outTangent)) return false;
            }

            return true;
        }

        private static bool SameObjectReference(Object a, Object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.GetInstanceID() == b.GetInstanceID();
        }

        private static bool IsIgnoredProperty(string propertyPath)
        {
            foreach (string ignored in s_ignoredPropertyPaths)
            {
                if (propertyPath.Equals(ignored, System.StringComparison.Ordinal)) return true;
            }

            // 子路径（如 m_Sprite.xxx）不应出现；这里只做前缀兜底
            return propertyPath.StartsWith("m_OnCullStateChanged");
        }

        // ============================================================
        // 应用 / 还原 / 清理
        // ============================================================

        /// <summary>
        /// 为选中实例生成并按组分配共享材质。返回实际被设置的实例数；<paramref name="groupCount"/> 为组数。
        /// </summary>
        public static int ApplySharedMaterials(IList<ImageEx> images, string folder, out int groupCount)
        {
            groupCount = 0;
            Shader shader = ResolveShader();
            if (shader == null)
            {
                Debug.LogError("[ImageEx 合批] 未找到 ImageEx 着色器，已取消。");
                return 0;
            }

            EnsureFolder(folder);

            List<List<ImageEx>> groups = GroupBySettings(images);
            groupCount = groups.Count;

            int applied = 0;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                string assetPath = $"{folder}/{GeneratedMaterialPrefix}{groupIndex:00}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null || material.shader != shader)
                {
                    material = new Material(shader) { name = GeneratedMaterialPrefix + groupIndex.ToString("00") };
                    AssetDatabase.CreateAsset(material, assetPath);
                }

                foreach (ImageEx image in groups[groupIndex])
                {
                    AssignSharedMaterial(image, material);
                    applied++;
                }
            }

            AssetDatabase.SaveAssets();
            return applied;
        }

        /// <summary>把单个实例切到 Shared 模式并挂上指定材质（不触发回读，见类注释）。</summary>
        public static void AssignSharedMaterial(ImageEx image, Material material)
        {
            if (image == null || material == null) return;

            Undo.RecordObject(image, "分配 ImageEx 共享材质");
            SerializedObject serialized = new SerializedObject(image);
            SerializedProperty materialProperty = serialized.FindProperty("m_Material");
            SerializedProperty modeProperty = serialized.FindProperty("materialMode");

            if (materialProperty != null) materialProperty.objectReferenceValue = material;
            if (modeProperty != null) modeProperty.enumValueIndex = (int)MaterialMode.Shared;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            image.SetMaterialDirty();
            UnityEditor.EditorUtility.SetDirty(image);

            if (materialProperty == null || modeProperty == null)
            {
                Debug.LogWarning("[ImageEx 合批] 未找到 m_Material / materialMode 序列化字段，请确认 ImageEx 版本。", image);
            }
        }

        /// <summary>还原为 Dynamic（保留材质引用但不再使用，便于再次切回）。</summary>
        public static void RestoreDynamic(IList<ImageEx> images)
        {
            foreach (ImageEx image in images)
            {
                if (image == null) continue;

                Undo.RecordObject(image, "还原 ImageEx 为 Dynamic");
                SerializedObject serialized = new SerializedObject(image);
                SerializedProperty modeProperty = serialized.FindProperty("materialMode");
                SerializedProperty materialProperty = serialized.FindProperty("m_Material");

                if (modeProperty != null) modeProperty.enumValueIndex = (int)MaterialMode.Dynamic;
                if (materialProperty != null) materialProperty.objectReferenceValue = null;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                image.SetMaterialDirty();
                UnityEditor.EditorUtility.SetDirty(image);
            }
        }

        /// <summary>删除本工具在指定目录生成的材质资产（只删 <c>ImageExShared_</c> 前缀）。返回删除数量。</summary>
        public static int DeleteGeneratedMaterials(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;

            int deleted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).StartsWith(GeneratedMaterialPrefix)) continue;
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }

            return deleted;
        }

        // ============================================================
        // 材质 / 着色器
        // ============================================================

        private static Shader ResolveShader()
        {
            Shader shader = Shader.Find("ReunionMovement/UI/ImageEx");
            if (shader != null) return shader;

            shader = Shader.Find("Gear/UI/ImageEx");
            if (shader != null) return shader;

            return Resources.Load<Shader>("Shader/UI/ImageEx/ImageEx");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;

            EnsureFolder(parent);
            if (AssetDatabase.IsValidFolder(folder)) return;
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
