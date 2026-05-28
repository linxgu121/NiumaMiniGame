using NiumaMiniGame.Enum;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Network
{
    /// <summary>
    /// 网络层收到的消息。
    /// PayloadJson 是已解出的 payload 原始 JSON，业务层根据 MessageType 再反序列化为具体 DTO。
    /// </summary>
    public readonly struct RealtimeInboundMessage
    {
        public readonly RealtimeDeliveryMode DeliveryMode;
        public readonly RealtimeMessageEnvelope Envelope;
        public readonly string MessageType;
        public readonly string PayloadJson;

        public RealtimeInboundMessage(
            RealtimeDeliveryMode deliveryMode,
            RealtimeMessageEnvelope envelope,
            string payloadJson)
        {
            DeliveryMode = deliveryMode;
            Envelope = envelope;
            MessageType = envelope != null ? envelope.messageType : null;
            PayloadJson = payloadJson;
        }

        public bool IsValid => Envelope != null && Envelope.IsValid();
    }
}
