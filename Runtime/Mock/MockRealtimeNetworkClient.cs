using System.Collections.Generic;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Mock
{
    /// <summary>
    /// 本地 Mock 实时网络客户端。
    /// 它不创建真实 Socket，只把消息提交给内存中的 MockRoomServer。
    /// </summary>
    public sealed class MockRealtimeNetworkClient : IRealtimeNetworkClient
    {
        private readonly Queue<RealtimeInboundMessage> _inboundMessages = new Queue<RealtimeInboundMessage>();
        private readonly MockRoomServer _server;
        private string _displayName;

        public bool IsConnected { get; private set; }
        public string ClientId { get; private set; }
        public string SessionId { get; private set; }
        public long ServerTimeMs { get; private set; }

        public MockRealtimeNetworkClient(MockRoomServer server)
        {
            _server = server ?? MockRoomServer.Shared;
        }

        public void Connect(RealtimeConnectRequest request)
        {
            if (IsConnected)
            {
                return;
            }

            ClientId = string.IsNullOrWhiteSpace(request.PlayerId)
                ? MiniGameIdentityUtility.CreateLocalPlayerId()
                : request.PlayerId;
            _displayName = MiniGameIdentityUtility.NormalizeDisplayName(request.DisplayName);
            SessionId = _server.Connect(this, ClientId, _displayName);
            ServerTimeMs = MockMiniGameTime.NowMs;
            IsConnected = true;
        }

        public void Disconnect(string reason)
        {
            if (!IsConnected)
            {
                return;
            }

            _server.Disconnect(SessionId, reason);
            IsConnected = false;
        }

        public void SendReliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage
        {
            if (!IsConnected || message == null)
            {
                return;
            }

            _server.ReceiveReliable(SessionId, message);
        }

        public void SendUnreliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage
        {
            if (!IsConnected || message == null)
            {
                return;
            }

            _server.ReceiveUnreliable(SessionId, message);
        }

        public bool TryDequeueMessage(out RealtimeInboundMessage message)
        {
            if (_inboundMessages.Count > 0)
            {
                message = _inboundMessages.Dequeue();
                return true;
            }

            message = default;
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (!IsConnected)
            {
                return;
            }

            ServerTimeMs = MockMiniGameTime.NowMs;
        }

        internal void SetSessionId(string sessionId)
        {
            SessionId = sessionId;
        }

        internal void EnqueueReliable<TMessage>(string messageType, TMessage payload, string roomId = null)
            where TMessage : IRealtimeMessage
        {
            Enqueue(RealtimeDeliveryMode.Reliable, messageType, payload, roomId);
        }

        internal void EnqueueUnreliable<TMessage>(string messageType, TMessage payload, string roomId = null)
            where TMessage : IRealtimeMessage
        {
            Enqueue(RealtimeDeliveryMode.Unreliable, messageType, payload, roomId);
        }

        private void Enqueue<TMessage>(
            RealtimeDeliveryMode deliveryMode,
            string messageType,
            TMessage payload,
            string roomId)
            where TMessage : IRealtimeMessage
        {
            var envelope = new RealtimeMessageEnvelope(messageType)
            {
                roomId = roomId,
                playerId = ClientId,
                sessionId = SessionId,
                serverTimeMs = MockMiniGameTime.NowMs,
                payload = MockJsonUtility.ToJson(payload)
            };

            _inboundMessages.Enqueue(new RealtimeInboundMessage(deliveryMode, envelope, envelope.payload));
        }
    }
}
