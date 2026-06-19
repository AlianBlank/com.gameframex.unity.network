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
    /// KCP 传输层接口。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public interface IKcpTransport : IDisposable
    {
        /// <summary>
        /// 获取是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接到远程端点。
        /// </summary>
        /// <param name="address">远程地址。</param>
        void Connect(Uri address);

        /// <summary>
        /// 发送原始数据。
        /// </summary>
        /// <param name="data">要发送的数据。</param>
        /// <param name="offset">数据偏移量。</param>
        /// <param name="count">数据长度。</param>
        void SendRaw(byte[] data, int offset, int count);

        /// <summary>
        /// 关闭传输连接。
        /// </summary>
        void Close();

        /// <summary>
        /// 设置连接成功回调。
        /// </summary>
        /// <param name="onConnected">连接成功回调。</param>
        void SetOnConnected(Action onConnected);

        /// <summary>
        /// 设置数据接收回调。
        /// </summary>
        /// <param name="onDataReceived">数据接收回调。</param>
        void SetOnDataReceived(Action<byte[], int> onDataReceived);

        /// <summary>
        /// 设置连接关闭回调。
        /// </summary>
        /// <param name="onClosed">连接关闭回调。</param>
        void SetOnClosed(Action onClosed);
    }
}
