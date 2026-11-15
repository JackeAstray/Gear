using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 渲染预算探针：采样 Batches / SetPass Calls，并统计当前 Canvas 下唯一材质数。
    /// 只读诊断，不改变渲染。
    /// </summary>
    [AddComponentMenu("UI/Gear/Render Budget Probe")]
    [DisallowMultipleComponent]
    public sealed class GearRenderBudgetProbe : MonoBehaviour
    {
        [SerializeField, Min(1)] private int sampleWindow = 60;
        [SerializeField] private Canvas canvas;

        private ProfilerRecorder batches;
        private ProfilerRecorder setPassCalls;
        private long batchSum;
        private long setPassSum;
        private int samples;

        public bool IsValid => batches.Valid || setPassCalls.Valid;
        public float AverageBatches => samples > 0 ? batchSum / (float)samples : 0f;
        public float AverageSetPassCalls => samples > 0 ? setPassSum / (float)samples : 0f;
        public int Samples => samples;

        private void OnEnable()
        {
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches", 1);
            setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls", 1);
            batchSum = 0;
            setPassSum = 0;
            samples = 0;
        }

        private void OnDisable()
        {
            if (batches.Valid) batches.Dispose();
            if (setPassCalls.Valid) setPassCalls.Dispose();
        }

        private void Update()
        {
            if (samples >= Mathf.Max(1, sampleWindow)) return;
            if (batches.Valid) batchSum += batches.LastValue;
            if (setPassCalls.Valid) setPassSum += setPassCalls.LastValue;
            samples++;
        }

        public int UniqueMaterials()
        {
            Canvas target = canvas != null ? canvas : GetComponentInParent<Canvas>();
            Graphic[] graphics = target != null
                ? target.GetComponentsInChildren<Graphic>(true)
                : GetComponentsInChildren<Graphic>(true);

            var ids = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i].material != null)
                    ids.Add(graphics[i].material.GetInstanceID());
            }

            return ids.Count;
        }

        public string Report()
        {
            var builder = new StringBuilder();
            builder.Append("valid=").Append(IsValid)
                .Append(" samples=").Append(Samples)
                .Append(" batches=").Append(AverageBatches.ToString("0.0"))
                .Append(" setPass=").Append(AverageSetPassCalls.ToString("0.0"))
                .Append(" materials=").Append(UniqueMaterials());
            return builder.ToString();
        }
    }
}
