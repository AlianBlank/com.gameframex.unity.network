// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

using System;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 待确认的可靠消息。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class PendingReliableMessage
    {
        private readonly byte[] m_Payload;

        private PendingReliableMessage(
            ulong sessionId,
            ulong reliableSequence,
            ulong ackSequence,
            MessageObject messageObject,
            byte[] payload,
            float enqueuedTimeSeconds)
        {
            SessionId = sessionId;
            ReliableSequence = reliableSequence;
            AckSequence = ackSequence;
            MessageObject = messageObject;
            m_Payload = payload;
            PayloadByteCount = payload.Length;
            EnqueuedTimeSeconds = enqueuedTimeSeconds;
            LastSendTimeSeconds = enqueuedTimeSeconds;
            RetryCount = 0;
        }

        public ulong SessionId { get; }

        public ulong ReliableSequence { get; }

        public ulong AckSequence { get; }

        public MessageObject MessageObject { get; }

        public byte[] Payload
        {
            get
            {
                var copy = new byte[m_Payload.Length];
                Buffer.BlockCopy(m_Payload, 0, copy, 0, m_Payload.Length);
                return copy;
            }
        }

        public int PayloadByteCount { get; }

        public float EnqueuedTimeSeconds { get; }

        public float LastSendTimeSeconds { get; private set; }

        public int RetryCount { get; private set; }

        public static PendingReliableMessage Create(
            ulong sessionId,
            ulong reliableSequence,
            ulong ackSequence,
            MessageObject messageObject,
            byte[] payload,
            float enqueuedTimeSeconds)
        {
            if (messageObject == null)
            {
                throw new ArgumentNullException(nameof(messageObject));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var payloadCopy = new byte[payload.Length];
            Buffer.BlockCopy(payload, 0, payloadCopy, 0, payload.Length);
            return new PendingReliableMessage(sessionId, reliableSequence, ackSequence, messageObject, payloadCopy, enqueuedTimeSeconds);
        }

        public void MarkRetried(float sendTimeSeconds)
        {
            RetryCount++;
            LastSendTimeSeconds = sendTimeSeconds;
        }
    }
}
