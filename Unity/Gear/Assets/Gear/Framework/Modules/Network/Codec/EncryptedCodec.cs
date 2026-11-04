using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules
{
    /// <summary>
    /// 加密编解码器包装器 —— 包装底层编解码器，对负载进行 AES-256-CBC 加密 + HMAC-SHA256 完整性保护
    /// （Encrypt-then-MAC）。
    /// 帧格式：[1B 版本][4B LE 序号][16B 随机 IV][AES-CBC 密文（PKCS7）][32B HMAC-SHA256]。
    /// MAC 覆盖 [LE16 消息 ID][版本][序号][IV][密文]。
    ///
    /// <para>
    /// 序号（v3 新增）用于防重放：接收端只接受严格大于已见最大序号的帧，因此抓包得到的
    /// 合法加密帧无法被原样重发（旧格式没有序号，攻击者可在会话内重放任意业务帧，
    /// 例如重复触发一次"购买"）。
    /// </para>
    ///
    /// 与纯 CBC 相比：篡改/填充 oracle 探测在解密前即被 MAC 校验拒绝；每包随机 IV 避免同明文同密文。
    /// 解密失败（密钥不符/数据损坏/被篡改/重放）时 TryDecode 返回 false，不影响主循环。
    ///
    /// 用法（两端密钥必须一致）：
    ///   var codec = EncryptedCodec.Wrap(NetworkCodecFactory.Create(NetworkCodecType.MessageId), keyBytes32);
    /// 注意：加解密有 CPU 成本（每包创建 Aes/HMAC 实例，收发线程独立互不共享），
    /// 建议仅对敏感通道整链路启用，而非高频状态同步。
    /// </summary>
    public sealed class EncryptedCodec : INetworkMessageCodec
    {
        // 版本 3：新增 [LE32 序号] 并纳入 MAC，接收侧拒绝 seq <= 已见最大值的帧（防重放/乱序）。
        // 版本 2（无序号）不再被接受：序号缺失意味着无法区分"重放的旧帧"与"新帧"，
        // 保持兼容就等于保留该攻击面。线格式变更只允许在新会话中协商，见 NetworkProtocol。
        const byte FormatVersion = NetworkProtocol.Version;
        const int SequenceLength = 4;
        const int IvLength = 16;
        const int MacLength = 32;

        readonly INetworkMessageCodec inner;

        /// <summary>发送序号（每帧 +1，从 1 开始；0 保留给"尚未发送"）。</summary>
        int sendSequence;

        /// <summary>已接受的最大接收序号（严格单调，用于拒绝重放帧）。</summary>
        int lastReceiveSequence;

        // 发送方向与接收方向各自独立的密钥：不区分方向时四者相同（兼容旧用法），
        // 区分方向时客户端发送密钥 == 服务端接收密钥，反之亦然。
        // 这样才能阻断"把对端自己发出的帧原样反射回去"的重放/反射攻击。
        readonly byte[] encKeySend;
        readonly byte[] encKeyRecv;
        readonly byte[] macKeySend;
        readonly byte[] macKeyRecv;

        /// <summary>不区分方向（兼容用法：单元测试、非对称场景之外的自定义链路）</summary>
        const byte NoDirection = 0xFF;

        /// <summary>
        /// MAC 输入中 [LE16 消息 ID] 的暂存。用 [ThreadStatic] 而非实例字段：
        /// 同一 codec 实例的 Encode（发送线程）与 Decrypt（接收线程）可能并发。
        /// </summary>
        [ThreadStatic] static byte[] macMessageIdScratch;

        public bool SupportsStreamFraming => inner.SupportsStreamFraming;

        /// <summary>
        /// 包装底层编解码器（不区分方向）。key 长度必须为 16/24/32 字节（AES-128/192/256）。
        /// 生产链路请优先使用 <see cref="WrapClient"/>/<see cref="WrapServer"/> 以启用方向绑定。
        /// </summary>
        public static EncryptedCodec Wrap(INetworkMessageCodec inner, byte[] key)
        {
            return new EncryptedCodec(inner, key, NoDirection);
        }

        /// <summary>按"客户端"角色包装：发送用方向 0，接收期望方向 1（服务端发送）。</summary>
        public static EncryptedCodec WrapClient(INetworkMessageCodec inner, byte[] key)
        {
            return new EncryptedCodec(inner, key, 0x00);
        }

        /// <summary>按"服务端"角色包装：发送用方向 1，接收期望方向 0（客户端发送）。</summary>
        public static EncryptedCodec WrapServer(INetworkMessageCodec inner, byte[] key)
        {
            return new EncryptedCodec(inner, key, 0x01);
        }

        public EncryptedCodec(INetworkMessageCodec inner, byte[] key)
            : this(inner, key, NoDirection)
        {
        }

        /// <param name="direction">本端角色：0=客户端，1=服务端，<see cref="NoDirection"/>=不区分方向</param>
        public EncryptedCodec(INetworkMessageCodec inner, byte[] key, byte direction)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (key == null || (key.Length != 16 && key.Length != 24 && key.Length != 32))
            {
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            }
            // 由主密钥派生加密密钥与 MAC 密钥（HMAC-SHA256 标签分离），避免同密钥双重用途。
            // 再叠加方向字节：本端发送用 direction，本端接收用 1-direction（对端视角），
            // 于是"客户端发送密钥 == 服务端接收密钥"，而"本端发送密钥 != 本端接收密钥" ——
            // 反射帧因此在接收侧 MAC 校验失败。
            using (var sha = SHA256.Create())
            {
                if (direction == NoDirection)
                {
                    var enc = sha.ComputeHash(Combine(key, new byte[] { 0x01 }));
                    var mac = sha.ComputeHash(Combine(key, new byte[] { 0x02 }));
                    encKeySend = encKeyRecv = enc;
                    macKeySend = macKeyRecv = mac;
                }
                else
                {
                    byte peer = (byte)(1 - (direction & 1));
                    encKeySend = sha.ComputeHash(Combine(key, new byte[] { 0x01, direction }));
                    encKeyRecv = sha.ComputeHash(Combine(key, new byte[] { 0x01, peer }));
                    macKeySend = sha.ComputeHash(Combine(key, new byte[] { 0x02, direction }));
                    macKeyRecv = sha.ComputeHash(Combine(key, new byte[] { 0x02, peer }));
                }
            }
        }

        static byte[] Combine(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        public byte[] Encode(ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var encrypted = Encrypt(payload, messageId);
            return inner.Encode(messageId, encrypted);
        }

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            return inner.TryGetFrameLength(buffer, offset, count, out frameLength);
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            if (!inner.TryDecode(frame, offset, length, out messageId, out var encrypted))
            {
                payload = default;
                return false;
            }
            try
            {
                payload = new ArraySegment<byte>(Decrypt(encrypted, messageId));
                return true;
            }
            catch (Exception ex)
            {
                // 坏帧可以持续发送（重放/探测），逐帧打 Warning 本身就是可用性风险：
                // 前 5 次全量记录用于定位，之后每 100 次记一条，避免日志刷屏掩盖真正的故障。
                int failures = Interlocked.Increment(ref decodeFailureCount);
                if (failures <= 5 || failures % 100 == 0)
                {
                    Log.Warning("[EncryptedCodec] 解密/完整性校验失败（密钥不符、被篡改或重放）累计 {0} 次，最近一次: {1}",
                        failures, ex.Message);
                }
                payload = default;
                return false;
            }
        }

        /// <summary>累计解密失败次数（仅用于日志节流，不参与协议判断）。</summary>
        static int decodeFailureCount;

        /// <summary>
        /// 计算 [LE16 消息 ID][版本][序号][IV][密文] 的 HMAC-SHA256。
        /// 必须把内层编解码器写入的 messageId 纳入 MAC：它位于加密负载之外，
        /// 若不覆盖，中间人就能在 MAC 仍然有效的前提下翻转消息 ID，
        /// 让业务帧被当作 PING/ACK/握手指令等系统帧重新派发。
        /// 版本与序号同样在 <paramref name="buffer"/> 覆盖范围内，因此改动会直接导致 MAC 失败。
        /// </summary>
        static byte[] ComputeMac(HMACSHA256 hmac, ushort messageId, byte[] buffer, int offset, int count)
        {
            var idBytes = macMessageIdScratch ?? (macMessageIdScratch = new byte[2]);
            idBytes[0] = (byte)messageId;
            idBytes[1] = (byte)(messageId >> 8);
            hmac.TransformBlock(idBytes, 0, 2, null, 0);
            hmac.TransformFinalBlock(buffer, offset, count);
            return hmac.Hash;
        }

        byte[] Encrypt(byte[] data, ushort messageId)
        {
            // 序号在发送前自增：即使调用方并发调用 Encode（理论上不该发生，但网络层不保证），
            // Interlocked 也能保证不会出现"两帧同号"（同号会让接收端误杀后一帧）。
            int sequence = Interlocked.Increment(ref sendSequence);

            using var aes = Aes.Create();
            aes.Key = encKeySend;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV(); // 每包随机 IV：同明文产生不同密文

            byte[] cipher;
            using (var encryptor = aes.CreateEncryptor())
            {
                cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
            }

            const int headerLength = 1 + SequenceLength;
            int bodyLength = headerLength + IvLength + cipher.Length;
            var result = new byte[bodyLength + MacLength];
            result[0] = FormatVersion;
            result[1] = (byte)sequence;
            result[2] = (byte)(sequence >> 8);
            result[3] = (byte)(sequence >> 16);
            result[4] = (byte)(sequence >> 24);
            Buffer.BlockCopy(aes.IV, 0, result, headerLength, IvLength);
            Buffer.BlockCopy(cipher, 0, result, headerLength + IvLength, cipher.Length);
            using (var hmac = new HMACSHA256(macKeySend))
            {
                var mac = ComputeMac(hmac, messageId, result, 0, bodyLength);
                Buffer.BlockCopy(mac, 0, result, bodyLength, MacLength);
            }
            return result;
        }

        byte[] Decrypt(ArraySegment<byte> data, ushort messageId)
        {
            const int headerLength = 1 + SequenceLength;
            if (data.Count < headerLength + IvLength + 16 + MacLength)
            {
                throw new InvalidDataException("加密帧长度不足");
            }
            var arr = data.Array;
            int off = data.Offset;
            int cipherLen = data.Count - headerLength - IvLength - MacLength;

            // Encrypt-then-MAC：先校验完整性（常量时间比较），任何篡改在解密前即被拒绝，
            // 堵住填充 oracle 探测路径
            using (var hmac = new HMACSHA256(macKeyRecv))
            {
                var expected = ComputeMac(hmac, messageId, arr, off, headerLength + IvLength + cipherLen);
                int macOff = off + headerLength + IvLength + cipherLen;
                if (!FixedTimeEquals(expected, arr, macOff, MacLength))
                {
                    throw new InvalidDataException("MAC 校验失败（数据被篡改）");
                }
            }

            // 版本校验放在 MAC 之后：版本字节本身已在 MAC 覆盖内，
            // 先确认完整性再解释内容，避免降级/版本混淆攻击
            if (arr[off] != FormatVersion)
            {
                throw new InvalidDataException("不支持的加密帧版本: " + arr[off] + "（期望 " + FormatVersion + "）");
            }

            // 序号同样在 MAC 覆盖内，到这里已确认未被篡改；再判重放。
            // 严格单调即可：四传输（TCP/WebSocket/KCP 可靠流/RawTcp）都按序交付，
            // 出现 seq <= 已见最大值只能是重放或伪造。
            int sequence = arr[off + 1]
                           | (arr[off + 2] << 8)
                           | (arr[off + 3] << 16)
                           | (arr[off + 4] << 24);
            RejectReplay(sequence);

            using var aes = Aes.Create();
            aes.Key = encKeyRecv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[IvLength];
            Buffer.BlockCopy(arr, off + headerLength, iv, 0, IvLength);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(arr, off + headerLength + IvLength, cipherLen);
        }

        /// <summary>
        /// 拒绝重放/乱序帧：接收序号必须严格大于已见最大值。
        /// 用 CAS 循环更新，保证并发解码时不会有两帧都通过校验（否则同一帧可被多线程同时接受）。
        /// </summary>
        void RejectReplay(int sequence)
        {
            int last;
            while (true)
            {
                last = Volatile.Read(ref lastReceiveSequence);
                if (sequence <= last)
                {
                    throw new InvalidDataException(
                        "重放/乱序帧被拒绝：序号 " + sequence + " 不大于已见最大序号 " + last);
                }
                if (Interlocked.CompareExchange(ref lastReceiveSequence, sequence, last) == last)
                {
                    return;
                }
            }
        }

        /// <summary>常量时间字节比较（避免 MAC 校验的计时侧信道）</summary>
        static bool FixedTimeEquals(byte[] expected, byte[] actual, int actualOffset, int count)
        {
            int diff = expected.Length ^ count;
            for (int i = 0; i < count; i++)
            {
                diff |= expected[i] ^ actual[actualOffset + i];
            }
            return diff == 0;
        }
    }
}
