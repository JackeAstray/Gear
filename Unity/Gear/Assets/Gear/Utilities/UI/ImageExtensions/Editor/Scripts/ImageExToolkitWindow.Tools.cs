using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    public sealed partial class ImageExToolkitWindow
    {
        private void DrawDiagnoseTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("分析选中")) sceneImages = null;
                if (GUILayout.Button("分析整个场景"))
                {
                    sceneImages = UnityEngine.Object.FindObjectsByType<ImageEx>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                }
                if (GUILayout.Button("全选场景 ImageEx")) SelectAllInScene();
            }

            List<ImageEx> scope = sceneImages != null ? new List<ImageEx>(sceneImages) : GetSelectedImageEx();
            if (scope.Count == 0)
            {
                EditorGUILayout.HelpBox("选中场景里的 ImageEx 后，这里会给出材质模式、效果启用与合批估算。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(sceneImages != null ? "范围：整个场景" : "范围：当前选中", EditorStyles.boldLabel);
            DrawStats(scope);
        }
    }
}
