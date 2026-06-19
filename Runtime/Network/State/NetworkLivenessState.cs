// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 网络活性状态。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public enum NetworkLivenessState
    {
        Connected = 0,
        SuspectDisconnected = 1,
        SilentReconnecting = 2,
        Resuming = 3,
        BusinessReconnecting = 4,
        Closed = 5
    }

    /// <summary>
    /// 统一活性状态机输入事件。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class NetworkLivenessInputEvent
    {
        public const string TransportConnected = "TransportConnected";
        public const string TransportDisconnected = "TransportDisconnected";
        public const string TransportError = "TransportError";
        public const string ClosedByUser = "ClosedByUser";
        public const string PacketReceived = "PacketReceived";
        public const string HeartbeatAckReceived = "HeartbeatAckReceived";
        public const string AckReceived = "AckReceived";
        public const string ResumeStarted = "ResumeStarted";
        public const string HeartbeatTimeout = "HeartbeatTimeout";
        public const string NoPacketTimeout = "NoPacketTimeout";
        public const string ResumeAccepted = "ResumeAccepted";
        public const string ResumeRejected = "ResumeRejected";
    }

    /// <summary>
    /// 网络活性状态切换原因。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class NetworkLivenessReason
    {
        public const string TransportConnected = "TransportConnected";
        public const string TransportDisconnected = "TransportDisconnected";
        public const string TransportError = "TransportError";
        public const string ClosedByUser = "ClosedByUser";
        public const string PacketReceived = "PacketReceived";
        public const string HeartbeatAckReceived = "HeartbeatAckReceived";
        public const string AckReceived = "AckReceived";
        public const string ResumeStarted = "ResumeStarted";
        public const string HeartbeatTimeout = "HeartbeatTimeout";
        public const string NoPacketTimeout = "NoPacketTimeout";
        public const string ResumeAccepted = "ResumeAccepted";
        public const string ResumeRejected = "ResumeRejected";
        public const string ServerKick = "ServerKick";
        public const string DuplicateLogin = "DuplicateLogin";
        public const string SessionReplaced = "SessionReplaced";
        public const string AccountBanned = "AccountBanned";
        public const string AdminKick = "AdminKick";
        public const string SessionExpired = "SessionExpired";
    }
}
