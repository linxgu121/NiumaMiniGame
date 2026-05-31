using System;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Controller;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using NiumaMiniGame.Telephone;
using NiumaMiniGame.ViewData;
using UnityEngine;

namespace NiumaMiniGame.Bridge
{
    /// <summary>
    /// MiniGame UI 桥接层。
    /// 只把黑板数据转换为 ViewData 并推给 Receiver，不负责实例化具体 UI。
    /// </summary>
    public sealed class MiniGameUIViewBridge : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("MiniGame 根控制器。为空时可自动在场景中查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("实现 IMiniGameUIReceiver 的组件。为空时会尝试在当前物体上查找。")]
        [SerializeField] private MonoBehaviour miniGameUIReceiverProvider;

        [Header("刷新")]
        [Tooltip("启用组件时是否立即刷新一次 UI。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("未手动绑定控制器时是否自动查找场景中的 NiumaMiniGameController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindController = true;

        [Tooltip("是否输出桥接层警告。")]
        [SerializeField] private bool logWarnings = true;

        private IMiniGameUIReceiver _receiver;
        private MiniGamePanelViewData _lastPanelData;
        private int _observedRevision = -1;
        private bool _refreshRequested;
        private bool _isApplyingUpdate;
        private int _lastBuildFailureRevision = int.MinValue;

        private void OnEnable()
        {
            _observedRevision = -1;
            _refreshRequested = refreshOnEnable;
            ResolveReceiver(false);
        }

        private void OnDisable()
        {
            _isApplyingUpdate = false;
            _refreshRequested = false;
        }

        private void LateUpdate()
        {
            if (!EnsureController())
            {
                return;
            }

            if (_refreshRequested || _observedRevision != miniGameController.MiniGameRevision)
            {
                _refreshRequested = false;
                RefreshMiniGamePanel();
            }
        }

        public void RequestRefresh()
        {
            _observedRevision = -1;
            _refreshRequested = true;
        }

        public void RefreshMiniGamePanel()
        {
            if (!EnsureController())
            {
                ApplyClearUpdate();
                return;
            }

            var targetRevision = miniGameController.MiniGameRevision;
            MiniGamePanelViewData panelData;
            try
            {
                panelData = BuildPanelViewData(targetRevision);
            }
            catch (Exception exception)
            {
                _observedRevision = -1;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    Debug.LogError($"[NiumaMiniGameUIBridge] 构建 MiniGame UI 表现数据失败，桥接层会在下一帧重试。Revision={targetRevision}, Error={exception.Message}", this);
                }
                _lastBuildFailureRevision = targetRevision;
                return;
            }

            _lastBuildFailureRevision = int.MinValue;
            _observedRevision = targetRevision;
            ApplyRawUpdate(new MiniGameUIUpdate(
                MiniGameUIUpdateType.Refresh,
                targetRevision,
                panelData,
                _lastPanelData));
            _lastPanelData = panelData;
        }

        private MiniGamePanelViewData BuildPanelViewData(int revision)
        {
            var blackboard = miniGameController.Blackboard;
            var room = blackboard.CurrentRoomSnapshot;

            return new MiniGamePanelViewData
            {
                Revision = revision,
                IsConnected = blackboard.IsConnected,
                IsLocalViewer = blackboard.IsLocalViewer,
                LocalPlayerId = blackboard.LocalPlayerId,
                SessionId = blackboard.SessionId,
                RoomId = blackboard.CurrentRoomId,
                LastMessageType = blackboard.LastMessageType,
                Room = BuildRoomViewData(room, blackboard.LocalPlayerId, blackboard.ServerTimeMs),
                CurrentTask = BuildTaskViewData(blackboard.CurrentTask, room, blackboard.ServerTimeMs),
                Review = BuildReviewViewData(blackboard.CurrentReview),
                Voting = BuildVotingViewData(blackboard.CurrentVoting, blackboard.ServerTimeMs),
                VotingResult = BuildVotingResultViewData(blackboard.CurrentVotingResult),
                Chats = BuildChatViewData(blackboard.ChatMessages, blackboard.LocalPlayerId),
                Gifts = BuildGiftViewData(blackboard.GiftMessages, blackboard.LocalPlayerId),
                LastError = BuildErrorViewData(blackboard.LastError)
            };
        }

        private static MiniGameRoomViewData BuildRoomViewData(RoomSnapshot snapshot, string localPlayerId, long serverTimeMs)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new MiniGameRoomViewData
            {
                RoomId = snapshot.roomId,
                ModeId = snapshot.modeId,
                State = ParseRoomState(snapshot.state),
                RoundIndex = snapshot.roundIndex,
                MaxRoundCount = snapshot.maxRoundCount,
                DrawerPlayerId = snapshot.drawerPlayerId,
                RemainingSeconds = ComputeRemainingSeconds(snapshot.stateDeadlineTimeMs, serverTimeMs),
                Players = BuildPlayers(snapshot.players, localPlayerId, false),
                Viewers = BuildViewers(snapshot.viewers, localPlayerId),
                Scores = BuildScores(snapshot.scores)
            };
        }

        private static MiniGamePlayerViewData[] BuildPlayers(RoomPlayerSnapshot[] players, string localPlayerId, bool isViewer)
        {
            if (players == null || players.Length == 0)
            {
                return Array.Empty<MiniGamePlayerViewData>();
            }

            var result = new MiniGamePlayerViewData[players.Length];
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                result[i] = new MiniGamePlayerViewData
                {
                    PlayerId = player?.playerId,
                    DisplayName = player?.displayName,
                    IsReady = player != null && player.ready,
                    IsConnected = player != null && player.connected,
                    IsLocalPlayer = player != null && string.Equals(player.playerId, localPlayerId, StringComparison.Ordinal),
                    IsViewer = isViewer
                };
            }

            return result;
        }

