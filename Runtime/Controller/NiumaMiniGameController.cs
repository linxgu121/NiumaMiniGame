using System;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Mock;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using NiumaMiniGame.Telephone;
using UnityEngine;

namespace NiumaMiniGame.Controller
{
    /// <summary>
    /// MiniGame 前端根控制器。
    /// 它只负责网络消息进出和黑板状态维护，具体 UI 表现交给 Bridge 与 Receiver。
    /// </summary>
    public sealed class NiumaMiniGameController : MonoBehaviour
    {
        [Header("网络客户端")]
        [Tooltip("真实网络客户端组件。组件需要实现 IRealtimeNetworkClient；为空且启用 Mock 时会自动创建 Mock 客户端。")]
        [SerializeField] private MonoBehaviour networkClientBehaviour;

        [Tooltip("是否使用本地 Mock 网络客户端。真实联调 Netty 后端时关闭，并绑定真实网络客户端组件。")]
        [SerializeField] private bool useMockClient = true;

        [Tooltip("是否在 Awake 时自动初始化并连接网络客户端。")]
        [SerializeField] private bool connectOnAwake = true;

        [Tooltip("是否由该控制器在 Update 中驱动网络 Tick。若后续接入统一模块启动器，可关闭。")]
        [SerializeField] private bool tickNetworkInUpdate = true;

        [Header("连接参数")]
        [Tooltip("真实后端地址。Mock 模式下仅作为占位。")]
        [SerializeField] private string host = "127.0.0.1";

        [Tooltip("真实后端 TCP 端口。Mock 模式下不使用。")]
        [SerializeField] private int tcpPort = 18080;

        [Tooltip("真实后端 UDP 端口。Mock 模式下不使用。")]
        [SerializeField] private int udpPort = 18081;

        [Tooltip("本地玩家 ID。为空时自动生成 UUID；后续接入账号系统后可改为账号下发 ID。")]
        [SerializeField] private string playerId;

        [Tooltip("房间内显示昵称。")]
        [SerializeField] private string displayName = "玩家";

        [Tooltip("是否启用 UDP。UDP 不通时由真实客户端降级为 TCP 点位。")]
        [SerializeField] private bool enableUdp = true;

        [Header("运行参数")]
        [Tooltip("每帧最多处理多少条网络消息，避免大量消息一次性卡住主线程。")]
        [SerializeField] private int maxMessagesPerTick = 64;

