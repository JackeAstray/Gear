using UnityEditor;
using UnityEngine;
using Evo.Tween.Diagnostics;

namespace Evo.Tween.Editor
{
    [CustomEditor(typeof(TweenManager))]
    public sealed class TweenManagerEditor : UnityEditor.Editor
    {
        bool capacitiesExpanded = true;
        bool tweensExpanded = true;

        TweenManager Manager => (TweenManager)target;

        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("EvoTween Manager is created automatically in Play Mode.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                DrawSchedulerSummary();
                EditorGUILayout.Space(4f);
                DrawPoolSummary();
                EditorGUILayout.Space(4f);
                DrawAliveTweens();
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying && Manager != null && Manager.AliveTweenCount > 0;

        void DrawSchedulerSummary()
        {
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.IntField("Alive Tweens", Manager.AliveTweenCount);
            EditorGUILayout.IntField("Active List Capacity", Manager.ActiveTweenCapacity);
        }

        void DrawPoolSummary()
        {
            capacitiesExpanded = EditorGUILayout.Foldout(capacitiesExpanded, "Pools", true);
            if (!capacitiesExpanded)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.IntField("Currently Pooled", Manager.PooledTweenCount);
            EditorGUILayout.IntField("Combined Limit", Manager.PooledTweenCapacity);
            EditorGUILayout.IntField("Tweens Created", Manager.CreatedTweenCount);
            EditorGUILayout.IntField("Pool Reuses", Manager.ReusedTweenCount);
            EditorGUILayout.IntField("Pool Returns", Manager.ReturnedTweenCount);
            EditorGUILayout.IntField("Pool Discards", Manager.DiscardedTweenCount);
            EditorGUI.indentLevel--;
        }

        void DrawAliveTweens()
        {
            int count = Manager.AliveTweenCount;
            tweensExpanded = EditorGUILayout.Foldout(tweensExpanded, $"Alive Tweens ({count})", true);
            if (!tweensExpanded)
                return;

            if (count == 0)
            {
                EditorGUILayout.HelpBox("No tweens are currently scheduled.", MessageType.None);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                TweenDebugInfo info = Manager.GetAliveTween(i);
                if (!info.IsValid)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{i + 1}. {info.Name}", EditorStyles.boldLabel);

                if (info.Target != null)
                    EditorGUILayout.ObjectField("Target", info.Target, typeof(Object), true);

                EditorGUILayout.TextField("State", info.IsPaused ? "Paused" : "Playing");
                EditorGUILayout.EnumPopup("Update Phase", info.UpdatePhase);
                EditorGUILayout.EnumPopup("Time Mode", info.TimeMode);
                EditorGUILayout.FloatField("Speed", info.Speed);

                Rect progressRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(progressRect, info.NormalizedTime, $"{info.Position:0.000}s / {info.Duration:0.000}s");

                if (info.DelayRemaining > 0f)
                    EditorGUILayout.FloatField("Delay Remaining", info.DelayRemaining);

                EditorGUILayout.TextField("Loops", GetLoopDescription(info));

                if (info.PropertyKey != 0L)
                    EditorGUILayout.TextField("Property Key", $"0x{info.PropertyKey:X}");

                EditorGUILayout.EndVertical();
            }
        }

        static string GetLoopDescription(TweenDebugInfo info)
        {
            if (info.Loop == LoopMode.None)
                return "None";

            string total = info.Iterations == 0 ? "Infinite" : info.Iterations.ToString();
            return $"{info.Loop}  ({info.CompletedIterations} / {total})";
        }
    }
}