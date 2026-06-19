// ==========================================================================================
//  GameFrameX 组织及其衍生项目的版权、商标、专利及其他相关权利
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  均受中华人民共和国及相关国际法律法规保护。
//  are protected by the laws of the People's Republic of China and relevant international regulations.
//
//  使用本项目须严格遵守相应法律法规及开源许可证之规定。
//  Usage of this project must strictly comply with applicable laws, regulations, and open-source licenses.
//
//  本项目采用 MIT 许可证与 Apache License 2.0 双许可证分发，
//  This project is dual-licensed under the MIT License and Apache License 2.0,
//  完整许可证文本请参见源代码根目录下的 LICENSE 文件。
//  please refer to the LICENSE file in the root directory of the source code for the full license text.
//
//  禁止利用本项目实施任何危害国家安全、破坏社会秩序、
//  It is prohibited to use this project to engage in any activities that endanger national security, disrupt social order,
//  侵犯他人合法权益等法律法规所禁止的行为！
//  or infringe upon the legitimate rights and interests of others, as prohibited by laws and regulations!
//  因基于本项目二次开发所产生的一切法律纠纷与责任，
//  Any legal disputes and liabilities arising from secondary development based on this project
//  本项目组织与贡献者概不承担。
//  shall be borne solely by the developer; the project organization and contributors assume no responsibility.
//
//  GitHub 仓库：https://github.com/GameFrameX
//  GitHub Repository: https://github.com/GameFrameX
//  Gitee  仓库：https://gitee.com/GameFrameX
//  Gitee Repository:  https://gitee.com/GameFrameX
//  官方文档：https://gameframex.doc.alianblank.com/
//  Official Documentation: https://gameframex.doc.alianblank.com/
// ==========================================================================================

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using GameFrameX.Runtime;
using kcp2k;

namespace GameFrameX.Network.Runtime
{
    public sealed partial class NetworkManager
    {
        /// <summary>
        /// KCP 网络频道。
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        private sealed class KcpNetworkChannel : NetworkChannelBase
        {
            private readonly KcpConfig m_KcpConfig;
            private readonly ServiceType? m_ServiceType;
            private IKcpTransport m_Transport;
            private Kcp m_Kcp;
            private readonly ConcurrentQueue<PooledBuffer> m_RawReceiveQueue;
            private byte[] m_KcpReceiveBuffer;
            private readonly MemoryStream m_SendMemoryStream;
            private volatile bool m_KcpClosed;
            private object m_ConnectUserData;

            /// <summary>
            /// 初始化 KCP 网络频道的新实例。
            /// </summary>
            /// <param name="name">网络频道名称。</param>
            /// <param name="networkChannelHelper">网络频道辅助器。</param>
            /// <param name="rpcTimeout">RPC超时时间。</param>
            /// <param name="serviceType">服务类型。</param>
            /// <param name="kcpConfig">KCP配置。</param>
            public KcpNetworkChannel(string name, INetworkChannelHelper networkChannelHelper, int rpcTimeout, ServiceType serviceType, KcpConfig kcpConfig)
                : base(name, networkChannelHelper, rpcTimeout)
            {
                m_ServiceType = serviceType;
                m_KcpConfig = kcpConfig ?? new KcpConfig();
                m_RawReceiveQueue = new ConcurrentQueue<PooledBuffer>();
                m_KcpReceiveBuffer = new byte[0];
                m_SendMemoryStream = new MemoryStream();
                m_Transport = null;
                m_Kcp = null;
                m_KcpClosed = false;
                m_ConnectUserData = null;
            }

            /// <summary>
            /// 初始化 KCP 网络频道的新实例（URI scheme 自动推断传输层）。
            /// </summary>
            /// <param name="name">网络频道名称。</param>
            /// <param name="networkChannelHelper">网络频道辅助器。</param>
            /// <param name="rpcTimeout">RPC超时时间。</param>
            /// <param name="kcpConfig">KCP配置。</param>
            public KcpNetworkChannel(string name, INetworkChannelHelper networkChannelHelper, int rpcTimeout, KcpConfig kcpConfig)
                : base(name, networkChannelHelper, rpcTimeout)
            {
                m_ServiceType = null;
                m_KcpConfig = kcpConfig ?? new KcpConfig();
                m_RawReceiveQueue = new ConcurrentQueue<PooledBuffer>();
                m_KcpReceiveBuffer = new byte[0];
                m_SendMemoryStream = new MemoryStream();
                m_Transport = null;
                m_Kcp = null;
                m_KcpClosed = false;
                m_ConnectUserData = null;
            }

            /// <summary>
            /// 连接到远程主机。
            /// </summary>
            /// <param name="address">远程主机的地址。</param>
            /// <param name="userData">用户自定义数据。</param>
            public override void Connect(Uri address, object userData = null)
            {
                if (PIsConnecting)
                {
                    return;
                }

                if (PSocket != null)
                {
                    Close(NetworkCloseReason.ConnectClose, (ushort)NetworkErrorCode.DisposeError);
                }

                ValidateKcpConfig();
                PIsConnecting = true;
                m_ConnectUserData = userData;

                m_KcpClosed = false;
                PSendState.Reset();
                PReceiveState.PrepareForPacketHeader();

                ServiceType effectiveServiceType;
                if (m_ServiceType.HasValue)
                {
                    effectiveServiceType = m_ServiceType.Value;
                }
                else
                {
                    effectiveServiceType = InferServiceTypeFromScheme(address.Scheme);
                }

                m_Transport = CreateTransport(effectiveServiceType);
                if (m_Transport == null)
                {
                    PIsConnecting = false;
                    string errorMessage = Utility.Text.Format("Unsupported service type '{0}' for KCP channel.", effectiveServiceType);
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SocketError, SocketError.Success, errorMessage);
                        return;
                    }

                    throw new GameFrameworkException(errorMessage);
                }