        private static MiniGamePlayerViewData[] BuildViewers(RoomViewerSnapshot[] viewers, string localPlayerId)
        {
            if (viewers == null || viewers.Length == 0)
            {
                return Array.Empty<MiniGamePlayerViewData>();
            }

            var result = new MiniGamePlayerViewData[viewers.Length];
            for (var i = 0; i < viewers.Length; i++)
            {
                var viewer = viewers[i];
                result[i] = new MiniGamePlayerViewData
                {
                    PlayerId = viewer?.playerId,
                    DisplayName = viewer?.displayName,
                    IsReady = false,
                    IsConnected = viewer != null && viewer.connected,
                    IsLocalPlayer = viewer != null && string.Equals(viewer.playerId, localPlayerId, StringComparison.Ordinal),
                    IsViewer = true
                };
            }

            return result;
        }

        private static MiniGameScoreViewData[] BuildScores(ScoreEntry[] scores)
        {
            if (scores == null || scores.Length == 0)
            {
                return Array.Empty<MiniGameScoreViewData>();
            }

            var result = new MiniGameScoreViewData[scores.Length];
            for (var i = 0; i < scores.Length; i++)
            {
                var score = scores[i];
                result[i] = new MiniGameScoreViewData
                {
                    PlayerId = score?.playerId,
                    DisplayName = score?.displayName,
                    Score = score != null ? score.score : 0
                };
            }

            Array.Sort(result, (left, right) => right.Score.CompareTo(left.Score));
            for (var i = 0; i < result.Length; i++)
            {
                result[i].Rank = i + 1;
            }

            return result;
        }

