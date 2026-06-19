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
    /// KCP over UDP 传输层实现。
    /// <para>每个 KCP 输出段直接作为一个 UDP 数据报发送，无需额外帧协议。</para>
    /// <para>接收侧使用 APM 模式（BeginReceiveFrom / EndReceiveFrom）在 .NET ThreadPool 线程上运行。</para>
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class KcpUdpTransport : IKcpTransport
    {
        /// <summary>
        /// 接收缓冲区大小，取标准 MTU 1500 字节。
        /// KCP 默认 MTU 为 1200，加上 KCP 头部开销后 1500 足够。
        /// </summary>
        private const int ReceiveBufferSize = 1500;

        private readonly int _receiveBufferSize;

        /// <summary>
        /// UDP Socket 实例。
        /// </summary>
        private Socket _socket;

        /// <summary>
        /// 远程端点。
        /// </summary>
        private EndPoint _remoteEndPoint;

        /// <summary>
        /// 连接状态标记。
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
        /// 获取是否已连接。
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        public KcpUdpTransport()
            : this(ReceiveBufferSize)
        {
        }

        public KcpUdpTransport(int receiveBufferSize)
        {
            _receiveBufferSize = Math.Max(ReceiveBufferSize, receiveBufferSize);
        }

        /// <summary>
        /// 连接到远程端点。
        /// <para>解析 <c>kcp://host:port</c> 格式的 URI，创建 UDP Socket 并"连接"到目标地址。</para>
        /// <para>UDP 的 Connect 并不建立真正的连接，只是让后续 Send 可以省略目标地址。</para>
        /// </summary>
        /// <param name="address">远程地址，格式为 kcp://host:port。</param>
        public void Connect(Uri address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }

            if (_isConnected)
            {
                throw new InvalidOperationException("传输层已经连接，请先关闭当前连接。");
            }

            // 从 URI 中提取 host 和 port，进行 DNS 解析
            var host = address.Host;
            var port = address.Port;
            var ipAddresses = Dns.GetHostAddresses(host);
            if (ipAddresses == null || ipAddresses.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            var ipAddress = ipAddresses[0];
            var remoteEp = new IPEndPoint(ipAddress, port);

            // 创建 UDP Socket
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Connect(remoteEp);
            _remoteEndPoint = remoteEp;
            _isConnected = true;

            // 开始异步接收循环
            BeginReceive();
            _onConnected?.Invoke();
        }

        /// <summary>
        /// 发送原始数据。
        /// <para>同步发送，因为 Kcp.Output 回调通常在主线程执行。</para>
        /// </summary>
        /// <param name="data">要发送的数据。</param>
        /// <param name="offset">数据偏移量。</param>
        /// <param name="count">数据长度。</param>
        public void SendRaw(byte[] data, int offset, int count)
        {
            if (!_isConnected || _socket == null)
            {
                return;
            }

            try
            {
                _socket.Send(data, offset, count, SocketFlags.None);
            }
            catch (SocketException)
            {
                // 发送失败时关闭连接
                Close();
            }
        }

        /// <summary>
        /// 关闭传输连接并释放资源。
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
                    // 忽略关闭时的异常
                }
                catch (ObjectDisposedException)
                {
                    // 忽略关闭时的异常
                }

                try
                {
                    socket.Close();
                }
                catch (SocketException)
                {
                    // 忽略关闭时的异常
                }
                catch (ObjectDisposedException)
                {
                    // 忽略关闭时的异常
                }
            }

            _remoteEndPoint = null;

            // 触发关闭回调
            var handler = _onClosed;
            if (handler != null)
            {
                handler.Invoke();
            }
        }

        /// <summary>
        /// 设置数据接收回调。
        /// </summary>
        /// <param name="onDataReceived">数据接收回调，参数为数据数组和有效长度。</param>
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
        /// 释放资源，等同于调用 Close。
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 启动一次异步接收操作。
        /// </summary>
        private void BeginReceive()
        {
            var socket = _socket;
            if (!_isConnected || socket == null)
            {
                return;
            }

            var buffer = new byte[_receiveBufferSize];
            var remoteEp = new IPEndPoint(IPAddress.Any, 0);
            var state = new ReceiveState(buffer, remoteEp);

            try
            {
                socket.BeginReceiveFrom(
                    state.Buffer, 0, state.Buffer.Length,
                    SocketFlags.None,
                    ref state.RemoteEndPoint,
                    OnReceiveCallback,
                    state);
            }
            catch (SocketException)
            {
                // Socket 已关闭或发生错误
                Close();
            }
        }

        /// <summary>
        /// 异步接收回调。
        /// </summary>
        /// <param name="ar">异步操作结果。</param>
        private void OnReceiveCallback(IAsyncResult ar)
        {
            var socket = _socket;
            if (!_isConnected || socket == null)
            {
                return;
            }

            var state = (ReceiveState)ar.AsyncState;
            int bytesRead;

            try
            {
                bytesRead = socket.EndReceiveFrom(ar, ref state.RemoteEndPoint);
            }
            catch (SocketException)
            {
                // 接收失败，关闭连接
                Close();
                return;
            }
            catch (ObjectDisposedException)
            {
                // Socket 已被释放
                return;
            }

            if (bytesRead > 0)
            {
                // 触发数据接收回调
                var handler = _onDataReceived;
                if (handler != null)
                {
                    handler.Invoke(state.Buffer, bytesRead);
                }
            }

            // 继续下一次接收
            BeginReceive();
        }

        /// <summary>
        /// 异步接收操作的状态对象，用于传递缓冲区和远程端点。
        /// </summary>
        private class ReceiveState
        {
            /// <summary>
            /// 接收缓冲区。
            /// </summary>
            public byte[] Buffer;

            /// <summary>
            /// 远程端点（引用传递，BeginReceiveFrom 需要修改）。
            /// </summary>
            public EndPoint RemoteEndPoint;

            /// <summary>
            /// 初始化接收状态。
            /// </summary>
            /// <param name="buffer">接收缓冲区。</param>
            /// <param name="remoteEndPoint">远程端点。</param>
            public ReceiveState(byte[] buffer, EndPoint remoteEndPoint)
            {
                Buffer = buffer;
                RemoteEndPoint = remoteEndPoint;
            }
        }
    }
}