                m_Transport.SetOnConnected(OnTransportConnected);
                m_Transport.SetOnDataReceived(OnTransportDataReceived);
                m_Transport.SetOnClosed(OnTransportClosed);

                m_Kcp = new Kcp(m_KcpConfig.ConversationId, OnKcpOutput);
                m_Kcp.SetNoDelay(m_KcpConfig.NoDelay ? 1u : 0u, m_KcpConfig.Interval, m_KcpConfig.Resend, m_KcpConfig.NoCongestionControl);
                m_Kcp.SetMtu(m_KcpConfig.Mtu);
                m_Kcp.SetWindowSize(m_KcpConfig.SendWindowSize, m_KcpConfig.ReceiveWindowSize);

                PNetworkChannelHelper.PrepareForConnecting();
                PSocket = new KcpNetworkSocket(m_Transport);

                m_SentPacketCount = 0;
                m_ReceivedPacketCount = 0;

                lock (PSendPacketPool)
                {
                    PSendPacketPool.Clear();
                }

                lock (PHeartBeatState)
                {
                    PHeartBeatState.Reset(true);
                }

                try
                {
                    m_Transport.Connect(address);
                }
                catch (Exception exception)
                {
                    PIsConnecting = false;
                    PSocket = null;
                    m_Transport.Dispose();
                    m_Transport = null;
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.ConnectError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                        return;
                    }

