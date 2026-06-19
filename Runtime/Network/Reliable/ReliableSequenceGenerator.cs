// ==========================================================================================
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// ==========================================================================================

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 可靠序号生成器。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class ReliableSequenceGenerator
    {
        private ulong m_NextSequence;

        public ReliableSequenceGenerator()
        {
            Reset(ReliableFifoServerContract.InitialReliableSequence);
        }

        /// <summary>
        /// 获取下一条将要分配的可靠序号，但不推进状态。
        /// </summary>
        public ulong PeekNext()
        {
            return m_NextSequence;
        }

        /// <summary>
        /// 分配并推进到下一条可靠序号。
        /// </summary>
        public ulong Next()
        {
            var currentSequence = m_NextSequence;
            m_NextSequence++;
            return currentSequence;
        }

        /// <summary>
        /// 重置到指定序号。
        /// </summary>
        public void Reset(ulong nextSequence)
        {
            m_NextSequence = nextSequence;
        }
    }
}
