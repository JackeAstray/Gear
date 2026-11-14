using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Gear.Common;
namespace Gear.Utilities.UI.ImageExtensions
{
    //绘制形状
    public enum DrawShape
    {
        None = 0,           //无
        Circle,             //圆
        Triangle,           //三角形
        Rectangle,          //矩形
        Pentagon,           //五边形
        Hexagon,            //六边形
        ChamferBox,         //倒角长方形
        Quadrilateral,      //四边形（梯形/平行四边形等）
        NStarPolygon,       //N星多边形
        Heart,              //心形
        BlobbyCross,        //滴状十字
        Squircle,           //方圆形 菱形
        NTriangleRounded,   //N三角形圆角
    }

    //渐变类型
    public enum GradientType
    {
        Linear,         //线性
        Corner,         //角
        Radial,         //径向
        Angular         //角向（扇面）
    }

    //材质模式
    public enum MaterialMode
    {
        Dynamic,        //动态
        Shared          //共享
    }

    //角样式类型
    public enum CornerStyleType
    {
        Sharp,          //尖角
        Rounded,        //圆角
        Cropped,        //截断
        BoxCut,         //盒子切割
        CircleCut       //圆形切割
    }

    /// <summary>
    /// ImageEx 的命中检测模式（只影响 Graphic 的点击/悬停，不影响渲染）。
    /// </summary>
    public enum RaycastDetectionMode
    {
        /// <summary>基类行为：矩形范围命中；设置了 Alpha 阈值时再按贴图 Alpha 判定。</summary>
        [InspectorName("标准")]
        Standard = 0,
        /// <summary>按当前 SDF 形状命中：圆角、圆形、星形以外的透明区域不响应。</summary>
        [InspectorName("形状")]
        Shape = 1,
        /// <summary>完全不参与命中（等价于 raycastTarget = false，但仍保留渲染）。</summary>
        [InspectorName("关闭")]
        None = 2
    }

    /// <summary>
    /// 矩形圆角半径的单位。
    /// </summary>
    public enum CornerRadiusMode
    {
        /// <summary>像素值（旧行为，默认）。</summary>
        [InspectorName("像素")]
        Pixels = 0,
        /// <summary>百分比：100 = 短边的一半（胶囊/全圆角）。</summary>
        [InspectorName("百分比")]
        Percentage = 1
    }
}