        [Tooltip("是否输出桥接和消息解析警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly MiniGameBlackboard _blackboard = new MiniGameBlackboard();
        private IRealtimeNetworkClient _networkClient;

        public MiniGameBlackboard Blackboard => _blackboard;
        public IRealtimeNetworkClient NetworkClient => _networkClient;
        public int MiniGameRevision => _blackboard.Revision;
        public bool IsConnected => _networkClient != null && _networkClient.IsConnected;
        public string LocalPlayerId => _networkClient != null ? _networkClient.ClientId : _blackboard.LocalPlayerId;
        public string SessionId => _networkClient != null ? _networkClient.SessionId : _blackboard.SessionId;

        private void Awake()
        {
            if (connectOnAwake)
            {
                Connect();
            }
        }

        private void Update()
        {
            if (tickNetworkInUpdate)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _blackboard.ApplyDisconnected();
        }

        public bool Connect()
        {
            if (!EnsureNetworkClient())
            {
                return false;
            }

            if (_networkClient.IsConnected)
            {
                return true;
            }

            var connectPlayerId = string.IsNullOrWhiteSpace(playerId)
                ? MiniGameIdentityUtility.CreateLocalPlayerId()
                : playerId;
            var connectDisplayName = MiniGameIdentityUtility.NormalizeDisplayName(displayName);

            _networkClient.Connect(new RealtimeConnectRequest(
                host,
                tcpPort,
                udpPort,
                connectPlayerId,
                connectDisplayName,
                enableUdp));
            Tick(0f);
            return _networkClient.IsConnected;
        }

        public void Disconnect(string reason = "UserDisconnect")
        {
            _networkClient?.Disconnect(reason);
            _blackboard.ApplyDisconnected();
        }

        public void Tick(float deltaTime)
        {
            if (_networkClient == null)
            {
                return;
            }

            _networkClient.Tick(deltaTime);

            var count = 0;
            while (count < Mathf.Max(1, maxMessagesPerTick) && _networkClient.TryDequeueMessage(out var message))
            {
                count++;
                ProcessInbound(message);
            }
        }

        public bool CreateRoom(string modeId)
        {
            return SendReliable(new CreateRoomRequest
            {
                modeId = string.IsNullOrWhiteSpace(modeId) ? "draw_telephone" : modeId,
                displayName = displayName
            });
        }

        public bool JoinRoom(string roomId, bool asViewer)
        {
            return SendReliable(new JoinRoomRequest
            {
                roomId = roomId,
                displayName = displayName,
                viewer = asViewer
            });
        }

        public bool LeaveRoom(string roomId = null)
        {
            return SendReliable(new LeaveRoomRequest
            {
                roomId = string.IsNullOrWhiteSpace(roomId) ? _blackboard.CurrentRoomId : roomId
            });
        }

        public bool SetReady(bool ready)
        {
            return SendReliable(new PlayerReadyRequest { ready = ready });
        }

        public bool BindUdp()
        {
            return SendReliable(new UdpBindRequest
            {
                roomId = _blackboard.CurrentRoomId,
                playerId = LocalPlayerId,
                sessionId = SessionId,
                udpBindToken = _blackboard.UdpBindToken
            });
        }

        public bool SendChat(string text)
        {
            return SendReliable(new SendRoomChatRequest { text = text });
        }

        public bool SendGift(string giftType, string toPlayerId)
        {
            return SendReliable(new SendRoomGiftRequest
            {
                giftType = giftType,
                toPlayerId = toPlayerId
            });
        }

        public bool SubmitDrawing(string chainId, string strokeGroupId)
        {
            return SendReliable(new SubmitTelephoneDrawing
            {
                chainId = chainId,
                strokeGroupId = strokeGroupId
            });
        }

        public bool SubmitGuess(string chainId, string guessText)
        {
            return SendReliable(new SubmitTelephoneGuess
            {
                chainId = chainId,
                guessText = guessText
            });
        }

        public bool SubmitVote(string chainId, int score)
        {
            return SendReliable(new SubmitChainVote
            {
                chainId = chainId,
                score = Mathf.Clamp(score, 0, 100)
            });
        }

        public bool SendStrokePointBatch(StrokePointBatch batch, bool reliableFallback = false)
        {
            if (batch == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(batch.roomId))
            {
                batch.roomId = _blackboard.CurrentRoomId;
            }

            if (reliableFallback)
            {
                return SendReliable(batch);
            }

            return SendUnreliable(batch);
        }

        private bool SendReliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage
        {
            if (!EnsureNetworkClient() || !_networkClient.IsConnected || message == null)
            {
                return false;
            }

            _networkClient.SendReliable(message);
            Tick(0f);
            return true;
        }

        private bool SendUnreliable<TMessage>(TMessage message) where TMessage : IRealtimeMessage
        {
            if (!EnsureNetworkClient() || !_networkClient.IsConnected || message == null)
            {
                return false;
            }

            _networkClient.SendUnreliable(message);
            Tick(0f);
            return true;
        }

        private bool EnsureNetworkClient()
        {
            if (_networkClient != null)
            {
                return true;
            }

            if (networkClientBehaviour != null)
            {
                _networkClient = networkClientBehaviour as IRealtimeNetworkClient;
                if (_networkClient == null && logWarnings)
                {
                    Debug.LogWarning("[NiumaMiniGame] networkClientBehaviour 未实现 IRealtimeNetworkClient。", this);
                }
            }

            if (_networkClient == null && useMockClient)
            {
                _networkClient = new MockRealtimeNetworkClient(MockRoomServer.Shared);
            }

            return _networkClient != null;
        }

        private void ProcessInbound(RealtimeInboundMessage message)
        {
            if (!message.IsValid)
            {
                return;
            }

            _blackboard.MarkInbound(message.MessageType, message.Envelope.serverTimeMs);

            switch (message.MessageType)
            {
                case MessageType.ConnectAccepted:
                    var accepted = Deserialize<ConnectAccepted>(message);
                    if (accepted != null)
                    {
                        _blackboard.ApplyConnected(accepted.playerId, accepted.sessionId, accepted.serverTimeMs);
                    }
                    break;
                case MessageType.CreateRoomResult:
                    _blackboard.ApplyCreateRoomResult(Deserialize<CreateRoomResult>(message));
                    break;
                case MessageType.JoinRoomResult:
                    _blackboard.ApplyJoinRoomResult(Deserialize<JoinRoomResult>(message));
                    break;
                case MessageType.ReconnectResult:
                    _blackboard.ApplyReconnectResult(Deserialize<ReconnectResult>(message));
                    break;
                case MessageType.RoomSnapshot:
                    _blackboard.ApplyRoomSnapshot(Deserialize<RoomSnapshot>(message));
                    break;
                case MessageType.UdpBindAccepted:
                    _blackboard.ApplyUdpBindAccepted(Deserialize<UdpBindAccepted>(message));
                    break;
                case MessageType.DrawTelephoneStageStarted:
                    _blackboard.ApplyStageStarted(Deserialize<DrawTelephoneStageStarted>(message));
                    break;
                case MessageType.DrawTelephoneReviewStarted:
                    _blackboard.ApplyReviewStarted(Deserialize<DrawTelephoneReviewStarted>(message));
                    break;
                case MessageType.DrawTelephoneVotingStarted:
                    _blackboard.ApplyVotingStarted(Deserialize<DrawTelephoneVotingStarted>(message));
                    break;
                case MessageType.DrawTelephoneVotingEnded:
                    _blackboard.ApplyVotingEnded(Deserialize<DrawTelephoneVotingEnded>(message));
                    break;
                case MessageType.GameEnded:
                    _blackboard.ApplyGameEnded(Deserialize<GameEnded>(message));
                    break;
                case MessageType.RoomChatMessage:
                    _blackboard.AddChat(Deserialize<RoomChatMessage>(message));
                    break;
                case MessageType.RoomGiftSent:
                    _blackboard.AddGift(Deserialize<RoomGiftSent>(message));
                    break;
                case MessageType.StrokePointBatch:
                    _blackboard.AddStrokeBatch(Deserialize<StrokePointBatch>(message));
                    break;
                case MessageType.ErrorMessage:
                    _blackboard.ApplyError(Deserialize<ErrorMessage>(message));
                    break;
                case MessageType.ServerShutdown:
                    _blackboard.ApplyServerShutdown(Deserialize<ServerShutdown>(message));
                    break;
            }
        }

        private TMessage Deserialize<TMessage>(RealtimeInboundMessage message) where TMessage : class, IRealtimeMessage
        {
            if (string.IsNullOrWhiteSpace(message.PayloadJson))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<TMessage>(message.PayloadJson);
            }
            catch (Exception exception)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"[NiumaMiniGame] 解析消息失败：Type={message.MessageType}, Error={exception.Message}", this);
                }
                return null;
            }
        }
    }
}
