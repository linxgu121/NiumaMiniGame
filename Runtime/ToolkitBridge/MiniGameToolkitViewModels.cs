using System;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using NiumaUI.Toolkit.Common;
using UnityEngine;

namespace NiumaMiniGame.ToolkitBridge
{
    public enum MiniGameStartPage
    {
        Home = 0,
        Naming = 1,
        Prepare = 2,
        RoomInput = 3,
        RoomLobby = 4
    }

    public enum MiniGameBrushTool
    {
        Pencil = 0,
        Pen = 1,
        Eraser = 2
    }

    [Serializable]
    public sealed class MiniGameToolkitModeOption
    {
        [Tooltip("模式 ID。必须与后端 ModeConfig 的 modeId 一致，例如 draw_telephone。")]
        public string ModeId = "draw_telephone";

        [Tooltip("显示给玩家看的模式名称。为空时显示 ModeId。")]
        public string DisplayName = "你画我猜";

        [Tooltip("该模式在房间大厅显示的图片。把 RoomPanel 里的模式展示 Image 绑到 Binding 后，这里每个模式可配置自己的图。")]
        public Sprite DisplaySprite;
    }

    public sealed class MiniGameStartPanelViewModel : UIPanelViewModelBase
    {
        public readonly System.Collections.Generic.List<ToolkitTextRowData> PlayerRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public readonly System.Collections.Generic.List<ToolkitTextRowData> ViewerRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public readonly System.Collections.Generic.List<ToolkitTextRowData> ChatRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public readonly System.Collections.Generic.List<ToolkitTextRowData> ModeRows = new System.Collections.Generic.List<ToolkitTextRowData>();

        public MiniGameStartPage Page { get; private set; }
        public MiniGamePanelViewData Panel { get; private set; }
        public string DisplayNameInput { get; set; }
        public string RoomIdInput { get; set; }
        public string ChatInput { get; set; }
        public bool JoinAsViewer { get; private set; }
        public string SelectedModeId { get; private set; }
        public string ToastText { get; private set; }

        public void Apply(MiniGameStartUIUpdate update, MiniGameToolkitModeOption[] modes, int maxRows)
        {
            var nextPanel = update.PanelData;
            SetContext(nextPanel?.RoomId);
            Panel = nextPanel;

            if (Panel?.Room != null)
            {
                Page = MiniGameStartPage.RoomLobby;
                SelectedModeId = Normalize(Panel.Room.ModeId, SelectedModeId);
            }
            else if (Page == MiniGameStartPage.RoomLobby)
            {
                Page = MiniGameStartPage.Home;
            }

            if (string.IsNullOrWhiteSpace(SelectedModeId))
                SelectedModeId = FirstModeId(modes);

            ToastText = Panel?.LastToast?.Text;
            if (string.IsNullOrWhiteSpace(ToastText))
                ToastText = Panel?.LastError?.DebugMessage;

            RebuildRows(modes, maxRows);
            MarkDirty();
        }

        public void ShowHome()
        {
            Page = MiniGameStartPage.Home;
            JoinAsViewer = false;
            MarkDirty();
        }

        public void ShowNaming()
        {
            Page = MiniGameStartPage.Naming;
            MarkDirty();
        }

        public void ShowPrepare()
        {
            Page = MiniGameStartPage.Prepare;
            JoinAsViewer = false;
            MarkDirty();
        }

        public void ShowRoomInput(bool asViewer)
        {
            Page = MiniGameStartPage.RoomInput;
            JoinAsViewer = asViewer;
            MarkDirty();
        }

        public void SelectMode(string modeId)
        {
            SelectedModeId = Normalize(modeId, SelectedModeId);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Page = MiniGameStartPage.Home;
            Panel = null;
            DisplayNameInput = null;
            RoomIdInput = null;
            ChatInput = null;
            JoinAsViewer = false;
            SelectedModeId = null;
            ToastText = null;
            PlayerRows.Clear();
            ViewerRows.Clear();
            ChatRows.Clear();
            ModeRows.Clear();
        }

        private void RebuildRows(MiniGameToolkitModeOption[] modes, int maxRows)
        {
            PlayerRows.Clear();
            ViewerRows.Clear();
            ChatRows.Clear();
            ModeRows.Clear();

            var limit = Mathf.Max(1, maxRows);
            var players = Panel?.Room?.Players ?? Array.Empty<MiniGamePlayerViewData>();
            for (var i = 0; i < players.Length && i < limit; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;
                PlayerRows.Add(new ToolkitTextRowData(p.PlayerId, MiniGameToolkitText.PlayerLine(p), false, p.IsConnected, p));
            }

            var viewers = Panel?.Room?.Viewers ?? Array.Empty<MiniGamePlayerViewData>();
            for (var i = 0; i < viewers.Length && i < limit; i++)
            {
                var p = viewers[i];
                if (p == null)
                    continue;
                ViewerRows.Add(new ToolkitTextRowData(p.PlayerId, MiniGameToolkitText.PlayerLine(p), false, p.IsConnected, p));
            }

            var chats = Panel?.Chats ?? Array.Empty<MiniGameChatViewData>();
            for (var i = Mathf.Max(0, chats.Length - limit); i < chats.Length; i++)
            {
                var chat = chats[i];
                if (chat == null)
                    continue;
                ChatRows.Add(new ToolkitTextRowData($"chat:{i}", $"{MiniGameToolkitText.Text(chat.DisplayName, chat.PlayerId)}：{chat.Text}", false, true, chat));
            }

            if (modes != null)
            {
                for (var i = 0; i < modes.Length; i++)
                {
                    var option = modes[i];
                    if (option == null || string.IsNullOrWhiteSpace(option.ModeId))
                        continue;
                    var id = option.ModeId.Trim();
                    var label = MiniGameToolkitText.Text(option.DisplayName, id);
                    ModeRows.Add(new ToolkitTextRowData(id, label, string.Equals(id, SelectedModeId, StringComparison.Ordinal), true, option));
                }
            }
        }

