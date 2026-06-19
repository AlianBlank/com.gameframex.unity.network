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

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络管理器接口。
    /// </summary>
    public interface INetworkManager
    {
        /// <summary>
        /// 获取网络频道数量。
        /// </summary>
        int NetworkChannelCount { get; }

        /// <summary>
        /// 网络连接成功事件。
        /// </summary>
        event EventHandler<NetworkConnectedEventArgs> NetworkConnected;

        /// <summary>
        /// 网络连接关闭事件。
        /// </summary>
        event EventHandler<NetworkClosedEventArgs> NetworkClosed;

        /// <summary>
        /// 网络心跳包丢失事件。
        /// </summary>
        event EventHandler<NetworkMissHeartBeatEventArgs> NetworkMissHeartBeat;

        /// <summary>
        /// 网络错误事件。
        /// </summary>
        event EventHandler<NetworkErrorEventArgs> NetworkError;

        /// <summary>
        /// 检查是否存在网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否存在网络频道。</returns>
        bool HasNetworkChannel(string channelName);

        /// <summary>
        /// 获取网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>要获取的网络频道。</returns>
        INetworkChannel GetNetworkChannel(string channelName);

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <returns>所有网络频道。</returns>
        INetworkChannel[] GetAllNetworkChannels();

        /// <summary>
        /// 获取所有网络频道。
        /// </summary>
        /// <param name="results">所有网络频道。</param>
        void GetAllNetworkChannels(List<INetworkChannel> results);

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <returns>要创建的网络频道。</returns>
        INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout);

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="kcpConfig">KCP配置，当 serviceType 为 KCP 类型时使用。</param>
        /// <returns>要创建的网络频道。</returns>
        INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout, ServiceType serviceType, KcpConfig kcpConfig);

        /// <summary>
        /// 创建 KCP 网络频道（URI scheme 自动推断传输层）。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <param name="networkChannelHelper">网络频道辅助器。</param>
        /// <param name="rpcTimeout">RPC超时时间</param>
        /// <param name="kcpConfig">KCP配置。</param>
        /// <returns>要创建的网络频道。</returns>
        INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout, KcpConfig kcpConfig);

        /// <summary>
        /// 销毁网络频道。
        /// </summary>
        /// <param name="channelName">网络频道名称。</param>
        /// <returns>是否销毁网络频道成功。</returns>
        bool DestroyNetworkChannel(string channelName);

        /// <summary>
        /// 设置是否在应用程序获得焦点时发送心跳包。
        /// </summary>
        /// <param name="hasFocus">是否在应用程序获得焦点时发送心跳包。</param>
        void SetFocusHeartbeat(bool hasFocus);
    }
}