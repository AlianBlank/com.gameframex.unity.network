using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// HTTP消息包装基类
    /// </summary>
    public class MessageHttpObject
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 消息序列号
        /// </summary>
        public int UniqueId { get; set; }

        [GameFrameX.LitJSON.Runtime.JsonIgnore]
        public byte[] Body { get; set; }

        public override string ToString()
        {
            return Utility.Json.ToJson(this);
        }
    }
}
