using System.Text;
using NiumaMiniGame.Bridge;
using NiumaMiniGame.Controller;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NiumaMiniGame.UIBridge
{
    /// <summary>
    /// 你画我猜 2D 开始界面 UI。
    /// 负责把 MiniGamePanelViewData 显示到 Canvas，并把按钮点击转成 Controller 命令。
    /// </summary>
    public sealed class MiniGameStartScreenUI : MonoBehaviour, IMiniGameUIReceiver
    {
        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动查找场景中的 NiumaMiniGameController。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("为 true 时，启用组件会自动查找未绑定的 NiumaMiniGameController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindController = true;

        [Header("面板节点")]
        [Tooltip("开始界面根节点。为空时不主动控制显示隐藏。")]
        [SerializeField] private GameObject startRoot;

        [Tooltip("未进入房间时显示的创建 / 加入面板。")]
        [SerializeField] private GameObject entryPanel;

        [Tooltip("进入房间后显示的房间大厅面板。")]
        [SerializeField] private GameObject roomPanel;

        [Header("输入框")]
        [Tooltip("玩家昵称输入框。创建或加入房间前会写入 MiniGameController。")]
        [SerializeField] private TMP_InputField displayNameInput;

        [Tooltip("房间 ID 输入框。加入房间或观战时使用。")]
        [SerializeField] private TMP_InputField roomIdInput;

        [Tooltip("模式 ID 输入框。为空时使用 draw_telephone。")]
        [SerializeField] private TMP_InputField modeIdInput;

        [Tooltip("聊天输入框。第一版只在已进入房间后发送普通房间聊天。")]
        [SerializeField] private TMP_InputField chatInput;

        [Header("按钮")]
        [Tooltip("连接按钮。Mock 模式下也需要连接，连接后才能创建或加入房间。")]
        [SerializeField] private Button connectButton;

        [Tooltip("创建房间按钮。")]
        [SerializeField] private Button createRoomButton;

        [Tooltip("作为玩家加入房间按钮。")]
        [SerializeField] private Button joinRoomButton;

        [Tooltip("作为观战者加入房间按钮。")]
        [SerializeField] private Button joinAsViewerButton;

        [Tooltip("准备按钮。房间人数满足后，所有玩家准备即可进入游戏流程。")]
        [SerializeField] private Button readyButton;

        [Tooltip("取消准备按钮。")]
        [SerializeField] private Button unreadyButton;

        [Tooltip("离开房间按钮。")]
        [SerializeField] private Button leaveRoomButton;

        [Tooltip("发送聊天按钮。")]
        [SerializeField] private Button sendChatButton;

        [Header("文本显示")]
        [Tooltip("连接状态文本。")]
        [SerializeField] private TMP_Text connectionText;

        [Tooltip("房间状态文本。")]
        [SerializeField] private TMP_Text roomText;

        [Tooltip("玩家列表文本。")]
        [SerializeField] private TMP_Text playersText;

        [Tooltip("观战者列表文本。")]
        [SerializeField] private TMP_Text viewersText;

        [Tooltip("提示文本，用于显示当前可执行操作。")]
        [SerializeField] private TMP_Text hintText;

        [Tooltip("错误文本，用于显示服务器或 Mock 返回的错误。")]
        [SerializeField] private TMP_Text errorText;

        [Header("默认配置")]
        [Tooltip("默认模式 ID。当前你画我猜使用 draw_telephone。")]
        [SerializeField] private string defaultModeId = "draw_telephone";

        [Tooltip("为 true 时，开始界面显示期间解锁并显示鼠标，避免从 3D 场景切入后鼠标仍被 TPC 锁定。")]
        [SerializeField] private bool unlockCursorWhileVisible = true;

        [Tooltip("为 true 时，缺少引用或 EventSystem 时输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly StringBuilder _builder = new StringBuilder(512);
        private CursorLockMode _previousLockMode;
        private bool _previousCursorVisible;
        private bool _hasCursorSnapshot;

        private void Awake()
        {
            BindButtons();
            EnsureDefaultInputValues();
        }

        private void OnEnable()
        {
            ResolveController(false);
            BindButtons();
            EnsureDefaultInputValues();
            ApplyVisibleCursorState();
            WarnIfEventSystemMissing();
        }

        private void OnDisable()
        {
            RestoreCursorState();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        public void ApplyMiniGameUpdate(MiniGameUIUpdate update)
        {
            var panel = update.PanelData;
            RefreshPanels(panel);
            RefreshTexts(panel);
            RefreshButtonStates(panel);
        }

        public void ClickConnect()
        {
            if (!ResolveController(true))
            {
                return;
            }

            WriteInputValuesToController();
            miniGameController.Connect();
        }

        public void ClickCreateRoom()
        {
            if (!ResolveController(true))
            {
                return;
            }

            WriteInputValuesToController();
            EnsureConnected();
            miniGameController.CreateRoom(ReadModeId());
        }

        public void ClickJoinRoom()
        {
            JoinRoom(false);
        }

        public void ClickJoinAsViewer()
        {
            JoinRoom(true);
        }

        public void ClickReady()
        {
            if (ResolveController(true))
            {
                miniGameController.SetReady(true);
            }
        }

        public void ClickUnready()
        {
            if (ResolveController(true))
            {
                miniGameController.SetReady(false);
            }
        }

        public void ClickLeaveRoom()
        {
            if (ResolveController(true))
            {
                miniGameController.LeaveRoom();
            }
        }

        public void ClickSendChat()
        {
            if (!ResolveController(true) || chatInput == null)
            {
                return;
            }

            var text = chatInput.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (miniGameController.SendChat(text))
            {
                chatInput.text = string.Empty;
            }
        }

        private void JoinRoom(bool asViewer)
        {
            if (!ResolveController(true))
            {
                return;
            }

            var roomId = roomIdInput == null ? null : roomIdInput.text;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                SetText(errorText, "请输入房间 ID。");
                return;
            }

            WriteInputValuesToController();
            EnsureConnected();
            miniGameController.JoinRoom(roomId.Trim().ToUpperInvariant(), asViewer);
        }

        private void EnsureConnected()
        {
            if (miniGameController != null && !miniGameController.IsConnected)
            {
                miniGameController.Connect();
            }
        }

        private void WriteInputValuesToController()
        {
            if (displayNameInput != null)
            {
                miniGameController.SetDisplayName(displayNameInput.text);
            }
        }

        private string ReadModeId()
        {
            if (modeIdInput == null || string.IsNullOrWhiteSpace(modeIdInput.text))
            {
                return defaultModeId;
            }

            return modeIdInput.text.Trim();
        }

        private void RefreshPanels(MiniGamePanelViewData panel)
        {
            var hasRoom = panel != null && !string.IsNullOrWhiteSpace(panel.RoomId);
            SetActive(startRoot, true);
            SetActive(entryPanel, !hasRoom);
            SetActive(roomPanel, hasRoom);
        }

        private void RefreshTexts(MiniGamePanelViewData panel)
        {
            if (panel == null)
            {
                SetText(connectionText, "未连接");
                SetText(roomText, "未进入房间");
                SetText(playersText, string.Empty);
                SetText(viewersText, string.Empty);
                SetText(hintText, "输入昵称后，可以创建房间或输入房间号加入。");
                SetText(errorText, string.Empty);
                return;
            }

            SetText(connectionText, panel.IsConnected
                ? $"已连接  玩家ID：{ShortId(panel.LocalPlayerId)}"
                : "未连接");

            if (panel.Room == null)
            {
                SetText(roomText, "未进入房间");
                SetText(playersText, string.Empty);
                SetText(viewersText, string.Empty);
                SetText(hintText, "创建房间，或输入房间号加入好友房间。");
            }
            else
            {
                SetText(roomText, BuildRoomSummary(panel.Room, panel.IsLocalViewer));
                SetText(playersText, BuildPlayerList("玩家", panel.Room.Players));
                SetText(viewersText, BuildPlayerList("观战", panel.Room.Viewers));
                SetText(hintText, BuildHint(panel));
            }

            SetText(errorText, BuildErrorText(panel.LastError));
        }

        private void RefreshButtonStates(MiniGamePanelViewData panel)
        {
            var connected = panel != null && panel.IsConnected;
            var hasRoom = panel != null && !string.IsNullOrWhiteSpace(panel.RoomId);
            var inLobby = panel?.Room != null && panel.Room.State == MiniGameRoomState.Lobby;
            var isViewer = panel != null && panel.IsLocalViewer;

            SetInteractable(connectButton, !connected);
            SetInteractable(createRoomButton, connected && !hasRoom);
            SetInteractable(joinRoomButton, connected && !hasRoom);
            SetInteractable(joinAsViewerButton, connected && !hasRoom);
            SetInteractable(readyButton, connected && hasRoom && inLobby && !isViewer);
            SetInteractable(unreadyButton, connected && hasRoom && inLobby && !isViewer);
            SetInteractable(leaveRoomButton, connected && hasRoom);
            SetInteractable(sendChatButton, connected && hasRoom);
        }

        private string BuildRoomSummary(MiniGameRoomViewData room, bool isLocalViewer)
        {
            return $"房间：{room.RoomId}\n模式：{room.ModeId}\n状态：{room.State}\n身份：{(isLocalViewer ? "观战者" : "玩家")}";
        }

        private string BuildPlayerList(string title, MiniGamePlayerViewData[] players)
        {
            _builder.Clear();
            _builder.Append(title).Append("：");

            if (players == null || players.Length == 0)
            {
                _builder.Append("无");
                return _builder.ToString();
            }

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                _builder.AppendLine();
                _builder.Append(player.IsLocalPlayer ? "我 " : "- ");
                _builder.Append(string.IsNullOrWhiteSpace(player.DisplayName) ? ShortId(player.PlayerId) : player.DisplayName);

                if (!player.IsViewer)
                {
                    _builder.Append(player.IsReady ? "  已准备" : "  未准备");
                }

                if (!player.IsConnected)
                {
                    _builder.Append("  离线");
                }
            }

            return _builder.ToString();
        }

        private static string BuildHint(MiniGamePanelViewData panel)
        {
            if (panel.Room == null)
            {
                return "等待进入房间。";
            }

            switch (panel.Room.State)
            {
                case MiniGameRoomState.Lobby:
                    return panel.IsLocalViewer ? "正在观战大厅，等待房主或玩家开始。" : "点击准备，等待其他玩家准备。";
                case MiniGameRoomState.Preparing:
                    return "游戏正在准备。";
                case MiniGameRoomState.Playing:
                    return "游戏进行中，下一步会接入绘画 / 猜词界面。";
                case MiniGameRoomState.Review:
                    return "正在展示原词和玩家猜测，玩家可以聊天并评分。";
                case MiniGameRoomState.Voting:
                    return "正在评分阶段。";
                case MiniGameRoomState.Settlement:
                    return "游戏结算中。";
                case MiniGameRoomState.Closed:
                    return "房间已关闭。";
                default:
                    return "等待房间状态同步。";
            }
        }

        private static string BuildErrorText(MiniGameErrorViewData error)
        {
            if (error == null || string.IsNullOrWhiteSpace(error.ErrorCode))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(error.DebugMessage)
                ? $"错误：{error.ErrorCode}"
                : $"错误：{error.ErrorCode}\n{error.DebugMessage}";
        }

        private void BindButtons()
        {
            BindButton(connectButton, ClickConnect);
            BindButton(createRoomButton, ClickCreateRoom);
            BindButton(joinRoomButton, ClickJoinRoom);
            BindButton(joinAsViewerButton, ClickJoinAsViewer);
            BindButton(readyButton, ClickReady);
            BindButton(unreadyButton, ClickUnready);
            BindButton(leaveRoomButton, ClickLeaveRoom);
            BindButton(sendChatButton, ClickSendChat);
        }

        private void UnbindButtons()
        {
            UnbindButton(connectButton, ClickConnect);
            UnbindButton(createRoomButton, ClickCreateRoom);
            UnbindButton(joinRoomButton, ClickJoinRoom);
            UnbindButton(joinAsViewerButton, ClickJoinAsViewer);
            UnbindButton(readyButton, ClickReady);
            UnbindButton(unreadyButton, ClickUnready);
            UnbindButton(leaveRoomButton, ClickLeaveRoom);
            UnbindButton(sendChatButton, ClickSendChat);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void EnsureDefaultInputValues()
        {
            if (displayNameInput != null && string.IsNullOrWhiteSpace(displayNameInput.text) && miniGameController != null)
            {
                displayNameInput.text = miniGameController.DisplayName;
            }

            if (modeIdInput != null && string.IsNullOrWhiteSpace(modeIdInput.text))
            {
                modeIdInput.text = defaultModeId;
            }
        }

        private bool ResolveController(bool warn)
        {
            if (miniGameController != null)
            {
                return true;
            }

            if (autoFindController)
            {
#if UNITY_2023_1_OR_NEWER
                miniGameController = FindFirstObjectByType<NiumaMiniGameController>();
#else
                miniGameController = FindObjectOfType<NiumaMiniGameController>();
#endif
            }

            if (miniGameController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameStartScreenUI] 未找到 NiumaMiniGameController，开始界面按钮不会生效。", this);
            }

            return miniGameController != null;
        }

        private void WarnIfEventSystemMissing()
        {
            if (logWarnings && EventSystem.current == null)
            {
                Debug.LogWarning("[MiniGameStartScreenUI] 当前场景没有 EventSystem，Unity UI Button 无法响应点击。请创建 UI/EventSystem。", this);
            }
        }

        private void ApplyVisibleCursorState()
        {
            if (!unlockCursorWhileVisible)
            {
                return;
            }

            if (!_hasCursorSnapshot)
            {
                _previousLockMode = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                _hasCursorSnapshot = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursorState()
        {
            if (!unlockCursorWhileVisible || !_hasCursorSnapshot)
            {
                return;
            }

            Cursor.lockState = _previousLockMode;
            Cursor.visible = _previousCursorVisible;
            _hasCursorSnapshot = false;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetInteractable(Selectable target, bool value)
        {
            if (target != null)
            {
                target.interactable = value;
            }
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未知";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }
    }
}
