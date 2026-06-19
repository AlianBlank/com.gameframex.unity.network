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
using System.Collections.Generic;
using System.Net.Sockets;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络管理器。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed partial class NetworkManager : GameFrameworkModule, INetworkManager
    {
        private readonly Dictionary<string, NetworkChannelBase> m_NetworkChannels;
        private readonly List<NetworkChannelBase> m_NetworkChannelSnapshot = new List<NetworkChannelBase>(4);

        private EventHandler<NetworkConnectedEventArgs> m_NetworkConnectedEventHandler;
        private EventHandler<NetworkClosedEventArgs> m_NetworkClosedEventHandler;
        private EventHandler<NetworkMissHeartBeatEventArgs> m_NetworkMissHeartBeatEventHandler;
        private EventHandler<NetworkErrorEventArgs> m_NetworkErrorEventHandler;

        private readonly object m_NetworkConnectedLock = new object();
        private readonly object m_NetworkClosedLock = new object();
        private readonly object m_NetworkMissHeartBeatLock = new object();
        private readonly object m_NetworkErrorLock = new object();

        /// <summary>
        /// 初始化网络管理器的新实例。
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public NetworkManager()
        {
            m_NetworkChannels = new Dictionary<string, NetworkChannelBase>(StringComparer.Ordinal);
            m_NetworkConnectedEventHandler = null;
            m_NetworkClosedEventHandler = null;
            m_NetworkMissHeartBeatEventHandler = null;
            m_NetworkErrorEventHandler = null;
        }

        /// <summary>
        /// 获取网络频道数量。
        /// </summary>
        public int NetworkChannelCount
        {
            get { return m_NetworkChannels.Count; }
        }

        /// <summary>
        /// 网络连接成功事件。
        /// </summary>
        public event EventHandler<NetworkConnectedEventArgs> NetworkConnected
        {
            add { m_NetworkConnectedEventHandler += value; }
            remove { m_NetworkConnectedEventHandler -= value; }
        }

        /// <summary>
        /// 网络连接关闭事件。
        /// </summary>
        public event EventHandler<NetworkClosedEventArgs> NetworkClosed
        {
            add { m_NetworkClosedEventHandler += value; }
            remove { m_NetworkClosedEventHandler -= value; }
        }

        /// <summary>
        /// 网络心跳包丢失事件。
        /// </summary>
        public event EventHandler<NetworkMissHeartBeatEventArgs> NetworkMissHeartBeat
        {
            add { m_NetworkMissHeartBeatEventHandler += value; }
            remove { m_NetworkMissHeartBeatEventHandler -= value; }
        }

        /// <summary>
        /// 网络错误事件。
        /// </summary>
        public event EventHandler<NetworkErrorEventArgs> NetworkError
        {
            add { m_NetworkErrorEventHandler += value; }
            remove { m_NetworkErrorEventHandler -= value; }
        }

        /// <summary>
        /// 网络管理器轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        protected override void Update(float elapseSeconds, float realElapseSeconds)
        {
            m_NetworkChannelSnapshot.Clear();
            m_NetworkChannelSnapshot.AddRange(m_NetworkChannels.Values);
            for (int i = 0; i < m_NetworkChannelSnapshot.Count; i++)
            {
                m_NetworkChannelSnapshot[i].Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 关闭并清理网络管理器。
        /// </summary>
        protected override void Shutdown()
        {
            foreach (var networkChannel in m_NetworkChannels)
            {
                var networkChannelBase = networkChannel.Value;
                UnregisterChannelEvents(networkChannelBase);
                networkChannelBase.Shutdown();
            }

            m_NetworkChannels.Clear();
        }

        /// <summary>
        /// 检查是否存在网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否存在网络频道。</returns>
        public bool HasNetworkChannel(string channelName)
        {
            return m_NetworkChannels.ContainsKey(channelName ?? string.Empty);
        }

        /// <summary>
        /// 获取网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>要获取的网络频道。</returns>
        public INetworkChannel GetNetworkChannel(string channelName)
        {
            if (m_NetworkChannels.TryGetValue(channelName ?? string.Empty, out var networkChannel))
            {
                return networkChannel;
            }

            return null;
        }

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <returns>所有网络频道。</returns>
        public INetworkChannel[] GetAllNetworkChannels()
        {
            var index = 0;
            var results = new INetworkChannel[m_NetworkChannels.Count];
            foreach (var networkChannel in m_NetworkChannels)
            {
                results[index++] = networkChannel.Value;
            }

            return results;
        }

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <param name="results">所有网络频道。</param>
        public void GetAllNetworkChannels(List<INetworkChannel> results)
        {
            GameFrameworkGuard.NotNull(results, nameof(results));

            results.Clear();
            foreach (var networkChannel in m_NetworkChannels)
            {
                results.Add(networkChannel.Value);
            }
        }

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <returns>要创建的网络频道。</returns>
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout)
        {
            return CreateNetworkChannel(channelName, networkChannelHelper, rpcTimeout, ServiceType.Tcp, null);
        }

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="kcpConfig">KCP配置。</param>
        /// <returns>要创建的网络频道。</returns>
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout, ServiceType serviceType, KcpConfig kcpConfig)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            GameFrameworkGuard.NotNull(networkChannelHelper, nameof(networkChannelHelper));

            if (HasNetworkChannel(channelName))
            {
                throw new GameFrameworkException(Utility.Text.Format("Already exist network channel '{0}'.", channelName ?? string.Empty));
            }

            NetworkChannelBase networkChannel;
            switch (serviceType)
            {
                case ServiceType.Kcp:
                case ServiceType.KcpUdp:
                case ServiceType.KcpTcp:
                case ServiceType.KcpWebSocket:
                {
                    networkChannel = new KcpNetworkChannel(channelName, networkChannelHelper, rpcTimeout, serviceType, kcpConfig);
                    break;
                }
#if (ENABLE_GAME_FRAME_X_WEB_SOCKET && UNITY_WEBGL) || FORCE_ENABLE_GAME_FRAME_X_WEB_SOCKET
                default:
                {
                    networkChannel = new WebSocketNetworkChannel(channelName, networkChannelHelper, rpcTimeout);
                    break;
                }
#else
                default:
                {
                    networkChannel = new SystemTcpNetworkChannel(channelName, networkChannelHelper, rpcTimeout);
                    break;
                }
#endif
            }

            RegisterChannelEvents(networkChannel);
            m_NetworkChannels.Add(channelName, networkChannel);
            return networkChannel;
        }

        /// <summary>
        /// 创建 KCP 网络频道（URI scheme 自动推断传输层）。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <param name="kcpConfig">KCP配置。</param>
        /// <returns>要创建的网络频道。</returns>
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout, KcpConfig kcpConfig)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            GameFrameworkGuard.NotNull(networkChannelHelper, nameof(networkChannelHelper));

            if (HasNetworkChannel(channelName))
            {
                throw new GameFrameworkException(Utility.Text.Format("Already exist network channel '{0}'.", channelName ?? string.Empty));
            }

            var networkChannel = new KcpNetworkChannel(channelName, networkChannelHelper, rpcTimeout, kcpConfig);
            RegisterChannelEvents(networkChannel);
            m_NetworkChannels.Add(channelName, networkChannel);
            return networkChannel;
        }

        /// <summary>
        /// 销毁网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否销毁网络频道成功。</returns>
        public bool DestroyNetworkChannel(string channelName)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            if (m_NetworkChannels.TryGetValue(channelName, out var networkChannel))
            {
                UnregisterChannelEvents(networkChannel);
                networkChannel.Shutdown();
                return m_NetworkChannels.Remove(channelName);
            }

            return false;
        }

        /// <summary>
        /// 设置是否开启心跳包失去焦点时也发送心跳包
        /// </summary>
        /// <param name="hasFocus">是否开启心跳包失去焦点时也发送心跳包</param>
        public void SetFocusHeartbeat(bool hasFocus)
        {
            foreach (var networkChannel in m_NetworkChannels.Values)
            {
                networkChannel.SetFocusHeartbeat(hasFocus);
            }
        }

        /// <summary>
        /// 订阅网络频道的四个事件回调。
        /// </summary>
        /// <param name="networkChannel">网络频道。</param>
        private void RegisterChannelEvents(NetworkChannelBase networkChannel)
        {
            networkChannel.NetworkChannelConnected += OnNetworkChannelConnected;
            networkChannel.NetworkChannelClosed += OnNetworkChannelClosed;
            networkChannel.NetworkChannelMissHeartBeat += OnNetworkChannelMissHeartBeat;
            networkChannel.NetworkChannelError += OnNetworkChannelError;
        }

        /// <summary>
        /// 退订网络频道的四个事件回调。
        /// </summary>
        /// <param name="networkChannel">网络频道。</param>
        private void UnregisterChannelEvents(NetworkChannelBase networkChannel)
        {
            networkChannel.NetworkChannelConnected -= OnNetworkChannelConnected;
            networkChannel.NetworkChannelClosed -= OnNetworkChannelClosed;
            networkChannel.NetworkChannelMissHeartBeat -= OnNetworkChannelMissHeartBeat;
            networkChannel.NetworkChannelError -= OnNetworkChannelError;
        }

        private void OnNetworkChannelConnected(NetworkChannelBase networkChannel, object userData)
        {
            if (m_NetworkConnectedEventHandler != null)
            {
                lock (m_NetworkConnectedLock)
                {
                    NetworkConnectedEventArgs networkConnectedEventArgs = NetworkConnectedEventArgs.Create(networkChannel, userData);
                    m_NetworkConnectedEventHandler(this, networkConnectedEventArgs);
                    ReferencePool.Release(networkConnectedEventArgs);
                }
            }
        }

        private void OnNetworkChannelClosed(NetworkChannelBase networkChannel, string reason, ushort errorCode)
        {
            if (m_NetworkClosedEventHandler != null)
            {
                lock (m_NetworkClosedLock)
                {
                    NetworkClosedEventArgs networkClosedEventArgs = NetworkClosedEventArgs.Create(networkChannel, reason, errorCode);
                    m_NetworkClosedEventHandler(this, networkClosedEventArgs);
                    ReferencePool.Release(networkClosedEventArgs);
                }
            }
        }

        private void OnNetworkChannelMissHeartBeat(NetworkChannelBase networkChannel, int missHeartBeatCount)
        {
            if (m_NetworkMissHeartBeatEventHandler != null)
            {
                lock (m_NetworkMissHeartBeatLock)
                {
                    NetworkMissHeartBeatEventArgs networkMissHeartBeatEventArgs = NetworkMissHeartBeatEventArgs.Create(networkChannel, missHeartBeatCount);
                    m_NetworkMissHeartBeatEventHandler(this, networkMissHeartBeatEventArgs);
                    ReferencePool.Release(networkMissHeartBeatEventArgs);
                }
            }
        }

        private void OnNetworkChannelError(NetworkChannelBase networkChannel, NetworkErrorCode errorCode, SocketError socketErrorCode, string errorMessage)
        {
            if (m_NetworkErrorEventHandler != null)
            {
                lock (m_NetworkErrorLock)
                {
                    NetworkErrorEventArgs networkErrorEventArgs = NetworkErrorEventArgs.Create(networkChannel, errorCode, socketErrorCode, errorMessage);
                    m_NetworkErrorEventHandler(this, networkErrorEventArgs);
                    ReferencePool.Release(networkErrorEventArgs);
                }
            }
        }
    }
}
