using System;

namespace NiumaMiniGame.Protocol
{
    /// <summary>
    /// 实时消息信封。
    /// payload 保存原始 JSON 文本，真实 Netty 序列化层需要把它写入后端 payload 节点。
    /// </summary>
    [Serializable]
    public sealed class RealtimeMessageEnvelope
    {
        public const int CurrentVersion = 1;

        public int protocolVersion = CurrentVersion;
        public string messageType;
        public string roomId;
        public string playerId;
        public string sessionId;
        public long clientSequence;
        public long serverSequence;
        public long clientTimeMs;
        public long serverTimeMs;
        public string payload;

        public RealtimeMessageEnvelope()
        {
        }

        public RealtimeMessageEnvelope(string messageType)
        {
            this.messageType = messageType;
        }

        /// <summary>
        /// 只校验信封最基本的协议字段，业务字段由具体消息自行校验。
        /// </summary>
        public bool IsValid()
        {
            return protocolVersion == CurrentVersion
                   && !string.IsNullOrWhiteSpace(messageType);
        }
    }

    /// <summary>
    /// 网络协议 DTO 标记接口。
    /// 它不提供行为，只用于限制 SendReliable / SendUnreliable 的泛型参数。
    /// </summary>
    public interface IRealtimeMessage
    {
    }
}
