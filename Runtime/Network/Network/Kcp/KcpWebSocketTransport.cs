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

#if ENABLE_GAME_FRAME_X_WEB_SOCKET
using System;
using UnityWebSocket;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// KCP over WebSocket 传输层实现。
    /// WebSocket 是消息模式，无需额外的帧协议封装。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class KcpWebSocketTransport : IKcpTransport
    {
        /// <summary>
        /// WebSocket 客户端实例
        /// </summary>
        private IWebSocket _webSocket;

        /// <summary>
        /// 数据接收回调
        /// </summary>
        private Action<byte[], int> _onDataReceived;

        /// <summary>
        /// 连接成功回调
        /// </summary>
        private Action _onConnected;

        /// <summary>
        /// 连接关闭回调
        /// </summary>
        private Action _onClosed;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        public bool IsConnected
        {
            get
            {
                return _webSocket != null && _webSocket.IsConnected;
            }
        }

        /// <summary>
        /// 连接到指定的 WebSocket 服务器。
        /// 支持 kcp-ws:// 和 kcp-wss:// 协议前缀，会自动转换为 ws:// 和 wss://。
        /// </summary>
        /// <param name="address">服务器地址</param>
        public void Connect(Uri address)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KcpWebSocketTransport));
            }

            if (_webSocket != null && _webSocket.IsConnected)
            {
                return;
            }

            // 协议转换：kcp-ws:// -> ws://, kcp-wss:// -> wss://
            string scheme = address.Scheme;
            if (scheme == "kcp-ws")
            {
                scheme = "ws";
            }
            else if (scheme == "kcp-wss")
            {
                scheme = "wss";
            }

            UriBuilder uriBuilder = new UriBuilder(address)
            {
                Scheme = scheme
            };
            string wsUrl = uriBuilder.Uri.ToString();

            _webSocket = new WebSocket(wsUrl);
            _webSocket.OnOpen += OnOpen;
            _webSocket.OnMessage += OnMessage;
            _webSocket.OnClose += OnClose;
            _webSocket.OnError += OnError;
            _webSocket.ConnectAsync();
        }

        /// <summary>
        /// 发送原始字节数据。
        /// </summary>
        /// <param name="data">数据缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">发送长度</param>
        public void SendRaw(byte[] data, int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KcpWebSocketTransport));
            }

            if (_webSocket == null || !_webSocket.IsConnected)
            {
                return;
            }

            // WebSocket 消息模式下，如果偏移为 0 且长度等于数据长度，直接发送
            if (offset == 0 && count == data.Length)
            {
                _webSocket.SendAsync(data);
            }
            else
            {
                // 截取需要发送的数据片段
                byte[] segment = new byte[count];
                Buffer.BlockCopy(data, offset, segment, 0, count);
                _webSocket.SendAsync(segment);
            }
        }

        /// <summary>
        /// 关闭 WebSocket 连接。
        /// </summary>
        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            if (_webSocket != null)
            {
                _webSocket.CloseAsync();
            }
        }

        /// <summary>
        /// 设置数据接收回调。
        /// </summary>
        /// <param name="onDataReceived">数据接收回调，参数为字节数组和有效数据长度</param>
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
        /// <param name="onClosed">连接关闭回调</param>
        public void SetOnClosed(Action onClosed)
        {
            _onClosed = onClosed;
        }

        private void OnOpen(object sender, OpenEventArgs e)
        {
            _onConnected?.Invoke();
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsBinary)
            {
                _onDataReceived?.Invoke(e.RawData, e.RawData.Length);
            }
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            _onClosed?.Invoke();
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            UnityEngine.Debug.LogError($"[KcpWebSocketTransport] WebSocket error: {e.Message}");
            _onClosed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_webSocket != null)
            {
                _webSocket.OnOpen -= OnOpen;
                _webSocket.OnMessage -= OnMessage;
                _webSocket.OnClose -= OnClose;
                _webSocket.OnError -= OnError;
                _webSocket.CloseAsync();
                _webSocket = null;
            }

            _onDataReceived = null;
            _onConnected = null;
            _onClosed = null;
        }
    }
}
#endif
