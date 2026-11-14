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
        Dynamic,        //动态（每实例独立材质）
        Shared,         //共享（手动指定的共享材质）
        Pooled          //按设置签名共享（同设置实例自动合批）
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
    /// DotMatrix 的解析图案（由 uv/格子索引 + 时间直接算出，不需要纹理）。
    /// </summary>
    public enum DotMatrixPattern
    {
        [InspectorName("波浪")] Wave = 0,
        [InspectorName("同心圆")] Rings = 1,
        [InspectorName("雷达")] Radar = 2,
        [InspectorName("螺旋")] Spiral = 3,
        [InspectorName("蛇形")] Snake = 4,
        [InspectorName("均衡器")] Equalizer = 5,
        [InspectorName("棋盘")] Checker = 6,
        [InspectorName("脉冲")] Pulse = 7,
    }

    /// <summary>DotMatrix 的点形状。</summary>
    public enum DotMatrixShape
    {
        [InspectorName("圆点")] Circle = 0,
        [InspectorName("方块")] Square = 1,
        [InspectorName("菱形")] Diamond = 2,
    }

    /// <summary>
    /// 图片填充方式：Stretch 拉伸、Cover 裁切填满、Fit 完整显示（留边）。
    /// <para>
    /// 旧资源的 <c>preserveAspect = true</c> 仍按 Fit 处理；Cover 会覆盖 preserveAspect。
    /// </para>
    /// </summary>
    public enum SpriteFillMode
    {
        [InspectorName("拉伸")]
        Stretch = 0,
        [InspectorName("裁切填满")]
        Cover = 1,
        [InspectorName("完整显示")]
        Fit = 2,
    }

    /// <summary>
    /// 矩形圆角的同步来源：让嵌套卡片/胶囊按钮的圆角随父级或目标一起变化。
    /// </summary>
    public enum CornerRadiusSyncMode
    {
        /// <summary>使用自身 <see cref="RectangleImg.CornerRadius"/>（旧行为）。</summary>
        [InspectorName("不同步")]
        None = 0,
        /// <summary>取父节点 ImageEx 的像素圆角。</summary>
        [InspectorName("跟随父级")]
        MatchParent = 1,
        /// <summary>取 <see cref="RectangleImg.SyncTarget"/> 上 ImageEx 的像素圆角。</summary>
        [InspectorName("跟随目标")]
        MatchTarget = 2,
        /// <summary>向上找最近一个矩形 ImageEx 的像素圆角。</summary>
        [InspectorName("跟随最近祖先")]
        MatchAncestor = 3,
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