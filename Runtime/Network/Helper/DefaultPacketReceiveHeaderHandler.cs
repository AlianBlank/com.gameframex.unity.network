using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 默认消息接收头处理器
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class DefaultPacketReceiveHeaderHandler : IPacketReceiveHeaderHandler, IPacketHandler
    {
        /// <summary>
        /// 包长度
        /// </summary>
        public uint PacketLength { get; private set; }

        /// <summary>
        /// 消息ID
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 消息唯一编号
        /// </summary>
        public int UniqueId { get; private set; }

        /// <summary>
        /// 消息操作类型
        /// </summary>
        public byte OperationType { get; private set; }

        /// <summary>
        /// 压缩标记
        /// </summary>
        public byte ZipFlag { get; private set; }

        /// <summary>
        /// 协议头标记。
        /// </summary>
        public ushort HeaderFlags { get; private set; }

        /// <summary>
        /// 协议版本。
        /// </summary>
        public ushort ProtocolVersion { get; private set; }

        /// <summary>
        /// 是否携带可靠扩展头。
        /// </summary>
        public bool HasReliableExtension { get; private set; }

        /// <summary>
        /// 是否为重复响应。
        /// </summary>
        public bool IsDuplicate { get; private set; }

        /// <summary>
        /// 会话编号。
        /// </summary>
        public ulong SessionId { get; private set; }

        /// <summary>
        /// 可靠消息序号。
        /// </summary>
        public ulong ReliableSequence { get; private set; }

        /// <summary>
        /// ACK 序号。
        /// </summary>
        public ulong AckSequence { get; private set; }

        /// <summary>
        /// 消息包头长度
        /// </summary>
        public ushort PacketHeaderLength { get; private set; } = PacketHeaderLayout.BaseHeaderLength;

        /// <summary>
        /// 消息包处理
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public bool Handler(object source)
        {
            byte[] reader = source as byte[];
            if (reader == null || reader.Length < PacketHeaderLayout.BaseHeaderLength)
            {
                return false;
            }

            ResetReliableFields();
            // packetLength
            int offset = 0;
            var readPacketLength = reader.ReadUInt(ref offset); //4
            PacketLength = readPacketLength;
            // operationType
            OperationType = reader.ReadByte(ref offset); //1
            // zipFlag
            ZipFlag = reader.ReadByte(ref offset); //1
            // headerFlags
            HeaderFlags = reader.ReadUShort(ref offset); //2
            ProtocolVersion = PacketHeaderFlags.GetProtocolVersion(HeaderFlags);
            if (ProtocolVersion != PacketHeaderLayout.ProtocolVersion)
            {
                return false;
            }
            // uniqueId
            UniqueId = reader.ReadInt(ref offset); //4
            // MessageId
            Id = reader.ReadInt(ref offset); //4
            HasReliableExtension = (HeaderFlags & PacketHeaderFlags.Reliable) != 0;
            IsDuplicate = (HeaderFlags & PacketHeaderFlags.Duplicate) != 0;
            PacketHeaderLength = HasReliableExtension ? (ushort)PacketHeaderLayout.ReliableHeaderLength : (ushort)PacketHeaderLayout.BaseHeaderLength;
            if (HasReliableExtension)
            {
                if (reader.Length < PacketHeaderLength)
                {
                    return false;
                }

                SessionId = ReadUInt64BigEndian(reader, ref offset);
                ReliableSequence = ReadUInt64BigEndian(reader, ref offset);
                AckSequence = ReadUInt64BigEndian(reader, ref offset);
            }
            return true;
        }

        private void ResetReliableFields()
        {
            PacketHeaderLength = PacketHeaderLayout.BaseHeaderLength;
            HeaderFlags = 0;
            ProtocolVersion = 0;
            HasReliableExtension = false;
            IsDuplicate = false;
            SessionId = 0ul;
            ReliableSequence = 0ul;
            AckSequence = 0ul;
        }

        private static ulong ReadUInt64BigEndian(byte[] buffer, ref int offset)
        {
            if (offset < 0 || offset + sizeof(ulong) > buffer.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(offset), "buffer read out of index");
            }

            ulong value = 0ul;
            for (var i = 0; i < sizeof(ulong); i++)
            {
                value = (value << 8) | buffer[offset + i];
            }

            offset += sizeof(ulong);
            return value;
        }
    }
}
