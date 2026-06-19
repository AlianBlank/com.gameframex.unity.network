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

using System.Collections.Generic;
using GameFrameX.Event.Runtime;
using GameFrameX.Runtime;
using UnityEngine;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络组件。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("GameFrameX/Network")]
    [UnityEngine.Scripting.Preserve]
    public sealed class NetworkComponent : GameFrameworkComponent
    {
        private INetworkManager m_NetworkManager = null;
        private EventComponent m_EventComponent = null;

        /// <summary>
        /// 忽略发送的网络消息ID的日志打印
        /// </summary>
        [SerializeField] private List<int> m_IgnoredSendNetworkIds = new List<int>();

        /// <summary>
        /// 忽略接收的网络消息ID的日志打印
        /// </summary>
        [SerializeField] private List<int> m_IgnoredReceiveNetworkIds = new List<int>();

        /// <summary>
        /// RPC超时时间，以毫秒为单位,默认为5秒
        /// </summary>
        [SerializeField] private int m_rpcTimeout = 5000;

        /// <summary>
        /// 是否在应用程序获得焦点时发送心跳包,默认为false
        /// </summary>
        [SerializeField] private bool m_FocusHeartbeat = false;

        /// <summary>
        /// 获取网络频道数量。
        /// </summary>
        public int NetworkChannelCount
        {
            get { return m_NetworkManager.NetworkChannelCount; }
        }


        /// <summary>
        /// 应用程序获得或失去焦点时调用。
        /// </summary>
        /// <param name="hasFocus">应用程序是否获得焦点。</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!m_FocusHeartbeat)
            {
                return;
            }

            m_NetworkManager.SetFocusHeartbeat(hasFocus);
            Log.Info($"SetFocusHeartbeat: {hasFocus}");
        }

        /// <summary>
        /// 游戏框架组件初始化。
        /// </summary>
        protected override void Awake()
        {
            ImplementationComponentType = Utility.Assembly.GetType(componentType);
            InterfaceComponentType = typeof(INetworkManager);
            base.Awake();
            m_NetworkManager = GameFrameworkEntry.GetModule<INetworkManager>();
            if (m_NetworkManager == null)
            {
                Log.Fatal("Network manager is invalid.");
                return;
            }

            m_NetworkManager.NetworkConnected += OnNetworkConnected;
            m_NetworkManager.NetworkClosed += OnNetworkClosed;
            m_NetworkManager.NetworkMissHeartBeat += OnNetworkMissHeartBeat;
            m_NetworkManager.NetworkError += OnNetworkError;
        }

        private void Start()
        {
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            if (m_EventComponent == null)
            {
                Log.Fatal("Event component is invalid.");
                return;
            }
        }

        /// <summary>
        /// 检查是否存在网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否存在网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public bool HasNetworkChannel(string channelName)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            return m_NetworkManager.HasNetworkChannel(channelName);
        }

        /// <summary>
        /// 获取网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>要获取的网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public INetworkChannel GetNetworkChannel(string channelName)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            return m_NetworkManager.GetNetworkChannel(channelName);
        }

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <returns>所有网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public INetworkChannel[] GetAllNetworkChannels()
        {
            return m_NetworkManager.GetAllNetworkChannels();
        }

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <param name="results">所有网络频道。</param>
        [UnityEngine.Scripting.Preserve]
        public void GetAllNetworkChannels(List<INetworkChannel> results)
        {
            m_NetworkManager.GetAllNetworkChannels(results);
        }

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <returns>要创建的网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            var networkChannel = m_NetworkManager.CreateNetworkChannel(channelName, networkChannelHelper, m_rpcTimeout);
            networkChannel.SetIgnoreLogNetworkIds(m_IgnoredSendNetworkIds, m_IgnoredReceiveNetworkIds);
            return networkChannel;
        }

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="kcpConfig">KCP配置，当 serviceType 为 KCP 类型时使用。</param>
        /// <returns>要创建的网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, ServiceType serviceType, KcpConfig kcpConfig = null)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            var networkChannel = m_NetworkManager.CreateNetworkChannel(channelName, networkChannelHelper, m_rpcTimeout, serviceType, kcpConfig);
            networkChannel.SetIgnoreLogNetworkIds(m_IgnoredSendNetworkIds, m_IgnoredReceiveNetworkIds);
            return networkChannel;
        }

        /// <summary>
        /// 创建 KCP 网络频道（URI scheme 自动推断传输层）。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="kcpConfig">KCP配置。</param>
        /// <returns>要创建的网络频道。</returns>
        [UnityEngine.Scripting.Preserve]
        public INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, KcpConfig kcpConfig)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            var networkChannel = m_NetworkManager.CreateNetworkChannel(channelName, networkChannelHelper, m_rpcTimeout, kcpConfig);
            networkChannel.SetIgnoreLogNetworkIds(m_IgnoredSendNetworkIds, m_IgnoredReceiveNetworkIds);
            return networkChannel;
        }

        /// <summary>
        /// 销毁网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否销毁网络频道成功。</returns>
        [UnityEngine.Scripting.Preserve]
        public bool DestroyNetworkChannel(string channelName)
        {
            GameFrameworkGuard.NotNullOrEmpty(channelName, nameof(channelName));
            return m_NetworkManager.DestroyNetworkChannel(channelName);
        }

        private void OnNetworkConnected(object sender, NetworkConnectedEventArgs eventArgs)
        {
            if (m_EventComponent == null)
            {
                return;
            }

            var networkConnectedEventArgs = NetworkConnectedEventArgs.Create(eventArgs.NetworkChannel, eventArgs.UserData);
            m_EventComponent.Fire(this, networkConnectedEventArgs);
        }

        private void OnNetworkClosed(object sender, NetworkClosedEventArgs eventArgs)
        {
            if (m_EventComponent == null)
            {
                return;
            }

            var networkClosedEventArgs = NetworkClosedEventArgs.Create(eventArgs.NetworkChannel, eventArgs.Reason, eventArgs.ErrorCode);
            m_EventComponent.Fire(this, networkClosedEventArgs);
        }

        private void OnNetworkMissHeartBeat(object sender, NetworkMissHeartBeatEventArgs eventArgs)
        {
            if (m_EventComponent == null)
            {
                return;
            }

            var networkMissHeartBeatEventArgs = NetworkMissHeartBeatEventArgs.Create(eventArgs.NetworkChannel, eventArgs.MissCount);
            m_EventComponent.Fire(this, networkMissHeartBeatEventArgs);
        }

        private void OnNetworkError(object sender, NetworkErrorEventArgs eventArgs)
        {
            if (m_EventComponent == null)
            {
                return;
            }

            var networkErrorEventArgs = NetworkErrorEventArgs.Create(eventArgs.NetworkChannel, eventArgs.ErrorCode, eventArgs.SocketErrorCode, eventArgs.ErrorMessage);
            m_EventComponent.Fire(this, networkErrorEventArgs);
        }
    }
}