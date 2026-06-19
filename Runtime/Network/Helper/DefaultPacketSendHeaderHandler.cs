using System.IO;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 默认消息发送头部处理器
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class DefaultPacketSendHeaderHandler : IPacketSendHeaderHandler, IPacketHandler
    {
        /// <summary>
        /// 通道级别的消息序列化器，为空时回退使用全局序列化器。
        /// </summary>
        /// <remarks>
        /// Channel-level message serializer; falls back to the global serializer when null.
        /// </remarks>
        internal IMessageSerializer ChannelSerializer { get; set; }
        public DefaultPacketSendHeaderHandler()
        {
            PacketHeaderLength = PacketHeaderLayout.BaseHeaderLength;
            m_CachedByte = new byte[PacketHeaderLength];
        }

        /// <summary>
        /// 消息包头长度
        /// </summary>
        public ushort PacketHeaderLength { get; }

        /// <summary>
        /// 获取网络消息包协议编号。
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 获取网络消息包长度。
        /// </summary>
        public uint PacketLength { get; private set; }

        /// <summary>
        /// 是否压缩消息内容
        /// </summary>
        public bool IsZip { get; private set; }

        /// <summary>
        /// 超过消息的长度超过该值的时候启用压缩.该值 必须在设置压缩器的时候才生效,默认512
        /// </summary>
        public virtual uint LimitCompressLength { get; } = 512;

        private readonly byte[] m_CachedByte;

        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="messageObject">消息对象</param>
        /// <param name="messageCompressHandler">压缩消息内容处理器</param>
        /// <param name="destination">缓存流</param>
        /// <param name="messageBodyBuffer">消息序列化完的二进制数组</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool Handler<T>(T messageObject, IMessageCompressHandler messageCompressHandler, MemoryStream destination, out byte[] messageBodyBuffer) where T : MessageObject
        {
            int offset = 0;
            var messageType = messageObject.GetType();
            Id = ProtoMessageIdHandler.GetReqMessageIdByType(messageType);
            messageBodyBuffer = (ChannelSerializer ?? MessageSerializerRegistry.Global).Serialize(messageObject);
            if (messageBodyBuffer == null)
            {
                return false;
            }
            if (messageCompressHandler != null && messageBodyBuffer.Length > LimitCompressLength)
            {
                IsZip = true;
                messageBodyBuffer = messageCompressHandler.Handler(messageBodyBuffer);
            }
            else
            {
                IsZip = false;
            }

            var messageLength = messageBodyBuffer.Length;
            PacketLength = (uint)(PacketHeaderLength + messageLength);
            // 数据包总大小
            m_CachedByte.WriteUInt(PacketLength, ref offset);
            // 消息操作类型
            m_CachedByte.WriteByte((byte)(ProtoMessageIdHandler.IsHeartbeat(messageType) ? 1 : 4), ref offset);
            // 消息压缩标记
            m_CachedByte.WriteByte((byte)(IsZip ? 1 : 0), ref offset);
            // 协议标记
            m_CachedByte.WriteUShort(PacketHeaderFlags.WithProtocolVersion(0), ref offset);
            // 消息编号
            m_CachedByte.WriteInt(messageObject.UniqueId, ref offset);
            // 消息ID
            m_CachedByte.WriteInt(Id, ref offset);
            destination.Write(m_CachedByte, 0, PacketHeaderLength);
            return true;
        }
    }
}
