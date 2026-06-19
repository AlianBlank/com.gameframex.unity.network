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

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 基于 TCP 的 KCP 传输层实现。
    /// <para>使用 4 字节大端长度前缀帧协议解决 TCP 流式传输的粘包问题。</para>
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class KcpTcpTransport : IKcpTransport
    {
        /// <summary>
        /// 接收状态枚举。
        /// </summary>
        private enum ReceiveState
        {
            /// <summary>
            /// 正在读取长度前缀（4 字节）。
            /// </summary>
            ReadLength = 0,

            /// <summary>
            /// 正在读取消息体。
            /// </summary>
            ReadBody = 1,
        }

        /// <summary>
        /// 长度前缀字节数。
        /// </summary>
        private const int LengthPrefixSize = 4;

        private const int DefaultMaxFrameSize = 256 * 1024;

        private readonly int _maxFrameSize;

        /// <summary>
        /// 底层 TCP Socket。
        /// </summary>
        private Socket _socket;

        /// <summary>
        /// 是否已连接。
        /// </summary>
        private volatile bool _isConnected;

        /// <summary>
        /// 数据接收回调。
        /// </summary>
        private Action<byte[], int> _onDataReceived;

        /// <summary>
        /// 连接成功回调。
        /// </summary>
        private Action _onConnected;

        /// <summary>
        /// 连接关闭回调。
        /// </summary>
        private Action _onClosed;

        /// <summary>
        /// 当前接收状态。
        /// </summary>
        private ReceiveState _receiveState;

        /// <summary>
        /// 长度前缀接收缓冲区（4 字节）。
        /// </summary>
        private readonly byte[] _receiveLengthBuffer = new byte[LengthPrefixSize];

        /// <summary>
        /// 消息体接收缓冲区（根据长度动态分配）。
        /// </summary>
        private byte[] _receiveBodyBuffer;

        /// <summary>
        /// 当前已接收字节数。
        /// </summary>
        private int _receiveOffset;

        /// <summary>
        /// 期望接收的消息体长度。
        /// </summary>
        private int _expectedLength;

        /// <summary>
        /// 获取是否已连接。
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        public KcpTcpTransport()
            : this(DefaultMaxFrameSize)
        {
        }

        public KcpTcpTransport(int maxFrameSize)
        {
            if (maxFrameSize <= 0)
            {
                throw new ArgumentOutOfRangeException("maxFrameSize", "Max frame size must be positive.");
            }

            _maxFrameSize = maxFrameSize;
        }

        /// <summary>
        /// 连接到远程端点。
        /// <para>地址格式：kcp-tcp://host:port</para>
        /// </summary>
        /// <param name="address">远程地址。</param>
        public void Connect(Uri address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }

            if (_isConnected)
            {
                throw new InvalidOperationException("传输层已连接，请先关闭当前连接。");
            }

            var host = address.Host;
            var port = address.Port;

            // DNS 解析
            var ipAddresses = Dns.GetHostAddresses(host);
            if (ipAddresses == null || ipAddresses.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            // 优先使用 IPv4 地址
            IPAddress targetAddress = null;
            for (int i = 0; i < ipAddresses.Length; i++)
            {
                if (ipAddresses[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    targetAddress = ipAddresses[i];
                    break;
                }
            }

            // 如果没有 IPv4 地址，使用第一个可用地址
            if (targetAddress == null)
            {
                targetAddress = ipAddresses[0];
            }

            var endPoint = new IPEndPoint(targetAddress, port);

            _socket = new Socket(targetAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;

            // 使用 APM 模式异步连接
            var connectResult = _socket.BeginConnect(endPoint, null, null);
            connectResult.AsyncWaitHandle.WaitOne();
            _socket.EndConnect(connectResult);

            _isConnected = true;

            // 重置接收状态
            ResetReceiveState();

            // 开始接收循环
            BeginReceive();
            _onConnected?.Invoke();
        }

        /// <summary>
        /// 发送原始数据。
        /// <para>自动添加 4 字节大端长度前缀。</para>
        /// </summary>
        /// <param name="data">要发送的数据。</param>
        /// <param name="offset">数据偏移量。</param>
        /// <param name="count">数据长度。</param>
        public void SendRaw(byte[] data, int offset, int count)
        {
            if (!_isConnected || _socket == null)
            {
                throw new InvalidOperationException("传输层未连接。");
            }

            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (count <= 0)
            {
                return;
            }

            // 构造带长度前缀的发送缓冲区：4 字节大端长度 + 载荷
            var sendBuffer = new byte[LengthPrefixSize + count];
            sendBuffer[0] = (byte)(count >> 24);
            sendBuffer[1] = (byte)(count >> 16);
            sendBuffer[2] = (byte)(count >> 8);
            sendBuffer[3] = (byte)(count);
            Buffer.BlockCopy(data, offset, sendBuffer, LengthPrefixSize, count);

            // 同步发送
            _socket.Send(sendBuffer, 0, sendBuffer.Length, SocketFlags.None);
        }

        /// <summary>
        /// 关闭传输连接。
        /// </summary>
        public void Close()
        {
            var socket = _socket;
            if (!_isConnected && socket == null)
            {
                return;
            }

            _isConnected = false;
            _socket = null;

            if (socket != null)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // 忽略关闭时的 Socket 异常
                }
                catch (ObjectDisposedException)
                {
                    // 忽略关闭时的 Socket 异常
                }

                try
                {
                    socket.Close();
                }
                catch (SocketException)
                {
                    // 忽略关闭时的 Socket 异常
                }
                catch (ObjectDisposedException)
                {
                    // 忽略关闭时的 Socket 异常
                }
            }

            var onClosed = _onClosed;
            if (onClosed != null)
            {
                onClosed.Invoke();
            }
        }

        /// <summary>
        /// 设置数据接收回调。
        /// </summary>
        /// <param name="onDataReceived">数据接收回调。</param>
        public void SetOnDataReceived(Action<byte[], int> onDataReceived)
        {
            _onDataReceived = onDataReceived;
        }

        public void SetOnConnected(Action onConnected)
        {
            _onConnected = onConnected;
        }

        /// <summary>
        /// 设置连接关闭回调。
        /// </summary>
        /// <param name="onClosed">连接关闭回调。</param>
        public void SetOnClosed(Action onClosed)
        {
            _onClosed = onClosed;
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 重置接收状态到初始值。
        /// </summary>
        private void ResetReceiveState()
        {
            _receiveState = ReceiveState.ReadLength;
            _receiveOffset = 0;
            _expectedLength = 0;
            _receiveBodyBuffer = null;
        }

        /// <summary>
        /// 开始异步接收数据。
        /// </summary>
        private void BeginReceive()
        {
            var socket = _socket;
            if (!_isConnected || socket == null)
            {
                return;
            }

            try
            {
                byte[] buffer;
                int remaining;

                if (_receiveState == ReceiveState.ReadLength)
                {
                    buffer = _receiveLengthBuffer;
                    remaining = LengthPrefixSize - _receiveOffset;
                }
                else
                {
                    buffer = _receiveBodyBuffer;
                    remaining = _expectedLength - _receiveOffset;
                }

                socket.BeginReceive(buffer, _receiveOffset, remaining, SocketFlags.None, OnReceiveCompleted, null);
            }
            catch (SocketException)
            {
                HandleConnectionLost();
            }
            catch (ObjectDisposedException)
            {
                HandleConnectionLost();
            }
        }

        /// <summary>
        /// 异步接收完成回调。
        /// </summary>
        /// <param name="ar">异步结果。</param>
        private void OnReceiveCompleted(IAsyncResult ar)
        {
            var socket = _socket;
            if (!_isConnected || socket == null)
            {
                return;
            }

            int bytesRead;
            try
            {
                bytesRead = socket.EndReceive(ar);
            }
            catch (SocketException)
            {
                HandleConnectionLost();
                return;
            }
            catch (ObjectDisposedException)
            {
                HandleConnectionLost();
                return;
            }

            // 对端关闭连接
            if (bytesRead <= 0)
            {
                HandleConnectionLost();
                return;
            }

            _receiveOffset += bytesRead;

            if (_receiveState == ReceiveState.ReadLength)
            {
                // 长度前缀尚未接收完整，继续接收
                if (_receiveOffset < LengthPrefixSize)
                {
                    BeginReceive();
                    return;
                }

                // 解析大端长度前缀
                _expectedLength = (_receiveLengthBuffer[0] << 24)
                                 | (_receiveLengthBuffer[1] << 16)
                                 | (_receiveLengthBuffer[2] << 8)
                                 | (_receiveLengthBuffer[3]);

                if (_expectedLength <= 0 || _expectedLength > _maxFrameSize)
                {
                    // 非法长度，重置状态
                    HandleConnectionLost();
                    return;
                }

                // 切换到读取消息体状态
                _receiveBodyBuffer = new byte[_expectedLength];
                _receiveOffset = 0;
                _receiveState = ReceiveState.ReadBody;
                BeginReceive();
            }
            else
            {
                // 消息体尚未接收完整，继续接收
                if (_receiveOffset < _expectedLength)
                {
                    BeginReceive();
                    return;
                }

                // 消息体接收完成，触发回调
                var onDataReceived = _onDataReceived;
                if (onDataReceived != null)
                {
                    onDataReceived.Invoke(_receiveBodyBuffer, _expectedLength);
                }

                // 切回读取长度前缀状态
                _receiveState = ReceiveState.ReadLength;
                _receiveOffset = 0;
                _expectedLength = 0;
                _receiveBodyBuffer = null;

                BeginReceive();
            }
        }

        /// <summary>
        /// 处理连接丢失。
        /// </summary>
        private void HandleConnectionLost()
        {
            if (_isConnected)
            {
                Close();
            }
        }
    }
}
