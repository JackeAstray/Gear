using System.Collections.Generic;
using UnityEngine;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules.Http
{
    /// <summary>
    /// 一次 HTTP 请求的结果快照（成功与失败共用同一类型，用 <see cref="IsSuccessful"/> 区分）。
    /// <para>
    /// 命名说明：这里全部是普通 C# 属性，不是 Unity 序列化字段，因此按 .NET 约定用 PascalCase。
    /// 2.0 之前是 camelCase（`url` / `isSuccessful` / …），照搬 Unity 序列化字段习惯，
    /// 容易让调用方误以为在写 Unity API。
    /// </para>
    /// </summary>
    public class HttpResponse
    {
        /// <summary>请求地址。</summary>
        public string Url { get; set; }

        /// <summary>是否拿到 2xx/3xx 成功响应。</summary>
        public bool IsSuccessful { get; set; }

        /// <summary>是否 HTTP 层错误（4xx/5xx）。</summary>
        public bool IsHttpError { get; set; }

        /// <summary>是否网络层错误（DNS/连接/超时）。</summary>
        public bool IsNetworkError { get; set; }

        /// <summary>HTTP 状态码；请求未到达服务器时为 0。</summary>
        public long StatusCode { get; set; }

        /// <summary>原始响应字节（下载/图片请求用）。</summary>
        public byte[] Bytes { get; set; }

        /// <summary>按 UTF-8 解码后的文本（文本请求用）。</summary>
        public string Text { get; set; }

        /// <summary>失败原因（成功时为空）。</summary>
        public string Error { get; set; }

        /// <summary>解码出的纹理（图片请求用，调用方负责释放）。</summary>
        public Texture2D Texture { get; set; }

        /// <summary>响应头（键为服务器返回的原始大小写）。</summary>
        public Dictionary<string, string> ResponseHeaders { get; set; }
    }
}
