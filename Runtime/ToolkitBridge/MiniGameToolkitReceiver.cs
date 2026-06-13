using NiumaMiniGame.Bridge;
using NiumaMiniGame.Enum;
using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaMiniGame.ToolkitBridge
{
    /// <summary>
    /// MiniGame UI Toolkit 组合接收器。
    /// 把现有 MiniGameUIViewBridge 的单一更新拆到开始页和游戏中页两个 Toolkit View。
    /// </summary>
    public sealed class MiniGameToolkitReceiver : MonoBehaviour, IMiniGameUIReceiver
    {
        [Header("UI Toolkit 管理器")]
        [Tooltip("拖核心场景或当前场景中的 UIToolkitUIManager。为空时会自动查找。")]
        [SerializeField] private UIToolkitUIManager uiManager;

        [Header("ViewId")]
        [Tooltip("MiniGame 开始 / 取名 / 预备 / 房间大厅 ViewId，需要在 UIToolkitViewRegistrySO 中注册。")]
        [SerializeField] private string startViewId = "MiniGameStartPanel";

        [Tooltip("MiniGame 游戏中 ViewId，需要在 UIToolkitViewRegistrySO 中注册。")]
        [SerializeField] private string playViewId = "MiniGamePlayPanel";

        [Header("切换策略")]
        [Tooltip("收到 Cleared 更新时是否关闭两个 MiniGame View。")]
        [SerializeField] private bool closeOnCleared = true;

        [Tooltip("从大厅切入游戏中时关闭开始页；从游戏结束回大厅时关闭游戏中页。")]
        [SerializeField] private bool closeInactiveViewOnSwitch = true;

        [Tooltip("Refresh 失败时是否自动 OpenView。")]
        [SerializeField] private bool autoOpenView = true;

        [Header("调试")]
        [Tooltip("缺少 UIToolkitUIManager 或 ViewId 未注册时输出警告。")]
        [SerializeField] private bool logWarnings = true;

        public void ApplyMiniGameUpdate(MiniGameUIUpdate update)
        {
            if (!EnsureUIManager())
                return;

            if (update.UpdateType == MiniGameUIUpdateType.Cleared)
            {
                if (closeOnCleared)
                {
                    CloseView(startViewId);
                    CloseView(playViewId);
                    return;
                }
            }

            var usePlayView = ShouldUsePlayView(update);
            if (usePlayView)
            {
                if (closeInactiveViewOnSwitch)
                    CloseView(startViewId);
                RefreshOrOpen(playViewId, new MiniGamePlayUIUpdate(update));
                return;
            }

            if (closeInactiveViewOnSwitch)
                CloseView(playViewId);
            RefreshOrOpen(startViewId, new MiniGameStartUIUpdate(update));
        }

        private bool ShouldUsePlayView(MiniGameUIUpdate update)
        {
            var gameplay = update.PanelData?.Gameplay;
            if (gameplay != null && gameplay.Phase != MiniGameGameplayPhase.None && gameplay.Phase != MiniGameGameplayPhase.Lobby)
                return true;

            var state = update.PanelData?.Room?.State ?? MiniGameRoomState.None;
            return state == MiniGameRoomState.Playing
                   || state == MiniGameRoomState.Review
                   || state == MiniGameRoomState.Voting
                   || state == MiniGameRoomState.Settlement;
        }

        private void RefreshOrOpen(string viewId, object update)
        {
            if (string.IsNullOrWhiteSpace(viewId))
                return;

            if (uiManager.RefreshView(viewId, update))
                return;

            if (autoOpenView && !uiManager.OpenView(viewId, update))
                Warn($"无法打开或刷新 View：{viewId}。请确认 UIToolkitViewRegistrySO 已注册该 ViewId 和 BindingProviderId。 ");
        }

        private void CloseView(string viewId)
        {
            if (!string.IsNullOrWhiteSpace(viewId))
                uiManager.CloseView(viewId);
        }

        private bool EnsureUIManager()
        {
            if (uiManager != null)
                return true;

#if UNITY_2023_1_OR_NEWER
            uiManager = FindFirstObjectByType<UIToolkitUIManager>();
#else
            uiManager = FindObjectOfType<UIToolkitUIManager>();
#endif
            if (uiManager != null)
                return true;

            Warn("未找到 UIToolkitUIManager，MiniGame Toolkit UI 无法显示。请在核心场景 UIRoot 上挂 UIToolkitUIManager，或手动拖入。 ");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings)
                Debug.LogWarning($"[MiniGameToolkitReceiver] {message}", this);
        }
    }
}