        private static DrawTelephoneTaskViewData BuildTaskViewData(DrawTelephoneTask task, RoomSnapshot room, long serverTimeMs)
        {
            if (task == null && room != null)
            {
                task = room.currentTask;
            }
            if (task == null)
            {
                return null;
            }

            return new DrawTelephoneTaskViewData
            {
                ChainId = task.chainId,
                StageIndex = task.stageIndex,
                ActionType = ParseTelephoneActionType(task.actionType),
                PromptWord = task.promptWord,
                PreviousGuess = task.previousGuess,
                PreviousStrokeGroupId = task.previousStrokeGroupId,
                PreviousCanvas = BuildCanvasViewData(task.previousCanvas),
                RemainingSeconds = ComputeRemainingSeconds(room?.stateDeadlineTimeMs ?? 0L, serverTimeMs)
            };
        }

        private static DrawTelephoneCanvasViewData BuildCanvasViewData(DrawTelephoneCanvasData canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            var strokes = canvas.strokes == null
                ? Array.Empty<DrawTelephoneStrokeViewData>()
                : new DrawTelephoneStrokeViewData[canvas.strokes.Length];

            for (var i = 0; i < strokes.Length; i++)
            {
                var stroke = canvas.strokes[i];
                strokes[i] = new DrawTelephoneStrokeViewData
                {
                    StrokeId = stroke?.strokeId,
                    Points = stroke?.points ?? Array.Empty<DrawPointData>()
                };
            }

            return new DrawTelephoneCanvasViewData
            {
                StrokeGroupId = canvas.strokeGroupId,
                Strokes = strokes
            };
        }

        private static DrawTelephoneReviewViewData BuildReviewViewData(DrawTelephoneReviewStarted review)
        {
            return review == null
                ? null
                : new DrawTelephoneReviewViewData
                {
                    Chains = review.chains ?? Array.Empty<DrawTelephoneChainState>(),
                    ReviewSecondsPerChain = review.reviewSecondsPerChain
                };
        }

        private static DrawTelephoneVotingViewData BuildVotingViewData(DrawTelephoneVotingStarted voting, long serverTimeMs)
        {
            return voting == null
                ? null
                : new DrawTelephoneVotingViewData
                {
                    Chains = voting.chains ?? Array.Empty<ChainVoteInfo>(),
                    RemainingSeconds = ComputeRemainingSeconds(voting.deadlineTimeMs, serverTimeMs)
                };
        }

        private static DrawTelephoneVotingResultViewData BuildVotingResultViewData(DrawTelephoneVotingEnded votingEnded)
        {
            return votingEnded == null
                ? null
                : new DrawTelephoneVotingResultViewData
                {
                    Results = votingEnded.results ?? Array.Empty<ChainVoteResult>()
                };
        }

        private static MiniGameChatViewData[] BuildChatViewData(System.Collections.Generic.IReadOnlyList<RoomChatMessage> messages, string localPlayerId)
        {
            if (messages == null || messages.Count == 0)
            {
                return Array.Empty<MiniGameChatViewData>();
            }

            var result = new MiniGameChatViewData[messages.Count];
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                result[i] = new MiniGameChatViewData
                {
                    PlayerId = message?.playerId,
                    DisplayName = message?.displayName,
                    Text = message?.text,
                    ServerTimeMs = message != null ? message.serverTimeMs : 0L,
                    IsLocalPlayer = message != null && string.Equals(message.playerId, localPlayerId, StringComparison.Ordinal)
                };
            }

