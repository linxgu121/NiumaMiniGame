using NiumaMiniGame.Controller;
using NiumaMiniGame.Enum;
using NiumaScene.Controller;
using NiumaScene.Data;
using UnityEngine;

namespace NiumaMiniGame.UIBridge
{
    /// <summary>
    /// 玩法场景返回桥接。
    /// 挂在真正你画我猜玩法场景中，观察 MiniGame 黑板；房间回到 Lobby 后自动返回房间场景。
    /// </summary>
    public sealed class MiniGameGameplaySceneReturnBridge : MonoBehaviour
    {
        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("NiumaScene 根控制器。为空时会自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("未绑定 Controller 时是否自动查找。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindControllers = true;

        [Header("返回规则")]
        [Tooltip("曾经进入游戏阶段后，收到房间状态 Lobby 时是否自动返回房间场景。")]
        [SerializeField] private bool returnWhenRoomBackToLobby = true;

        [Tooltip("收到房间状态 Closed 时是否也自动返回房间场景。")]
        [SerializeField] private bool returnWhenRoomClosed = true;

        [Tooltip("返回房间场景时是否冻结输入。")]
        [SerializeField] private bool freezeInputDuringReturn = true;

        [Tooltip("返回房间场景时是否请求显示 Loading UI。")]
        [SerializeField] private bool showLoadingUIOnReturn = true;

        [Tooltip("为 true 时输出缺少引用或返回失败的警告。")]
        [SerializeField] private bool logWarnings = true;

        private int _observedMiniGameRevision = -1;
        private bool _hasObservedGameplay;
        private bool _returnRequested;

        private void LateUpdate()
        {
            if (!ResolveMiniGameController(false))
            {
                return;
            }

            var revision = miniGameController.MiniGameRevision;
            if (_observedMiniGameRevision == revision)
            {
                return;
            }

            _observedMiniGameRevision = revision;
            CheckReturnState();
        }

        private void CheckReturnState()
        {
            var blackboard = miniGameController != null ? miniGameController.Blackboard : null;
            var room = blackboard?.CurrentRoomSnapshot;
            if (room == null)
            {
                return;
            }

            var roomState = ParseRoomState(room.state);
            var gameplayPhase = ParseGameplayPhase(blackboard.CurrentSequentialRelay?.phase, room.state);
            if (IsGameplayRunning(roomState, gameplayPhase))
            {
                _hasObservedGameplay = true;
                _returnRequested = false;
                return;
            }

            if (!_hasObservedGameplay || _returnRequested)
            {
                return;
            }

            if ((returnWhenRoomBackToLobby && roomState == MiniGameRoomState.Lobby)
                || (returnWhenRoomClosed && roomState == MiniGameRoomState.Closed))
            {
                ReturnToRoomScene();
            }
        }

        private void ReturnToRoomScene()
        {
            if (!ResolveSceneController(true))
            {
                return;
            }

            _returnRequested = true;
            var handle = sceneController.ReturnToPreviousScene(new SceneTransitionOptions
            {
                FreezeInputDuringLoad = freezeInputDuringReturn,
                ShowLoadingUI = showLoadingUIOnReturn,
                ReplacePendingRequest = true
            });

            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded))
            {
                _returnRequested = false;
                if (logWarnings)
                {
                    Debug.LogWarning($"[MiniGameGameplaySceneReturnBridge] 自动返回房间场景失败：{handle.Result?.ErrorCode} {handle.Result?.ErrorMessage}", this);
                }
            }
        }

        private bool ResolveMiniGameController(bool warn)
        {
            if (miniGameController != null)
            {
                return true;
            }

            if (autoFindControllers)
            {
#if UNITY_2023_1_OR_NEWER
                miniGameController = FindFirstObjectByType<NiumaMiniGameController>();
#else
                miniGameController = FindObjectOfType<NiumaMiniGameController>();
#endif
            }

            if (miniGameController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameGameplaySceneReturnBridge] 未找到 NiumaMiniGameController，无法观察房间返回状态。", this);
            }

            return miniGameController != null;
        }

        private bool ResolveSceneController(bool warn)
        {
            if (sceneController != null)
            {
                return true;
            }

            if (autoFindControllers)
            {
#if UNITY_2023_1_OR_NEWER
                sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
                sceneController = FindObjectOfType<NiumaSceneController>();
#endif
            }

            if (sceneController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameGameplaySceneReturnBridge] 未找到 NiumaSceneController，无法自动返回房间场景。", this);
            }

            return sceneController != null;
        }

        private static bool IsGameplayRunning(MiniGameRoomState roomState, MiniGameGameplayPhase? gameplayPhase)
        {
            if (roomState == MiniGameRoomState.Preparing
                || roomState == MiniGameRoomState.Playing
                || roomState == MiniGameRoomState.Review
                || roomState == MiniGameRoomState.Voting
                || roomState == MiniGameRoomState.Settlement)
            {
                return true;
            }

            return gameplayPhase == MiniGameGameplayPhase.Preparing
                   || gameplayPhase == MiniGameGameplayPhase.TopicReveal
                   || gameplayPhase == MiniGameGameplayPhase.Drawing
                   || gameplayPhase == MiniGameGameplayPhase.Answering
                   || gameplayPhase == MiniGameGameplayPhase.Review
                   || gameplayPhase == MiniGameGameplayPhase.Voting
                   || gameplayPhase == MiniGameGameplayPhase.Settlement;
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

        private static MiniGameGameplayPhase? ParseGameplayPhase(string sequentialPhase, string roomState)
        {
            var value = !string.IsNullOrWhiteSpace(sequentialPhase) ? sequentialPhase : roomState;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            switch (value.Trim().ToUpperInvariant())
            {
                case "LOBBY": return MiniGameGameplayPhase.Lobby;
                case "PREPARING": return MiniGameGameplayPhase.Preparing;
                case "TOPIC_REVEAL":
                case "TOPICREVEAL":
                    return MiniGameGameplayPhase.TopicReveal;
                case "DRAWING": return MiniGameGameplayPhase.Drawing;
                case "ANSWERING": return MiniGameGameplayPhase.Answering;
                case "REVIEW": return MiniGameGameplayPhase.Review;
                case "VOTING": return MiniGameGameplayPhase.Voting;
                case "SETTLEMENT": return MiniGameGameplayPhase.Settlement;
                case "CLOSED": return MiniGameGameplayPhase.Closed;
                case "PLAYING": return MiniGameGameplayPhase.Drawing;
                default: return null;
            }
        }
    }
}
