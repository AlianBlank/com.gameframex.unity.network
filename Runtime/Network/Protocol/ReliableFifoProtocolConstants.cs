// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 可靠 FIFO 协议包头布局常量。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class PacketHeaderLayout
    {
        /// <summary>协议版本。</summary>
        public const ushort ProtocolVersion = 1;

        /// <summary>基础头长度。</summary>
        public const int BaseHeaderLength = 16;

        /// <summary>可靠扩展头长度。</summary>
        public const int ReliableExtensionLength = 24;

        /// <summary>完整可靠头长度。</summary>
        public const int ReliableHeaderLength = 40;

        /// <summary>PacketLength 字段偏移。</summary>
        public const int PacketLengthOffset = 0;

        /// <summary>OperationType 字段偏移。</summary>
        public const int OperationTypeOffset = 4;

        /// <summary>ZipFlag 字段偏移。</summary>
        public const int ZipFlagOffset = 5;

        /// <summary>HeaderFlags 字段偏移。</summary>
        public const int HeaderFlagsOffset = 6;

        /// <summary>UniqueId 字段偏移。</summary>
        public const int UniqueIdOffset = 8;

        /// <summary>MessageId 字段偏移。</summary>
        public const int MessageIdOffset = 12;

        /// <summary>SessionId 字段偏移。</summary>
        public const int SessionIdOffset = 16;

        /// <summary>ReliableSequence 字段偏移。</summary>
        public const int ReliableSequenceOffset = 24;

        /// <summary>AckSequence 字段偏移。</summary>
        public const int AckSequenceOffset = 32;
    }

    /// <summary>
    /// 可靠 FIFO 协议头标记位。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class PacketHeaderFlags
    {
        /// <summary>协议版本掩码。</summary>
        public const ushort ProtocolVersionMask = 0xF000;

        /// <summary>协议版本位偏移。</summary>
        public const int ProtocolVersionShift = 12;

        /// <summary>可靠扩展标记。</summary>
        public const ushort Reliable = 1 << 7;

        /// <summary>ACK 标记。</summary>
        public const ushort Ack = 1 << 6;

        /// <summary>控制包标记。</summary>
        public const ushort Control = 1 << 5;

        /// <summary>Resume 标记。</summary>
        public const ushort Resume = 1 << 4;

        /// <summary>LatestOnly 标记。</summary>
        public const ushort LatestOnly = 1 << 3;

        /// <summary>NoRetry 标记。</summary>
        public const ushort NoRetry = 1 << 2;

        /// <summary>Duplicate 标记。</summary>
        public const ushort Duplicate = 1 << 1;

        /// <summary>
        /// 将协议版本写入标记位。
        /// </summary>
        public static ushort WithProtocolVersion(ushort flags)
        {
            return (ushort)((flags & ~ProtocolVersionMask) | (PacketHeaderLayout.ProtocolVersion << ProtocolVersionShift));
        }

        /// <summary>
        /// 读取协议版本。
        /// </summary>
        public static ushort GetProtocolVersion(ushort flags)
        {
            return (ushort)((flags & ProtocolVersionMask) >> ProtocolVersionShift);
        }
    }

    /// <summary>
    /// 可靠 FIFO 协议默认配置。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class ReliableFifoProtocolOptions
    {
        /// <summary>心跳预警超时。</summary>
        public const int HeartbeatWarnTimeoutSeconds = 5;

        /// <summary>静默重连窗口。</summary>
        public const int SilentResumeWindowSeconds = 10;

        /// <summary>服务端会话 TTL。</summary>
        public const int ServerSessionTtlSeconds = 30;

        /// <summary>可靠消息超时。</summary>
        public const int ReliableMessageTimeoutSeconds = 15;

        /// <summary>可靠重试上限。</summary>
        public const int ReliableRetryLimit = 5;

        /// <summary>pending 队列最大条数。</summary>
        public const int PendingQueueMaxCount = 1024;

        /// <summary>pending 队列最大字节数。</summary>
        public const int PendingQueueMaxBytes = 4 * 1024 * 1024;

        /// <summary>单条 pending 最大字节数。</summary>
        public const int PendingMessageMaxBytes = 256 * 1024;

        /// <summary>pending 队列超限原因。</summary>
        public const string PendingQueueOverflowReason = "PendingQueueOverflow";
    }

    /// <summary>
    /// 可靠 FIFO 协议的服务端协作契约。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class ReliableFifoServerContract
    {
        /// <summary>SessionId 由服务端生成。</summary>
        public const bool SessionIdGeneratedByServer = true;

        /// <summary>登录请求初始 SessionId。</summary>
        public const ulong LoginRequestInitialSessionId = 0ul;

        /// <summary>初始可靠序号。</summary>
        public const ulong InitialReliableSequence = 1ul;

        /// <summary>可靠响应缓存条数。</summary>
        public const int ResponseCacheMaxCount = 256;

        /// <summary>可靠响应缓存 TTL。</summary>
        public const int ResponseCacheTtlSeconds = 30;

        /// <summary>登录响应缓存 TTL。</summary>
        public const int LoginResponseCacheTtlSeconds = 30;

        /// <summary>Resume 时要求校验 token。</summary>
        public const bool RequiresResumeTokenValidation = true;
    }
}
