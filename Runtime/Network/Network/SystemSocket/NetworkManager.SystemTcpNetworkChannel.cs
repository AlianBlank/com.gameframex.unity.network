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
using System.Net;
using System.Net.Sockets;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    public sealed partial class NetworkManager
    {
        /// <summary>
        /// TCP 网络频道。
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        private sealed class SystemTcpNetworkChannel : NetworkChannelBase
        {
            private ConnectState m_ConnectState = null;
            private SystemNetSocket PSystemNetSocket = null;

            /// <summary>
            /// 初始化网络频道的新实例。
            /// </summary>
            /// <param name="name">网络频道名称。</param>
            /// <param name="networkChannelHelper">网络频道辅助器。</param>
            /// <param name="rpcTimeout">RPC超时时间</param>
            public SystemTcpNetworkChannel(string name, INetworkChannelHelper networkChannelHelper, int rpcTimeout) : base(name, networkChannelHelper, rpcTimeout)
            {
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

                base.Connect(address, userData);
                if (IsVerifyAddress)
                {
                    PSystemNetSocket = new SystemNetSocket(ConnectEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    PSocket = PSystemNetSocket;
                }

                if (PSocket == null)
                {
                    string errorMessage = "Initialize network channel failure.";
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SocketError, SocketError.Success, errorMessage);
                        return;
                    }

                    throw new GameFrameworkException(errorMessage);
                }

                PNetworkChannelHelper.PrepareForConnecting();

                ConnectAsync(userData);
            }

            protected override bool ProcessSend()
            {
                if (base.ProcessSend())
                {
                    SendAsync();
                    return true;
                }

                return false;
            }

            #region Receive

            private void ReceiveAsync()
            {
                try
                {
                    var position = (int)PReceiveState.Stream.Position;
                    var length = (int)(PReceiveState.Stream.Length - PReceiveState.Stream.Position);
                    PSystemNetSocket.BeginReceive(PReceiveState.Stream.GetBuffer(), position, length, SocketFlags.None, ReceiveCallback, PSystemNetSocket);
                }
                catch (Exception exception)
                {
                    PActive = false;
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.ReceiveError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                        return;
                    }

                    throw;
                }
            }

            private void ReceiveCallback(IAsyncResult asyncResult)
            {
                var systemNetSocket = (SystemNetSocket)asyncResult.AsyncState;
                if (!systemNetSocket.IsConnected)
                {
                    return;
                }

                int bytesReceived = 0;
                try
                {
                    bytesReceived = systemNetSocket.EndReceive(asyncResult);
                }
                catch (Exception exception)
                {
                    PActive = false;
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.ReceiveError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                        return;
                    }

                    throw;
                }

                if (bytesReceived <= 0)
                {
                    Close("Receive error.", (ushort)NetworkErrorCode.ReceiveError);
                    return;
                }

                lock (PHeartBeatState)
                {
                    PHeartBeatState.Reset(PResetHeartBeatElapseSecondsWhenReceivePacket);
                }

                PReceiveState.Stream.Position += bytesReceived;
                if (PReceiveState.Stream.Position < PReceiveState.Stream.Length)
                {
                    ReceiveAsync();
                    return;
                }

                PReceiveState.Stream.Position = 0L;
                bool processSuccess;
                if (PReceiveState.IsReadingReliableHeaderExtension)
                {
                    processSuccess = ProcessReliableHeaderExtension();
                    if (PReceiveState.IsEmptyBody)
                    {
                        ProcessPackBody();
                        ReceiveAsync();
                        return;
                    }
                }
                else if (PReceiveState.PacketHeader != null)
                {
                    processSuccess = ProcessPackBody();
                }
                else
                {
                    processSuccess = ProcessPackHeader();
                    if (PReceiveState.IsEmptyBody)
                    {
                        // 如果是空消息,直接返回
                        ProcessPackBody();
                        ReceiveAsync();
                        return;
                    }
                }

                if (processSuccess)
                {
                    ReceiveAsync();
                    return;
                }

                Close("Packet process error.", (ushort)NetworkErrorCode.DeserializePacketError);
            }

            /// <summary>
            /// 解析消息头
            /// </summary>
            /// <returns></returns>
            private bool ProcessPackHeader()
            {
                var baseHeaderLength = PacketHeaderLayout.BaseHeaderLength;
                var buffer = new byte[baseHeaderLength];
                _ = PReceiveState.Stream.Read(buffer, 0, baseHeaderLength);
                var processSuccess = PNetworkChannelHelper.DeserializePacketHeader(buffer);
                if (!processSuccess)
                {
                    return false;
                }

                var headerLength = PacketReceiveHeaderHandler.PacketHeaderLength;
                if (headerLength > baseHeaderLength)
                {
                    PReceiveState.PrepareForReliableHeaderExtension(buffer, headerLength - baseHeaderLength);
                    return true;
                }

                var bodyLength = (int)(PacketReceiveHeaderHandler.PacketLength - PacketReceiveHeaderHandler.PacketHeaderLength);
                if (bodyLength < 0)
                {
                    return false;
                }

                PReceiveState.Reset(bodyLength, PacketReceiveHeaderHandler);
                return processSuccess;
            }

            private bool ProcessReliableHeaderExtension()
            {
                var baseHeaderLength = PacketHeaderLayout.BaseHeaderLength;
                var headerLength = PacketReceiveHeaderHandler.PacketHeaderLength;
                var extensionLength = headerLength - baseHeaderLength;
                var extraHeader = new byte[extensionLength];
                _ = PReceiveState.Stream.Read(extraHeader, 0, extensionLength);

                var headerBuffer = new byte[headerLength];
                Buffer.BlockCopy(PReceiveState.BaseHeaderBuffer, 0, headerBuffer, 0, baseHeaderLength);
                Buffer.BlockCopy(extraHeader, 0, headerBuffer, baseHeaderLength, extensionLength);

                var processSuccess = PNetworkChannelHelper.DeserializePacketHeader(headerBuffer);
                if (!processSuccess)
                {
                    return false;
                }

                var bodyLength = (int)(PacketReceiveHeaderHandler.PacketLength - PacketReceiveHeaderHandler.PacketHeaderLength);
                if (bodyLength < 0)
                {
                    return false;
                }

                PReceiveState.Reset(bodyLength, PacketReceiveHeaderHandler);
                return true;
            }

            /// <summary>
            /// 解析消息内容
            /// </summary>
            /// <returns></returns>
            private bool ProcessPackBody()
            {
                var bodyLength = (int)(PReceiveState.PacketHeader.PacketLength - PReceiveState.PacketHeader.PacketHeaderLength);
                var buffer = new byte[bodyLength];
                _ = PReceiveState.Stream.Read(buffer, 0, bodyLength);

                if (PReceiveState.PacketHeader.ZipFlag != 0)
                {
                    // 解压
                    GameFrameworkGuard.NotNull(MessageDecompressHandler, nameof(MessageDecompressHandler));
                    buffer = MessageDecompressHandler.Handler(buffer);
                }

                var processSuccess = PNetworkChannelHelper.DeserializePacketBody(buffer, PacketReceiveHeaderHandler.Id, out var messageObject);
                if (processSuccess)
                {
                    messageObject.SetUpdateUniqueId(PacketReceiveHeaderHandler.UniqueId);
                    DebugReceiveLog(messageObject);
                    m_ExecutionMessageQueue.Enqueue(messageObject);
                    System.Threading.Interlocked.Increment(ref m_ReceivedPacketCount);
                    PReceiveState.PrepareForPacketHeader();
                }

                return processSuccess;
            }

            #endregion

            #region Sender

            protected override bool ProcessSendMessage(MessageObject messageObject)
            {
                if (!PActive)
                {
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SocketError, SocketError.Disconnecting, "Network channel is closing.");
                    }

                    return false;
                }

                bool serializeResult = base.ProcessSendMessage(messageObject);
                if (serializeResult)
                {
                }
                else
                {
                    const string errorMessage = "Serialized packet failure.";
                    throw new InvalidOperationException(errorMessage);
                }

                return true;
            }


            /// <summary>
            /// 实际发送异步数据
            /// </summary>
            private void SendAsync()
            {
                try
                {
                    PSystemNetSocket.BeginSend(PSendState.Stream.GetBuffer(), (int)PSendState.Stream.Position, (int)(PSendState.Stream.Length - PSendState.Stream.Position), SocketFlags.None, SendCallback, PSystemNetSocket);
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

            private void SendCallback(IAsyncResult asyncResult)
            {
                var systemNetSocket = (SystemNetSocket)asyncResult.AsyncState;
                if (!systemNetSocket.IsConnected)
                {
                    return;
                }

                int bytesSent = 0;
                try
                {
                    bytesSent = systemNetSocket.EndSend(asyncResult, out var error);
                }
                catch (Exception exception)
                {
                    PActive = false;
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.SendError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                    }

                    return;
                }

                PSendState.Stream.Position += bytesSent;
                if (PSendState.Stream.Position < PSendState.Stream.Length)
                {
                    SendAsync();
                    return;
                }

                System.Threading.Interlocked.Increment(ref m_SentPacketCount);
                PSendState.Reset();
            }

            #endregion

            #region Connect

            private void ConnectAsync(object userData)
            {
                try
                {
                    PIsConnecting = true;
                    m_ConnectState = new ConnectState(PSystemNetSocket, userData);
                    ((SystemNetSocket)PSocket).BeginConnect(ConnectEndPoint.Address, ConnectEndPoint.Port, ConnectCallback, m_ConnectState);
                }
                catch (Exception exception)
                {
                    if (NetworkChannelError != null)
                    {
                        SocketException socketException = exception as SocketException;
                        NetworkChannelError(this, NetworkErrorCode.ConnectError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                        return;
                    }

                    throw;
                }
            }

            private void ConnectCallback(IAsyncResult asyncResult)
            {
                PIsConnecting = false;
                var connectState = (ConnectState)asyncResult.AsyncState;
                var systemNetSocket = (SystemNetSocket)connectState.Socket;
                try
                {
                    systemNetSocket.EndConnect(asyncResult);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SocketException socketException = exception as SocketException;
                    NetworkChannelError?.Invoke(this, NetworkErrorCode.ConnectError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                    Close(NetworkCloseReason.ConnectClose, (ushort)(socketException?.SocketErrorCode ?? SocketError.Success));
                    return;
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

                PActive = true;
                NetworkChannelConnected?.Invoke(this, m_ConnectState.UserData);
                ReceiveAsync();
            }

            #endregion
        }
    }
}
