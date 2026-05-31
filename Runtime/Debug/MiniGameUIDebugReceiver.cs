using NiumaMiniGame.Bridge;
using UnityEngine;

namespace NiumaMiniGame.Debugging
{
    /// <summary>
    /// MiniGame UI 桥接调试接收器。
    /// 挂到场景中即可在 Inspector 里观察 Bridge 推送结果，正式 UI 不依赖它。
    /// </summary>
    public sealed class MiniGameUIDebugReceiver : MonoBehaviour, IMiniGameUIReceiver
    {
        [Header("运行时状态（只读）")]
        [SerializeField] private int lastRevision;
        [SerializeField] private string lastUpdateType;
        [SerializeField] private string lastRoomId;
        [SerializeField] private string lastRoomState;
        [SerializeField] private int lastPlayerCount;
        [SerializeField] private int lastViewerCount;
        [SerializeField] private string lastTaskSummary;
        [SerializeField] private string lastErrorCode;
        [SerializeField] private string lastSummary;

        public void ApplyMiniGameUpdate(MiniGameUIUpdate update)
        {
            lastRevision = update.Revision;
            lastUpdateType = update.UpdateType.ToString();

            var panel = update.PanelData;
            if (panel == null)
            {
                lastRoomId = null;
                lastRoomState = null;
                lastPlayerCount = 0;
                lastViewerCount = 0;
                lastTaskSummary = null;
                lastErrorCode = null;
                lastSummary = $"Update={lastUpdateType}, Revision={lastRevision}, Panel=null";
                return;
            }

            lastRoomId = panel.RoomId;
            lastRoomState = panel.Room != null ? panel.Room.State.ToString() : "None";
            lastPlayerCount = panel.Room?.Players?.Length ?? 0;
            lastViewerCount = panel.Room?.Viewers?.Length ?? 0;
            lastTaskSummary = panel.CurrentTask != null
                ? $"{panel.CurrentTask.ActionType} Stage={panel.CurrentTask.StageIndex} Chain={panel.CurrentTask.ChainId}"
                : "None";
            lastErrorCode = panel.LastError?.ErrorCode;
            lastSummary = $"Room={lastRoomId}, State={lastRoomState}, Players={lastPlayerCount}, Viewers={lastViewerCount}, Task={lastTaskSummary}, LastMessage={panel.LastMessageType}";
        }
    }
}
