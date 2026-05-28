using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Network
{
    /// <summary>
    /// Unity 前端实时网络客户端抽象。
    /// 本地 Mock 和真实 Netty 客户端都必须实现该接口，业务层只依赖这里。
    /// </summary>
    public interface IRealtimeNetworkClient
    {
        bool IsConnected { get; }
        string ClientId { get; }
        string SessionId { get; }
        long ServerTimeMs { get; }

        void Connect(RealtimeConnectRequest request);
        void Disconnect(string reason);

        void SendReliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage;
        void SendUnreliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage;

        bool TryDequeueMessage(out RealtimeInboundMessage message);
        void Tick(float deltaTime);
    }
}