                    throw;
                }
            }

            public override void Close(string reason, ushort code = 0)
            {
                lock (this)
                {
                    if (m_KcpClosed)
                    {
                        return;
                    }

                    m_KcpClosed = true;
                    PActive = false;
                    PIsConnecting = false;

                    if (m_Kcp != null)
                    {
                        m_Kcp = null;
                    }

                    if (m_Transport != null)
                    {
                        try
                        {
                            m_Transport.Close();
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    while (m_RawReceiveQueue.TryDequeue(out var rawData))
                    {
                        rawData.Dispose();
                    }

                    m_SentPacketCount = 0;
                    m_ReceivedPacketCount = 0;

                    lock (PSendPacketPool)
                    {
                        PSendPacketPool.Clear();
                    }

                    lock (PHeartBeatState)
                    {
                        PHeartBeatState.Reset(true);
                    }

                    PRpcState.Reset();
                    while (m_ExecutionMessageQueue.Count > 0)
                    {
                        m_ExecutionMessageQueue.TryDequeue(out _);
                    }

                    NetworkChannelClosed?.Invoke(this, reason, code);
                    PSocket = null;
                }
            }

            public override void Shutdown()
            {
                Close(NetworkCloseReason.Normal);

                if (m_Transport != null)
                {
                    m_Transport.Dispose();
                    m_Transport = null;
                }

                m_SendMemoryStream.Dispose();
                PNetworkChannelHelper.Shutdown();
            }

            public override void Update(float elapseSeconds, float realElapseSeconds)
            {
                if (!PActive)
                {
                    return;
                }

                ProcessRawReceive();

                if (m_Kcp != null && !m_KcpClosed)
                {
                    uint currentMs = (uint)Environment.TickCount;
                    m_Kcp.Update(currentMs);
                }

                ProcessKcpReceive();
                ProcessSend();
                ProcessHeartBeat(realElapseSeconds);
                ProcessReceivedMessage();
                PRpcState.Update(elapseSeconds, realElapseSeconds);
            }

            private void ProcessRawReceive()
            {
                while (m_RawReceiveQueue.TryDequeue(out var rawData))
                {
                    try
                    {
                        if (m_Kcp == null || m_KcpClosed)
                        {
                            return;
                        }

                        m_Kcp.Input(rawData.Buffer, 0, rawData.Length);
                    }
                    catch (Exception exception)
                    {
                        PActive = false;
                        if (NetworkChannelError != null)
                        {
                            NetworkChannelError(this, NetworkErrorCode.ReceiveError, SocketError.Success, exception.ToString());
                            return;
                        }

                        throw;
                    }
                    finally
                    {
                        rawData.Dispose();
                    }
                }
            }

            private void ProcessKcpReceive()
            {
                if (m_Kcp == null || m_KcpClosed)
                {
                    return;
                }

                for (;;)
                {
                    int msgSize = m_Kcp.PeekSize();
                    if (msgSize <= 0)
                    {
                        break;
                    }

                    if (msgSize > m_KcpConfig.MaxMessageSize)
                    {
                        PActive = false;
                        if (NetworkChannelError != null)
                        {
                            NetworkChannelError(this, NetworkErrorCode.ReceiveError, SocketError.Success, "KCP message size exceeds configured limit.");
                            return;
                        }

                        throw new GameFrameworkException("KCP message size exceeds configured limit.");
                    }

                    if (msgSize > m_KcpReceiveBuffer.Length)
                    {
                        m_KcpReceiveBuffer = new byte[msgSize];
                    }

                    int bytesRead = m_Kcp.Receive(m_KcpReceiveBuffer, msgSize);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    lock (PHeartBeatState)
                    {
                        PHeartBeatState.Reset(PResetHeartBeatElapseSecondsWhenReceivePacket);
                    }

                    System.Threading.Interlocked.Increment(ref m_ReceivedPacketCount);

                    ProcessKcpMessage(m_KcpReceiveBuffer, bytesRead);
                }
            }

            private void ProcessKcpMessage(byte[] data, int length)
            {
                if (length < PacketReceiveHeaderHandler.PacketHeaderLength)
                {
                    return;
                }

                bool processSuccess = PNetworkChannelHelper.DeserializePacketHeader(data);
                if (!processSuccess)
                {
                    NetworkChannelError?.Invoke(this, NetworkErrorCode.DeserializePacketHeaderError, SocketError.Success, "Packet header is invalid.");
                    return;
                }

                var bodyLength = (int)(PacketReceiveHeaderHandler.PacketLength - PacketReceiveHeaderHandler.PacketHeaderLength);
                if (length < bodyLength + PacketReceiveHeaderHandler.PacketHeaderLength)
                {
                    return;
                }

                PReceiveState.Reset(bodyLength, PacketReceiveHeaderHandler);

                byte[] bodyBuffer = new byte[bodyLength];
                Buffer.BlockCopy(data, PacketReceiveHeaderHandler.PacketHeaderLength, bodyBuffer, 0, bodyLength);

                if (PReceiveState.PacketHeader.ZipFlag != 0)
                {
                    GameFrameworkGuard.NotNull(MessageDecompressHandler, nameof(MessageDecompressHandler));
                    bodyBuffer = MessageDecompressHandler.Handler(bodyBuffer);
                }

                processSuccess = PNetworkChannelHelper.DeserializePacketBody(bodyBuffer, PacketReceiveHeaderHandler.Id, out var messageObject);
                if (processSuccess)
                {
                    messageObject.SetUpdateUniqueId(PacketReceiveHeaderHandler.UniqueId);
                    DebugReceiveLog(messageObject);
                    m_ExecutionMessageQueue.Enqueue(messageObject);
                    PReceiveState.PrepareForPacketHeader();
                }
                else
                {
                    NetworkChannelError?.Invoke(this, NetworkErrorCode.DeserializePacketError, SocketError.Success, "Packet body is invalid.");
                }
            }

            protected override bool ProcessSend()
            {
                lock (PSendPacketPool)
                {
                    if (PSendPacketPool.Count <= 0)
                    {
                        return false;
                    }

                    while (PSendPacketPool.First != null)
                    {
                        var messageObject = PSendPacketPool.First.Value;

                        try
                        {
                            DebugSendLog(messageObject);
                            m_SendMemoryStream.SetLength(0);
                            m_SendMemoryStream.Position = 0;

                            bool serializeResult = PNetworkChannelHelper.SerializePacketHeader(messageObject, m_SendMemoryStream, out var messageBodyBuffer);
                            if (serializeResult)
                            {
                                serializeResult = PNetworkChannelHelper.SerializePacketBody(messageBodyBuffer, m_SendMemoryStream);
                            }

                            if (!serializeResult)
                            {
                                const string errorMessage = "Serialized packet failure.";
                                throw new InvalidOperationException(errorMessage);
                            }

                            ReferencePool.Release(messageObject);

                            if (m_Kcp == null || m_KcpClosed)
                            {
                                return false;
                            }

                            byte[] sendBuffer = m_SendMemoryStream.ToArray();
                            if (sendBuffer.Length > m_KcpConfig.MaxMessageSize)
                            {
                                PActive = false;
                                if (NetworkChannelError != null)
                                {
                                    NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, "KCP message size exceeds configured limit.");
                                    return false;
                                }

                                throw new GameFrameworkException("KCP message size exceeds configured limit.");
                            }

                            int sendResult = m_Kcp.Send(sendBuffer, 0, sendBuffer.Length);
                            if (sendResult < 0)
                            {
                                PActive = false;
                                if (NetworkChannelError != null)
                                {
                                    NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, "KCP send failed.");
                                    return false;
                                }

                                throw new GameFrameworkException("KCP send failed.");
                            }

                            System.Threading.Interlocked.Increment(ref m_SentPacketCount);
                        }
                        catch (Exception exception)
                        {
                            PActive = false;
                            if (NetworkChannelError != null)
                            {
                                SocketException socketException = exception as SocketException;
                                NetworkChannelError(this, NetworkErrorCode.SerializeError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                                return false;
                            }

                            throw;
                        }
                        finally
                        {
                            PSendPacketPool.RemoveFirst();
                        }
                    }

                    return true;
                }
            }

            private void OnKcpOutput(byte[] data, int size)
            {
                if (m_Transport == null || !m_Transport.IsConnected)
                {
                    return;
                }

                try
                {
                    byte[] sendBuffer = new byte[size];
                    Buffer.BlockCopy(data, 0, sendBuffer, 0, size);
                    m_Transport.SendRaw(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception exception)
                {
                    PActive = false;
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.SendError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                        return;
                    }

                    throw;
                }
            }

            private void OnTransportConnected()
            {
                if (m_KcpClosed)
                {
                    return;
                }

                if (PActive)
                {
                    return;
                }

                PActive = true;
                PIsConnecting = false;
                NetworkChannelConnected?.Invoke(this, m_ConnectUserData);
            }

            private void OnTransportDataReceived(byte[] data, int length)
            {
                m_RawReceiveQueue.Enqueue(PooledBuffer.CopyFrom(data, 0, length));
            }

            private void OnTransportClosed()
            {
                if (PActive || PIsConnecting)
                {
                    Close("Transport closed.", (ushort)NetworkErrorCode.SocketError);
                }
            }

            private void ValidateKcpConfig()
            {
                if (m_KcpConfig.Mtu < 50)
                {
                    throw new GameFrameworkException("KCP MTU must be greater than or equal to 50.");
                }

                if (m_KcpConfig.Mtu > 65535)
                {
                    throw new GameFrameworkException("KCP MTU must be less than or equal to 65535.");
                }

                if (m_KcpConfig.SendWindowSize == 0)
                {
                    throw new GameFrameworkException("KCP send window size must be positive.");
                }

                if (m_KcpConfig.ReceiveWindowSize == 0)
                {
                    throw new GameFrameworkException("KCP receive window size must be positive.");
                }

                if (m_KcpConfig.MaxMessageSize <= 0)
                {
                    throw new GameFrameworkException("KCP max message size must be positive.");
                }
            }

            /// <summary>
            /// 根据 URI scheme 推断 KCP 服务类型。
            /// </summary>
            /// <param name="scheme">URI 的 scheme 部分（不区分大小写）。</param>
            /// <returns>对应的 ServiceType。</returns>
            private static ServiceType InferServiceTypeFromScheme(string scheme)
            {
                switch (scheme.ToLowerInvariant())
                {
                    case "kcp":
                    {
                        return ServiceType.KcpUdp;
                    }
                    case "kcp-tcp":
                    {
                        return ServiceType.KcpTcp;
                    }
                    case "kcp-ws":
                    case "kcp-wss":
                    {
                        return ServiceType.KcpWebSocket;
                    }
                    default:
                    {
                        throw new GameFrameworkException(Utility.Text.Format(
                            "Unsupported KCP URI scheme '{0}'. Supported: kcp, kcp-tcp, kcp-ws, kcp-wss.",
                            scheme));
                    }
                }
            }

            private IKcpTransport CreateTransport(ServiceType serviceType)
            {
                switch (serviceType)
                {
                    case ServiceType.Kcp:
                    case ServiceType.KcpUdp:
                    {
                        return new KcpUdpTransport((int)m_KcpConfig.Mtu);
                    }

                    case ServiceType.KcpTcp:
                    {
                        return new KcpTcpTransport(m_KcpConfig.MaxMessageSize);
                    }

#if ENABLE_GAME_FRAME_X_WEB_SOCKET
                    case ServiceType.KcpWebSocket:
                    {
                        return new KcpWebSocketTransport();
                    }
#endif

                    default:
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// KCP 网络套接字包装。
            /// </summary>
            private sealed class KcpNetworkSocket : INetworkSocket
            {
                private readonly IKcpTransport m_Transport;

                public KcpNetworkSocket(IKcpTransport transport)
                {
                    m_Transport = transport;
                }

                public bool IsConnected
                {
                    get { return m_Transport != null && m_Transport.IsConnected; }
                }

                public bool IsClosed
                {
                    get { return m_Transport == null || !m_Transport.IsConnected; }
                }

                public System.Net.EndPoint LocalEndPoint
                {
                    get { return null; }
                }

                public System.Net.EndPoint RemoteEndPoint
                {
                    get { return null; }
                }

                public int ReceiveBufferSize { get; set; }
                public int SendBufferSize { get; set; }

                public void Shutdown()
                {
                    if (m_Transport != null)
                    {
                        m_Transport.Close();
                    }
                }

                public void Close()
                {
                    if (m_Transport != null)
                    {
                        m_Transport.Close();
                    }
                }
            }
        }
    }
}
