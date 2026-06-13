using NiumaMiniGame.Controller;
using NiumaMiniGame.Drawing;
using UnityEngine;

namespace NiumaMiniGame.ToolkitBridge
{
    /// <summary>
    /// MiniGame Toolkit UI 命令中继。
    /// 策划在 ToolkitBindingProvider 的 UnityEvent 中拖这个脚本，即可调用 MiniGameController 的公开命令。
    /// </summary>
    public sealed class MiniGameToolkitCommandRelay : MonoBehaviour
    {
        [Header("MiniGame 控制器")]
        [Tooltip("拖场景中的 NiumaMiniGameController。为空时会自动查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("缺少控制器或命令失败时输出警告。")]
        [SerializeField] private bool logWarnings = true;

        public void SetDisplayName(string displayName)
        {
            if (EnsureController())
                miniGameController.SetDisplayName(displayName);
        }

        public void CreateRoom(string modeId)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.CreateRoom(modeId), "创建房间失败");
        }

        public void JoinRoom(string roomId, bool asViewer)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.JoinRoom(roomId, asViewer), asViewer ? "观战加入房间失败" : "加入房间失败");
        }

        public void JoinRoomAsPlayer(string roomId)
        {
            JoinRoom(roomId, false);
        }

        public void JoinRoomAsViewer(string roomId)
        {
            JoinRoom(roomId, true);
        }

        public void LeaveRoom()
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.LeaveRoom(), "离开房间失败");
        }

        public void SetReady(bool ready)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SetReady(ready), ready ? "准备失败" : "取消准备失败");
        }

        public void StartGame()
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.StartGame(), "开始游戏失败");
        }

        public void SwitchRole(bool asViewer)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SwitchRole(asViewer), asViewer ? "切换到观战失败" : "切换到玩家失败");
        }

        public void ChangeMode(string modeId)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.ChangeMode(modeId), "切换模式失败");
        }

        public void SendChat(string text)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SendChat(text), "发送聊天失败");
        }

        public void SendGift(string giftType)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SendGift(giftType, null), "发送礼物失败");
        }

        public void ClearCanvas()
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.RequestClearCanvas(), "清空画布失败");
        }

        public void UndoStroke(string strokeId)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.RequestUndoStroke(strokeId), "撤销笔画失败");
        }

        public void SendStrokeBegin(string strokeId, Color color, float brushSize)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SendStrokeBegin(strokeId, color, brushSize), "发送笔画开始失败");
        }

        public void SendStrokePointBatch(StrokePointBatch batch)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SendStrokePointBatch(batch), "发送绘画点位失败");
        }

        public void SendStrokeEnd(string strokeId, int totalPoints)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SendStrokeEnd(strokeId, totalPoints), "发送笔画结束失败");
        }

        public void SubmitDrawing(string strokeGroupId)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SubmitSequentialDrawing(strokeGroupId), "提交作画失败");
        }

        public void SubmitAnswer(string answerText)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SubmitSequentialAnswer(answerText), "提交答案失败");
        }

        public void SubmitEvaluation(bool agreed)
        {
            if (EnsureController())
                WarnIfFailed(miniGameController.SubmitSequentialEvaluation(agreed), "提交评价失败");
        }

        private bool EnsureController()
        {
            if (miniGameController != null)
                return true;

#if UNITY_2023_1_OR_NEWER
            miniGameController = FindFirstObjectByType<NiumaMiniGameController>();
#else
            miniGameController = FindObjectOfType<NiumaMiniGameController>();
#endif
            if (miniGameController != null)
                return true;

            Warn("未找到 NiumaMiniGameController。请把该脚本挂在 MiniGameUIRoot 或 CommandRelay 物体上，并拖入控制器。 ");
            return false;
        }

        private void WarnIfFailed(bool succeeded, string message)
        {
            if (!succeeded)
                Warn(message);
        }

        private void Warn(string message)
        {
            if (logWarnings)
                Debug.LogWarning($"[MiniGameToolkitCommandRelay] {message}", this);
        }
    }
}
