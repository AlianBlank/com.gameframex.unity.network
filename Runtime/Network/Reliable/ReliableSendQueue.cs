// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

using System;
using System.Collections.Generic;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 可靠发送 FIFO 队列。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class ReliableSendQueue
    {
        private readonly LinkedList<PendingReliableMessage> m_PendingMessages = new LinkedList<PendingReliableMessage>();

        public int Count
        {
            get { return m_PendingMessages.Count; }
        }

        public int TotalPayloadBytes { get; private set; }

        public void Enqueue(PendingReliableMessage pendingMessage)
        {
            if (pendingMessage == null)
            {
                throw new ArgumentNullException(nameof(pendingMessage));
            }

            m_PendingMessages.AddLast(pendingMessage);
            TotalPayloadBytes += pendingMessage.PayloadByteCount;
        }

        public bool CanEnqueue(PendingReliableMessage pendingMessage)
        {
            if (pendingMessage == null)
            {
                throw new ArgumentNullException(nameof(pendingMessage));
            }

            return Count < ReliableFifoProtocolOptions.PendingQueueMaxCount &&
                   TotalPayloadBytes + pendingMessage.PayloadByteCount <= ReliableFifoProtocolOptions.PendingQueueMaxBytes;
        }

        public bool TryPeek(out PendingReliableMessage pendingMessage)
        {
            if (m_PendingMessages.First == null)
            {
                pendingMessage = null;
                return false;
            }

            pendingMessage = m_PendingMessages.First.Value;
            return true;
        }

        public PendingReliableMessage[] Snapshot()
        {
            var messages = new PendingReliableMessage[m_PendingMessages.Count];
            var index = 0;
            var node = m_PendingMessages.First;
            while (node != null)
            {
                messages[index++] = node.Value;
                node = node.Next;
            }

            return messages;
        }

        public int AcknowledgeThrough(ulong ackSequence)
        {
            var acknowledgedCount = 0;
            while (m_PendingMessages.First != null &&
                   m_PendingMessages.First.Value.ReliableSequence <= ackSequence)
            {
                TotalPayloadBytes -= m_PendingMessages.First.Value.PayloadByteCount;
                m_PendingMessages.RemoveFirst();
                acknowledgedCount++;
            }

            return acknowledgedCount;
        }

        public void Clear()
        {
            m_PendingMessages.Clear();
            TotalPayloadBytes = 0;
        }
    }
}
