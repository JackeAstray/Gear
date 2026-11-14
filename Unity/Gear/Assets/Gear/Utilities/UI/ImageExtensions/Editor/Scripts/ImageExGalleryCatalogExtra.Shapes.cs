using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    internal static partial class ImageExGalleryCatalogExtra
    {
        private static void SetupShape(ImageEx p, DrawShape shape)
        {
            p.DrawShape = shape;
            switch (shape)
            {
                case DrawShape.Circle:
                    p.Circle = new CircleImg { FitToRect = true };
                    break;
                case DrawShape.Triangle:
                    p.Triangle = new TriangleImg { CornerRadius = new Vector3(0.1f, 0.1f, 0.1f) };
                    break;
                case DrawShape.Rectangle:
                    p.Rectangle = new RectangleImg { CornerRadius = new Vector4(26f, 26f, 26f, 26f) };
                    break;
                case DrawShape.Pentagon:
                    p.Pentagon = new PentagonImg
                    {
                        CornerRadius = new Vector4(0.06f, 0.06f, 0.06f, 0.06f),
                        TipSize = 70f,
                    };
                    break;
                case DrawShape.Hexagon:
                    p.Hexagon = new HexagonImg
                    {
                        CornerRadius = new Vector4(1f, 1f, 1f, 1f),
                        TipSize = new Vector2(50f, 50f),
                    };
                    break;
                case DrawShape.ChamferBox:
                    p.ChamferBox = new ChamferBoxImg
                    {
                        ChamferBoxSize = new Vector2(1.5f, 1f),
                        ChamferBoxRadius = new Vector4(0.12f, 0.12f, 0.12f, 0.12f),
                    };
                    break;
                case DrawShape.Quadrilateral:
                    p.Quadrilateral = new QuadrilateralImg
                    {
                        TopLeftOffset = new Vector2(0.16f, 0f),
                        TopRightOffset = new Vector2(0f, 0.16f),
                        BottomLeftOffset = new Vector2(0f, 0.16f),
                        BottomRightOffset = new Vector2(0.16f, 0f),
                    };
                    break;
                case DrawShape.NStarPolygon:
                    p.NStarPolygon = new NStarPolygonImg { SideCount = 5f, Inset = 2f, CornerRadius = 0.04f };
                    break;
                case DrawShape.BlobbyCross:
                    p.BlobbyCross = new BlobbyCrossImg { BlobbyCrossTime = 0.35f };
                    break;
                case DrawShape.Squircle:
                    p.Squircle = new SquircleImg { SquircleTime = 0.35f };
                    break;
                case DrawShape.NTriangleRounded:
                    p.NTriangleRounded = new NTriangleRoundedImg { NTriangleRoundedNumber = 3f };
                    break;
            }
        }
    }
}
