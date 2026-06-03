using System;
using System.Collections.Generic;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.Controller
{
    /// <summary>
    /// MiniGame 前端运行时黑板。
    /// Controller 负责写入，UI Bridge 只读取，不直接改网络或玩法状态。
    /// </summary>
    public sealed class MiniGameBlackboard
    {
        private const int MaxChatMessages = 64;
        private const int MaxGiftMessages = 64;
        private const int MaxStrokeBatches = 128;

        private readonly List<RoomChatMessage> _chatMessages = new List<RoomChatMessage>(MaxChatMessages);
        private readonly List<RoomGiftSent> _giftMessages = new List<RoomGiftSent>(MaxGiftMessages);
        private readonly List<StrokePointBatch> _strokeBatches = new List<StrokePointBatch>(MaxStrokeBatches);

        public int Revision { get; private set; }
        public bool IsConnected { get; private set; }
        public string LocalPlayerId { get; private set; }
        public string SessionId { get; private set; }
        public string CurrentRoomId { get; private set; }
        public string UdpBindToken { get; private set; }
        public int UdpPort { get; private set; }
        public long ServerTimeMs { get; private set; }
        public string LastMessageType { get; private set; }

        public CreateRoomResult LastCreateRoomResult { get; private set; }
        public JoinRoomResult LastJoinRoomResult { get; private set; }
        public ReconnectResult LastReconnectResult { get; private set; }
        public UdpBindAccepted LastUdpBindAccepted { get; private set; }
        public ErrorMessage LastError { get; private set; }
        public RoomToastMessage LastToast { get; private set; }
        public ServerShutdown LastServerShutdown { get; private set; }

        public RoomSnapshot CurrentRoomSnapshot { get; private set; }
        public DrawTelephoneStageStarted CurrentStage { get; private set; }
        public DrawTelephoneTask CurrentTask { get; private set; }
        public DrawTelephoneReviewStarted CurrentReview { get; private set; }
        public DrawTelephoneVotingStarted CurrentVoting { get; private set; }
        public DrawTelephoneVotingEnded CurrentVotingResult { get; private set; }
        public SequentialRelayStateSnapshot CurrentSequentialRelay { get; private set; }
        public GameEnded LastGameEnded { get; private set; }

        public IReadOnlyList<RoomChatMessage> ChatMessages => _chatMessages;
        public IReadOnlyList<RoomGiftSent> GiftMessages => _giftMessages;
        public IReadOnlyList<StrokePointBatch> StrokeBatches => _strokeBatches;

        public bool IsLocalViewer
        {
            get
            {
                var viewers = CurrentRoomSnapshot?.viewers;
                if (viewers == null || string.IsNullOrWhiteSpace(LocalPlayerId))
                {
                    return false;
                }

                for (var i = 0; i < viewers.Length; i++)
                {
                    if (string.Equals(viewers[i]?.playerId, LocalPlayerId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Clear()
        {
            IsConnected = false;
            LocalPlayerId = null;
            SessionId = null;
            CurrentRoomId = null;
            UdpBindToken = null;
            UdpPort = 0;
            ServerTimeMs = 0L;
            LastMessageType = null;
            LastCreateRoomResult = null;
            LastJoinRoomResult = null;
            LastReconnectResult = null;
            LastUdpBindAccepted = null;
            LastError = null;
            LastToast = null;
            LastServerShutdown = null;
            CurrentRoomSnapshot = null;
            CurrentStage = null;
            CurrentTask = null;
            CurrentReview = null;
            CurrentVoting = null;
            CurrentVotingResult = null;
            CurrentSequentialRelay = null;
            LastGameEnded = null;
            _chatMessages.Clear();
            _giftMessages.Clear();
            _strokeBatches.Clear();
            BumpRevision();
        }

        public void ApplyConnected(string playerId, string sessionId, long serverTimeMs)
        {
            IsConnected = true;
            LocalPlayerId = playerId;
            SessionId = sessionId;
            ServerTimeMs = Math.Max(ServerTimeMs, serverTimeMs);
            BumpRevision();
        }

        public void ApplyDisconnected()
        {
            IsConnected = false;
            BumpRevision();
        }

        public void ApplyCreateRoomResult(CreateRoomResult result)
        {
            LastCreateRoomResult = result;
            if (result != null && result.succeeded)
            {
                CurrentRoomId = result.roomId;
                UdpBindToken = result.udpBindToken;
                UdpPort = result.udpPort;
            }
            BumpRevision();
        }

        public void ApplyJoinRoomResult(JoinRoomResult result)
        {
            LastJoinRoomResult = result;
            if (result != null && result.succeeded)
            {
                CurrentRoomId = result.roomId;
                UdpBindToken = result.udpBindToken;
                UdpPort = result.udpPort;
            }
            BumpRevision();
        }

        public void ApplyReconnectResult(ReconnectResult result)
        {
            LastReconnectResult = result;
            if (result != null && result.succeeded)
            {
                SessionId = result.sessionId;
                UdpBindToken = result.udpBindToken;
                UdpPort = result.udpPort;
                ApplyRoomSnapshot(result.snapshot);
                return;
            }

            BumpRevision();
        }

        public void ApplyRoomSnapshot(RoomSnapshot snapshot)
        {
            CurrentRoomSnapshot = snapshot;
            if (snapshot != null)
            {
                CurrentRoomId = snapshot.roomId;
                CurrentTask = snapshot.currentTask;
                CurrentSequentialRelay = snapshot.sequentialRelay;
                if (IsRoomBackToLobby(snapshot.state))
                {
                    // 服务器回到房间大厅时，清理上一局玩法缓存，避免 UI 继续显示结算或作画状态。
                    CurrentStage = null;
                    CurrentTask = null;
                    CurrentReview = null;
                    CurrentVoting = null;
                    CurrentVotingResult = null;
                    CurrentSequentialRelay = null;
                    LastGameEnded = null;
                }
            }
            BumpRevision();
        }

        public void ApplySequentialRelayStateChanged(SequentialRelayStateChanged message)
        {
            CurrentSequentialRelay = message?.snapshot;
            BumpRevision();
        }

        public void ApplyStageStarted(DrawTelephoneStageStarted stage)
        {
            CurrentStage = stage;
            CurrentTask = stage != null && stage.tasks != null && stage.tasks.Length > 0
                ? stage.tasks[0]
                : null;
            BumpRevision();
        }

        public void ApplyReviewStarted(DrawTelephoneReviewStarted review)
        {
            CurrentReview = review;
            CurrentVoting = null;
            CurrentVotingResult = null;
            BumpRevision();
        }

        public void ApplyVotingStarted(DrawTelephoneVotingStarted voting)
        {
            CurrentVoting = voting;
            CurrentVotingResult = null;
            BumpRevision();
        }

        public void ApplyVotingEnded(DrawTelephoneVotingEnded votingEnded)
        {
            CurrentVotingResult = votingEnded;
            BumpRevision();
        }

        public void ApplyGameEnded(GameEnded gameEnded)
        {
            LastGameEnded = gameEnded;
            BumpRevision();
        }

        public void ApplyUdpBindAccepted(UdpBindAccepted accepted)
        {
            LastUdpBindAccepted = accepted;
            BumpRevision();
        }

        public void AddChat(RoomChatMessage message)
        {
            AddWithLimit(_chatMessages, message, MaxChatMessages);
            BumpRevision();
        }

        public void AddGift(RoomGiftSent message)
        {
            AddWithLimit(_giftMessages, message, MaxGiftMessages);
            BumpRevision();
        }

        public void AddStrokeBatch(StrokePointBatch batch)
        {
            AddWithLimit(_strokeBatches, batch, MaxStrokeBatches);
            BumpRevision();
        }

        public void ApplyError(ErrorMessage error)
        {
            LastError = error;
            BumpRevision();
        }

        public void ApplyToast(RoomToastMessage toast)
        {
            LastToast = toast;
            BumpRevision();
        }

        public void ApplyServerShutdown(ServerShutdown shutdown)
        {
            LastServerShutdown = shutdown;
            BumpRevision();
        }

        public void MarkInbound(string messageType, long serverTimeMs)
        {
            LastMessageType = messageType;
            if (serverTimeMs > 0L)
            {
                ServerTimeMs = serverTimeMs;
            }
        }

        private static void AddWithLimit<T>(List<T> list, T item, int limit)
        {
            if (item == null)
            {
                return;
            }

            list.Add(item);
            while (list.Count > limit)
            {
                list.RemoveAt(0);
            }
        }

        private void BumpRevision()
        {
            if (Revision < int.MaxValue)
            {
                Revision++;
            }
        }

        private static bool IsRoomBackToLobby(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            var normalized = state.Trim().ToUpperInvariant();
            return string.Equals(normalized, "LOBBY", StringComparison.Ordinal)
                   || string.Equals(normalized, "CLOSED", StringComparison.Ordinal);
        }
    }
}
