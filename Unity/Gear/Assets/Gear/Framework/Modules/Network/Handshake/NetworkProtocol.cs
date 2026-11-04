namespace Gear.Framework.Modules
{
    /// <summary>
    /// 网络协议版本 —— 单一事实源。
    ///
    /// <para>
    /// 为什么需要它：加密帧格式（<see cref="EncryptedCodec"/>）与握手负载（<see cref="NetworkHandshake"/>）
    /// 都是**二进制线格式**，改动一次就与旧端不互通。此前只能用"帧内版本字节 + 拒绝"处理不兼容，
    /// 表现为连接建立后业务帧全部静默失败，排查成本极高。现在把版本放在握手第一字节：
    /// 版本不支持时在握手阶段就断开并给出明确日志，而不是让业务层"看起来连上了但什么都不通"。
    /// </para>
    ///
    /// <para>规则：只增不改。新增能力 = 版本 +1；旧版本要么明确拒绝（<see cref="MinSupportedVersion"/> 抬高），
    /// 要么在解码侧做兼容分支。任何情况下都不允许"静默降级"。</para>
    /// </summary>
    public static class NetworkProtocol
    {
        /// <summary>当前协议版本。3 = 版本化握手 + 加密帧序号（防重放）。</summary>
        public const byte Version = 3;

        /// <summary>仍接受的最低版本。低于它的对端在握手阶段被拒绝。</summary>
        public const byte MinSupportedVersion = 3;

        /// <summary>该版本是否可与之通信。</summary>
        public static bool IsSupported(byte version)
        {
            return version >= MinSupportedVersion && version <= Version;
        }

        /// <summary>版本不支持时的统一日志文案（客户端/服务端共用，便于抓包对照）。</summary>
        public static string DescribeMismatch(byte peerVersion)
        {
            return "对端协议版本 " + peerVersion + "，本端支持 [" + MinSupportedVersion + ", " + Version
                + "]；加密帧格式/握手负载不兼容，已在握手阶段断开。请两端同步升级 Gear。";
        }
    }
}