            return result;
        }

        private static MiniGameGiftViewData[] BuildGiftViewData(System.Collections.Generic.IReadOnlyList<RoomGiftSent> messages, string localPlayerId)
        {
            if (messages == null || messages.Count == 0)
            {
                return Array.Empty<MiniGameGiftViewData>();
            }

            var result = new MiniGameGiftViewData[messages.Count];
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                result[i] = new MiniGameGiftViewData
                {
                    FromPlayerId = message?.fromPlayerId,
                    FromDisplayName = message?.fromDisplayName,
                    ToPlayerId = message?.toPlayerId,
                    GiftType = message?.giftType,
                    ServerTimeMs = message != null ? message.serverTimeMs : 0L,
                    IsFromLocalPlayer = message != null && string.Equals(message.fromPlayerId, localPlayerId, StringComparison.Ordinal)
                };
            }

            return result;
        }

        private static MiniGameErrorViewData BuildErrorViewData(ErrorMessage error)
        {
            return error == null
                ? null
                : new MiniGameErrorViewData
                {
                    ErrorCode = error.errorCode,
                    MessageKey = error.messageKey,
                    DebugMessage = error.debugMessage
                };
        }

        private void ApplyClearUpdate()
        {
            _lastPanelData = null;
            _observedRevision = -1;
            ApplyRawUpdate(new MiniGameUIUpdate(
                MiniGameUIUpdateType.Cleared,
                _observedRevision,
                null,
                null));
        }

        private void ApplyRawUpdate(MiniGameUIUpdate update)
        {
            ResolveReceiver(false);
            if (_receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaMiniGameUIBridge] 检测到 UI 刷新重入，已跳过本次 ApplyMiniGameUpdate。请不要在 IMiniGameUIReceiver.ApplyMiniGameUpdate 中修改 MiniGame 状态。", this);
                }
                return;
            }

            var revisionBeforeApply = miniGameController != null ? miniGameController.MiniGameRevision : _observedRevision;
            _isApplyingUpdate = true;
            try
            {
                _receiver.ApplyMiniGameUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (miniGameController != null && miniGameController.MiniGameRevision != revisionBeforeApply)
            {
                _observedRevision = -1;
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaMiniGameUIBridge] UI Receiver 内修改了 MiniGame 数据，桥接层已请求下一帧重新刷新。建议把房间命令放到按钮回调或业务管线中处理。", this);
                }
            }
        }

        private bool EnsureController()
        {
            if (miniGameController != null)
            {
                return true;
            }

            if (!autoFindController)
            {
                return false;
            }

#if UNITY_2023_1_OR_NEWER
            miniGameController = FindFirstObjectByType<NiumaMiniGameController>();
#else
            miniGameController = FindObjectOfType<NiumaMiniGameController>();
#endif
            return miniGameController != null;
        }

        private void ResolveReceiver(bool logMissing)
        {
            if (_receiver != null)
            {
                return;
            }

            if (miniGameUIReceiverProvider == null)
            {
                var behaviours = GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IMiniGameUIReceiver)
                    {
                        miniGameUIReceiverProvider = behaviours[i];
                        break;
                    }
                }
            }

            _receiver = miniGameUIReceiverProvider as IMiniGameUIReceiver;
            if (_receiver == null && logMissing && logWarnings)
            {
                Debug.LogWarning("[NiumaMiniGameUIBridge] 未找到 IMiniGameUIReceiver，UI 数据不会被展示。", this);
            }
        }

        private static MiniGameRoomState ParseRoomState(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return MiniGameRoomState.None;
            }

            switch (value.Trim().ToUpperInvariant())
            {
                case "LOBBY": return MiniGameRoomState.Lobby;
                case "PREPARING": return MiniGameRoomState.Preparing;
                case "PLAYING": return MiniGameRoomState.Playing;
                case "REVIEW": return MiniGameRoomState.Review;
                case "VOTING": return MiniGameRoomState.Voting;
                case "SETTLEMENT": return MiniGameRoomState.Settlement;
                case "CLOSED": return MiniGameRoomState.Closed;
                default: return MiniGameRoomState.None;
            }
        }

        private static TelephoneActionType ParseTelephoneActionType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TelephoneActionType.None;
            }

            switch (value.Trim().ToUpperInvariant())
            {
                case "DRAW": return TelephoneActionType.Draw;
                case "GUESS": return TelephoneActionType.Guess;
                default: return TelephoneActionType.None;
            }
        }

        private static float ComputeRemainingSeconds(long deadlineTimeMs, long serverTimeMs)
        {
            if (deadlineTimeMs <= 0L || serverTimeMs <= 0L || deadlineTimeMs <= serverTimeMs)
            {
                return 0f;
            }

            return (deadlineTimeMs - serverTimeMs) / 1000f;
        }
    }
}
