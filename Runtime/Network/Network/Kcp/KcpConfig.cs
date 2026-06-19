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

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// KCP 配置。
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.Preserve]
    public sealed class KcpConfig
    {
        /// <summary>
        /// 是否启用无延迟模式。
        /// </summary>
        public bool NoDelay = true;

        /// <summary>
        /// KCP 内部 Update 间隔，以毫秒为单位。
        /// </summary>
        public uint Interval = 10;

        /// <summary>
        /// 快速重传阈值。
        /// </summary>
        public int Resend = 2;

        /// <summary>
        /// 是否关闭拥塞控制。
        /// </summary>
        public bool NoCongestionControl = true;

        /// <summary>
        /// 最大传输单元。
        /// </summary>
        public uint Mtu = 1200;

        /// <summary>
        /// 发送窗口大小。
        /// </summary>
        public uint SendWindowSize = 256;

        /// <summary>
        /// 接收窗口大小。
        /// </summary>
        public uint ReceiveWindowSize = 256;

        /// <summary>
        /// 最大单条 KCP 消息大小。
        /// </summary>
        public int MaxMessageSize = 256 * 1024;

        /// <summary>
        /// KCP 会话 ID。
        /// <para>必须与服务端约定一致，否则 KCP 段无法配对。</para>
        /// <para>默认 0 适用于单通道场景；客户端同时创建多个 KCP 通道时需要为每个通道配置不同的值。</para>
        /// </summary>
        public uint ConversationId = 0;
    }
}
