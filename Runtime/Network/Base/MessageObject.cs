using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 消息基类
    /// </summary>
    public class MessageObject : IReference
    {
        /// <summary>
        /// 消息唯一编号
        /// </summary>
        [GameFrameX.LitJSON.Runtime.JsonIgnore]
        public int UniqueId { get; private set; }

        protected MessageObject()
        {
            UpdateUniqueId();
        }

        /// <summary>
        /// 更新唯一编码
        /// </summary>
        public void UpdateUniqueId()
        {
            UniqueId = Utility.IdGenerator.GetNextUniqueIntId();
        }

        /// <summary>
        /// 设置唯一编码
        /// </summary>
        public void SetUpdateUniqueId(int uniqueId)
        {
            UniqueId = uniqueId;
        }

        public override string ToString()
        {
            return Utility.Json.ToJson(this);
        }

        /// <summary>
        /// 清理引用。
        /// </summary>
        public virtual void Clear()
        {
        }
    }
}
