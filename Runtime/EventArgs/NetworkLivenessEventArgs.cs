// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

using GameFrameX.Event.Runtime;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 统一网络活性事件。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class NetworkLivenessEventArgs : GameEventArgs
    {
        public static readonly string EventId = typeof(NetworkLivenessEventArgs).FullName;

        public override string Id => EventId;

        public string ChannelName { get; private set; }
        public string InputEvent { get; private set; }
        public NetworkLivenessState State { get; private set; }
        public string Reason { get; private set; }
        public string TransportType { get; private set; }
        public ulong SessionId { get; private set; }
        public ulong LastAckSequence { get; private set; }
        public int PendingCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public bool Recoverable { get; private set; }
        public int ErrorCode { get; private set; }
        public string Message { get; private set; }

        public static NetworkLivenessEventArgs Create(
            string channelName,
            string inputEvent,
            NetworkLivenessState state,
            string reason,
            string transportType,
            ulong sessionId,
            ulong lastAckSequence,
            int pendingCount,
            float elapsedSeconds,
            bool recoverable,
            int errorCode,
            string message)
        {
            var eventArgs = ReferencePool.Acquire<NetworkLivenessEventArgs>();
            eventArgs.ChannelName = channelName;
            eventArgs.InputEvent = inputEvent;
            eventArgs.State = state;
            eventArgs.Reason = reason;
            eventArgs.TransportType = transportType;
            eventArgs.SessionId = sessionId;
            eventArgs.LastAckSequence = lastAckSequence;
            eventArgs.PendingCount = pendingCount;
            eventArgs.ElapsedSeconds = elapsedSeconds;
            eventArgs.Recoverable = recoverable;
            eventArgs.ErrorCode = errorCode;
            eventArgs.Message = message;
            return eventArgs;
        }

        public override void Clear()
        {
            ChannelName = null;
            InputEvent = null;
            State = NetworkLivenessState.Connected;
            Reason = null;
            TransportType = null;
            SessionId = 0ul;
            LastAckSequence = 0ul;
            PendingCount = 0;
            ElapsedSeconds = 0f;
            Recoverable = false;
            ErrorCode = 0;
            Message = null;
        }
    }
}
