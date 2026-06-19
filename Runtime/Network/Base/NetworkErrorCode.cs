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

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络关闭原因。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class NetworkCloseReason
    {
        /// <summary>
        /// 正常关闭。
        /// </summary>
        public const string Normal = "Normal";

        /// <summary>
        /// 超时关闭。
        /// </summary>
        public const string Timeout = "Timeout";

        /// <summary>
        /// 资源释放关闭。
        /// </summary>
        public const string Dispose = "Dispose";

        /// <summary>
        /// 连接关闭。
        /// </summary>
        public const string ConnectClose = "ConnectClose";

        /// <summary>
        /// 连接地址错误关闭。
        /// </summary>
        public const string ConnectAddressError = "ConnectAddressError";

        /// <summary>
        /// 连接地址异常错误关闭。
        /// </summary>
        public const string ConnectAddressExceptionError = "ConnectAddressExceptionError";

        /// <summary>
        /// 缺失心跳关闭。
        /// </summary>
        public const string MissHeartBeat = "MissHeartBeat";

        /// <summary>
        /// 服务器踢人关闭。
        /// </summary>
        public const string ServerKick = "ServerKick";

        /// <summary>
        /// 重复登录关闭。
        /// </summary>
        public const string DuplicateLogin = "DuplicateLogin";

        /// <summary>
        /// 会话替换关闭。
        /// </summary>
        public const string SessionReplaced = "SessionReplaced";

        /// <summary>
        /// 账号封禁关闭。
        /// </summary>
        public const string AccountBanned = "AccountBanned";

        /// <summary>
        /// 管理员踢人关闭。
        /// </summary>
        public const string AdminKick = "AdminKick";

        /// <summary>
        /// 会话过期关闭。
        /// </summary>
        public const string SessionExpired = "SessionExpired";
    }

    /// <summary>
    /// 网络错误码。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public enum NetworkErrorCode : byte
    {
        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 地址族错误。
        /// </summary>
        AddressFamilyError,

        /// <summary>
        /// Socket 错误。
        /// </summary>
        SocketError,

        /// <summary>
        /// 连接错误。
        /// </summary>
        ConnectError,

        /// <summary>
        /// 发送错误。
        /// </summary>
        SendError,

        /// <summary>
        /// 接收错误。
        /// </summary>
        ReceiveError,

        /// <summary>
        /// 序列化错误。
        /// </summary>
        SerializeError,

        /// <summary>
        /// 反序列化消息包头错误。
        /// </summary>
        DeserializePacketHeaderError,

        /// <summary>
        /// 反序列化消息包错误。
        /// </summary>
        DeserializePacketError,

        /// <summary>
        /// 缺失心跳错误。
        /// </summary>
        MissHeartBeatError,

        /// <summary>
        /// 资源释放错误。
        /// </summary>
        DisposeError,

        /// <summary>
        /// 服务器踢人错误。
        /// </summary>
        ServerKickError,

        /// <summary>
        /// 重复登录错误。
        /// </summary>
        DuplicateLoginError,

        /// <summary>
        /// 会话替换错误。
        /// </summary>
        SessionReplacedError,

        /// <summary>
        /// 账号封禁错误。
        /// </summary>
        AccountBannedError,

        /// <summary>
        /// 管理员踢人错误。
        /// </summary>
        AdminKickError,

        /// <summary>
        /// 会话过期错误。
        /// </summary>
        SessionExpiredError,
    }
}
