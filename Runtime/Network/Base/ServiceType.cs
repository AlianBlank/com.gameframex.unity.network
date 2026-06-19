namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络服务类型。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public enum ServiceType : byte
    {
        /// <summary>
        /// TCP 网络服务。
        /// </summary>
        Tcp = 0,

        /// <summary>
        /// 使用同步接收的 TCP 网络服务。
        /// </summary>
        TcpWithSyncReceive = 1,

        /// <summary>
        /// UDP 网络服务。
        /// </summary>
        Udp = 2,

        /// <summary>
        /// KCP over UDP 网络服务。
        /// </summary>
        Kcp = 3,

        /// <summary>
        /// WebSocket 网络服务。
        /// </summary>
        WebSocket = 4,

        /// <summary>
        /// KCP over UDP 网络服务。
        /// </summary>
        KcpUdp = 5,

        /// <summary>
        /// KCP over TCP 网络服务。
        /// </summary>
        KcpTcp = 6,

        /// <summary>
        /// KCP over WebSocket 网络服务。
        /// </summary>
        KcpWebSocket = 7,
    }
}
