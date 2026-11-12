using UnityEngine;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Corners tab: one radius or four, squircle, and Radius Sync on the inner panel.
    /// The options and corner labels start from the card's own radii.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeCorners : MonoBehaviour
    {
        [SerializeField] private ProceduralShape card;
        [SerializeField] private ProceduralShape inner;
        [Tooltip("Top left, top right, bottom right, bottom left.")]
        [SerializeField] private TMP_Text[] cornerLabels;
        [SerializeField] private TMP_Text info;
        [SerializeField, TextArea] private string syncedInfo;
        [SerializeField, TextArea] private string unsyncedInfo;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup mode;
        [SerializeField] private Slider radius;
        [Tooltip("Top left, top right, bottom right, bottom left.")]
        [SerializeField] private Slider[] corners;
        [SerializeField] private Switch squircle;
        [SerializeField] private Switch radiusSync;

        void Awake()
        {
            Vector4 radii = card.CornerRadius;
            DemoUtils.Show(mode, card.IndependentCorners ? 1 : 0);
            DemoUtils.Show(radius, radii.x);
           
            for (int i = 0; i < corners.Length; i++)
                DemoUtils.Show(corners[i], radii[i]);
            
            DemoUtils.Show(squircle, card.SquircleCorners);
            DemoUtils.Show(radiusSync, Synced);

            ShowRows(card.IndependentCorners);
            ApplySync();
        }

        bool Synced => inner.RadiusSynchronizationMode != ProceduralShape.RadiusSyncMode.None;

        // Each slider sits right under its option row
        void ShowRows(bool independent)
        {
            radius.transform.parent.gameObject.SetActive(!independent);
            foreach (Slider corner in corners)
                corner.transform.parent.gameObject.SetActive(independent);
        }

        public void SetCornerMode(int index)
        {
            // 0: one radius for every corner, 1: a bar per corner, each starting from the shared radius
            bool independent = index == 1;
            if (independent)
            {
                foreach (Slider corner in corners)
                    corner.SetValueWithoutNotify(radius.value);
            }

            card.IndependentCorners = independent;
            inner.IndependentCorners = independent;
            card.CornerRadius = independent
                ? new Vector4(corners[0].value, corners[1].value, corners[2].value, corners[3].value)
                : Vector4.one * radius.value;
          
            ShowRows(independent);
            RefreshCorners();
        }

        public void SetRadius(float value)
        {
            if (card.IndependentCorners)
                return;

            card.CornerRadius = Vector4.one * value;
            RefreshCorners();
        }

        public void SetTopLeft(float value) => SetCorner(0, value);
        public void SetTopRight(float value) => SetCorner(1, value);
        public void SetBottomRight(float value) => SetCorner(2, value);
        public void SetBottomLeft(float value) => SetCorner(3, value);

        void SetCorner(int index, float value)
        {
            if (!card.IndependentCorners)
                return;

            Vector4 radii = card.CornerRadius;
            radii[index] = value;
            card.CornerRadius = radii;
            RefreshCorners();
        }

        public void SetRadiusSync(bool value)
        {
            inner.RadiusSynchronizationMode = value ? ProceduralShape.RadiusSyncMode.MatchParent : ProceduralShape.RadiusSyncMode.None;
            ApplySync();
        }

        void ApplySync()
        {
            inner.outlineStyler.color.stylerID = Synced ? "Accent" : "Negative";
            inner.UpdateRect();
            RefreshCorners();
        }

        void RefreshCorners()
        {
            // GetRadiiPixels returns top-right, bottom-right, top-left, bottom-left; CornerRadius starts at top-left
            Vector4 shaderOrder = card.GetRadiiPixels(card.GetDrawingRect());
            var pixels = new Vector4(shaderOrder.z, shaderOrder.x, shaderOrder.y, shaderOrder.w);

            // Without sync the inner panel copies the parent's radius, which is what looks wrong
            if (!Synced)
            {
                inner.CornerRadiusMode = ProceduralShape.RadiusMode.Pixels;
                inner.CornerRadius = pixels;
            }

            for (int i = 0; i < cornerLabels.Length; i++)
                cornerLabels[i].SetText("{0} px", Mathf.Round(pixels[i]));

            if (info != null)
                info.text = Synced ? syncedInfo : unsyncedInfo;
        }
    }
}