        private static string FirstModeId(MiniGameToolkitModeOption[] modes)
        {
            if (modes != null)
            {
                for (var i = 0; i < modes.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(modes[i]?.ModeId))
                        return modes[i].ModeId.Trim();
                }
            }

            return "draw_telephone";
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    public sealed class MiniGamePlayPanelViewModel : UIPanelViewModelBase
    {
        public readonly System.Collections.Generic.List<ToolkitTextRowData> PlayerRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public readonly System.Collections.Generic.List<ToolkitTextRowData> ChatRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public readonly System.Collections.Generic.List<ToolkitTextRowData> EvaluationRows = new System.Collections.Generic.List<ToolkitTextRowData>();

        public MiniGamePanelViewData Panel { get; private set; }
        public MiniGameGameplayViewData Gameplay { get; private set; }
        public MiniGameBrushTool BrushTool { get; set; }
        public Color BrushColor { get; set; } = Color.black;
        public float BrushSize { get; set; } = 6f;
        public string ChatInput { get; set; }
        public string AnswerInput { get; set; }
        public string CurrentStrokeId { get; set; }
        public string CurrentStrokeGroupId { get; set; }

        public void Apply(MiniGamePlayUIUpdate update, int maxRows)
        {
            var nextPanel = update.PanelData;
            var nextGameplay = update.Gameplay;
            SetContext(nextGameplay?.RoomId ?? nextPanel?.RoomId);
            Panel = nextPanel;
            Gameplay = nextGameplay;
            RebuildRows(maxRows);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Panel = null;
            Gameplay = null;
            ChatInput = null;
            AnswerInput = null;
            CurrentStrokeId = null;
            CurrentStrokeGroupId = null;
            PlayerRows.Clear();
            ChatRows.Clear();
            EvaluationRows.Clear();
        }

        private void RebuildRows(int maxRows)
        {
            PlayerRows.Clear();
            ChatRows.Clear();
            EvaluationRows.Clear();

            var limit = Mathf.Max(1, maxRows);
            var players = Gameplay?.Players ?? Panel?.Room?.Players ?? Array.Empty<MiniGamePlayerViewData>();
            for (var i = 0; i < players.Length && i < limit; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;
                PlayerRows.Add(new ToolkitTextRowData(p.PlayerId, MiniGameToolkitText.PlayerLine(p), false, p.IsConnected, p));
            }

            var chats = Gameplay?.Chats ?? Panel?.Chats ?? Array.Empty<MiniGameChatViewData>();
            for (var i = Mathf.Max(0, chats.Length - limit); i < chats.Length; i++)
            {
                var chat = chats[i];
                if (chat == null)
                    continue;
                ChatRows.Add(new ToolkitTextRowData($"chat:{i}", $"{MiniGameToolkitText.Text(chat.DisplayName, chat.PlayerId)}：{chat.Text}", false, true, chat));
            }

            var evaluations = Gameplay?.Evaluations ?? Array.Empty<MiniGameEvaluationViewData>();
            for (var i = 0; i < evaluations.Length && i < limit; i++)
            {
                var e = evaluations[i];
                if (e == null)
                    continue;
                var state = e.HasEvaluated ? (e.Agreed ? "赞同" : "不赞同") : "未评价";
                EvaluationRows.Add(new ToolkitTextRowData(e.PlayerId, $"{MiniGameToolkitText.Text(e.DisplayName, e.PlayerId)}：{state}", e.IsLocalPlayer, e.CanEvaluate, e));
            }
        }
    }

    internal static class MiniGameToolkitText
    {
        public static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }

        public static string PlayerLine(MiniGamePlayerViewData player)
        {
            if (player == null)
                return string.Empty;

            var name = Text(player.DisplayName, player.PlayerId);
            var state = Text(player.PlayerStateText, player.PlayerState.ToString());
            var role = player.IsViewer ? "观战" : "玩家";
            var host = player.IsHost ? " 房主" : string.Empty;
            var ready = player.IsReady ? " 已准备" : " 未准备";
            var local = player.IsLocalPlayer ? " 本机" : string.Empty;
            var connected = player.IsConnected ? string.Empty : " 离线";
            return $"{name} [{role}/{state}]{host}{ready}{local}{connected}";
        }

        public static bool IsVisible(MiniGameUIAccessState state)
        {
            return state != MiniGameUIAccessState.Hidden;
        }

        public static bool IsOpen(MiniGameUIAccessState state)
        {
            return state == MiniGameUIAccessState.Open;
        }
    }
}
