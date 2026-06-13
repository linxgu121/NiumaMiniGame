using System;
using NiumaMiniGame.Bridge;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace NiumaMiniGame.ToolkitBridge
{
    public sealed class MiniGameStartToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }
        [Serializable] public sealed class StringBoolEvent : UnityEvent<string, bool> { }

        [Header("页面根节点 name")]
        [SerializeField, Tooltip("入口页根节点 name，显示开始游戏和退出游戏。")]
        private string homeRootName = "HomePage";
        [SerializeField, Tooltip("取名页根节点 name，玩家输入昵称。")]
        private string namingRootName = "NamingPage";
        [SerializeField, Tooltip("预备页根节点 name，显示创建房间、加入房间、观战加入、返回。")]
        private string prepareRootName = "PreparePage";
        [SerializeField, Tooltip("房间号输入页根节点 name，加入房间和观战加入共用。")]
        private string roomInputRootName = "RoomInputPage";
        [SerializeField, Tooltip("房间大厅页根节点 name，显示模式、房间号、玩家、观战者、聊天。")]
        private string lobbyRootName = "RoomLobbyPage";

        [Header("文本与输入 name")]
        [SerializeField, Tooltip("状态/错误提示 Label 的 name。")]
        private string statusLabelName = "StatusText";
        [SerializeField, Tooltip("房间号 Label 的 name。")]
        private string roomIdLabelName = "RoomIdText";
        [SerializeField, Tooltip("模式名称 Label 的 name。")]
        private string modeLabelName = "ModeNameText";
        [SerializeField, Tooltip("玩家数量 Label 的 name。")]
        private string playerCountLabelName = "PlayerCountText";
        [SerializeField, Tooltip("昵称输入 TextField 的 name。")]
        private string displayNameInputName = "DisplayNameInput";
        [SerializeField, Tooltip("房间号输入 TextField 的 name。")]
        private string roomIdInputName = "RoomIdInput";
        [SerializeField, Tooltip("聊天输入 TextField 的 name。")]
        private string chatInputName = "ChatInput";
        [SerializeField, Tooltip("模式展示 Image 的 name。每个 ModeOptions 元素可配置自己的 DisplaySprite。")]
        private string modeImageName = "ModeDisplayImage";

        [Header("列表 name")]
        [SerializeField, Tooltip("玩家列表 ListView 的 name，只显示参赛玩家。")]
        private string playersListName = "PlayersList";
        [SerializeField, Tooltip("观战者列表 ListView 的 name，只显示观战者。")]
        private string viewersListName = "ViewersList";
        [SerializeField, Tooltip("聊天记录 ListView 的 name。")]
        private string chatListName = "ChatList";
        [SerializeField, Tooltip("模式列表 ListView 的 name。可为空，若不用列表可只使用 ModeNextButton 切换。")]
        private string modeListName = "ModeList";
        [SerializeField, Tooltip("空玩家列表节点 name。")]
        private string playersEmptyRootName = "PlayersEmptyRoot";
        [SerializeField, Tooltip("空观战者列表节点 name。")]
        private string viewersEmptyRootName = "ViewersEmptyRoot";
        [SerializeField, Tooltip("空聊天列表节点 name。")]
        private string chatEmptyRootName = "ChatEmptyRoot";
        [SerializeField, Tooltip("空模式列表节点 name。")]
        private string modeEmptyRootName = "ModeEmptyRoot";

        [Header("按钮 name")]
        [SerializeField, Tooltip("入口页开始游戏按钮 name。点击后进入取名页。")]
        private string enterGameButtonName = "EnterGameButton";
        [SerializeField, Tooltip("入口页退出游戏按钮 name。点击后触发 On Exit Game Requested，通常绑定场景返回。")]
        private string exitGameButtonName = "ExitGameButton";
        [SerializeField, Tooltip("取名页确认按钮 name。点击后设置昵称并进入预备页。")]
        private string confirmNameButtonName = "ConfirmNameButton";
        [SerializeField, Tooltip("取名页返回按钮 name。点击后回入口页。")]
        private string namingBackButtonName = "NamingBackButton";
        [SerializeField, Tooltip("预备页创建房间按钮 name。")]
        private string createRoomButtonName = "CreateRoomButton";
        [SerializeField, Tooltip("预备页加入房间按钮 name。点击后进入房间号输入页。")]
        private string joinRoomButtonName = "JoinRoomButton";
        [SerializeField, Tooltip("预备页观战加入按钮 name。点击后进入房间号输入页，并以观战身份加入。")]
        private string joinAsViewerButtonName = "JoinAsViewerButton";
        [SerializeField, Tooltip("预备页返回按钮 name。点击后回取名页。")]
        private string prepareBackButtonName = "PrepareBackButton";
        [SerializeField, Tooltip("房间号输入页进入按钮 name。")]
        private string roomInputEnterButtonName = "RoomInputEnterButton";
        [SerializeField, Tooltip("房间号输入页返回按钮 name。点击后回预备页。")]
        private string roomInputBackButtonName = "RoomInputBackButton";
        [SerializeField, Tooltip("房间大厅开始游戏按钮 name。只有房主应显示或可点。")]
        private string startGameButtonName = "StartGameButton";
        [SerializeField, Tooltip("房间大厅准备按钮 name。非房主玩家点击后进入已准备。")]
        private string readyButtonName = "ReadyButton";
        [SerializeField, Tooltip("房间大厅取消准备按钮 name。")]
        private string unreadyButtonName = "UnreadyButton";
        [SerializeField, Tooltip("房间大厅切换身份按钮 name。玩家与观战者互相切换。")]
        private string switchRoleButtonName = "SwitchRoleButton";
        [SerializeField, Tooltip("房间大厅离开房间按钮 name。")]
        private string leaveRoomButtonName = "LeaveRoomButton";
        [SerializeField, Tooltip("房间大厅发送聊天按钮 name。")]
        private string sendChatButtonName = "SendChatButton";
        [SerializeField, Tooltip("切换到下一个模式按钮 name。")]
        private string modeNextButtonName = "ModeNextButton";

        [Header("模式配置")]
        [SerializeField, Tooltip("房间可选模式。ModeId 必须与后端一致，DisplaySprite 是该模式对应展示图。")]
        private MiniGameToolkitModeOption[] modeOptions = Array.Empty<MiniGameToolkitModeOption>();
        [SerializeField, Tooltip("列表最多显示多少行。")]
        private int maxRows = 80;

        [Header("列表样式")]
        [SerializeField] private string rowClass = "niuma-minigame-row";
        [SerializeField] private string selectedRowClass = "is-selected";
        [SerializeField] private string disabledRowClass = "is-disabled";

        [Header("交互事件：拖 MiniGameToolkitCommandRelay 或 NiumaMiniGameController 对应方法")]
        [SerializeField, Tooltip("退出游戏按钮事件。通常绑定 MiniGameStartScreen 或场景模块的返回 RPG 方法。")]
        private UnityEvent onExitGameRequested = new UnityEvent();
        [SerializeField, Tooltip("确认昵称事件。绑定 MiniGameToolkitCommandRelay.SetDisplayName。")]
        private StringEvent onDisplayNameSubmitted = new StringEvent();
        [SerializeField, Tooltip("创建房间事件。绑定 MiniGameToolkitCommandRelay.CreateRoom。")]
        private StringEvent onCreateRoomRequested = new StringEvent();
        [SerializeField, Tooltip("加入房间事件。绑定 MiniGameToolkitCommandRelay.JoinRoom。第二个参数表示是否观战。")]
        private StringBoolEvent onJoinRoomRequested = new StringBoolEvent();
        [SerializeField, Tooltip("准备状态事件。绑定 MiniGameToolkitCommandRelay.SetReady。")]
        private BoolEvent onReadyChanged = new BoolEvent();
        [SerializeField, Tooltip("开始游戏事件。绑定 MiniGameToolkitCommandRelay.StartGame。")]
        private UnityEvent onStartGameRequested = new UnityEvent();
        [SerializeField, Tooltip("离开房间事件。绑定 MiniGameToolkitCommandRelay.LeaveRoom。")]
        private UnityEvent onLeaveRoomRequested = new UnityEvent();
        [SerializeField, Tooltip("切换身份事件。绑定 MiniGameToolkitCommandRelay.SwitchRole。true=观战，false=玩家。")]
        private BoolEvent onSwitchRoleRequested = new BoolEvent();
        [SerializeField, Tooltip("切换模式事件。绑定 MiniGameToolkitCommandRelay.ChangeMode。")]
        private StringEvent onChangeModeRequested = new StringEvent();
        [SerializeField, Tooltip("发送聊天事件。绑定 MiniGameToolkitCommandRelay.SendChat。")]
        private StringEvent onSendChatRequested = new StringEvent();

        protected override string DefaultProviderId => "MiniGameStartPanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new MiniGameStartToolkitBinding(
                homeRootName,
                namingRootName,
                prepareRootName,
                roomInputRootName,
                lobbyRootName,
                statusLabelName,
                roomIdLabelName,
                modeLabelName,
                playerCountLabelName,
                displayNameInputName,
                roomIdInputName,
                chatInputName,
                modeImageName,
                playersListName,
                viewersListName,
                chatListName,
                modeListName,
                playersEmptyRootName,
                viewersEmptyRootName,
                chatEmptyRootName,
                modeEmptyRootName,
                enterGameButtonName,
                exitGameButtonName,
                confirmNameButtonName,
                namingBackButtonName,
                createRoomButtonName,
                joinRoomButtonName,
                joinAsViewerButtonName,
                prepareBackButtonName,
                roomInputEnterButtonName,
                roomInputBackButtonName,
                startGameButtonName,
                readyButtonName,
                unreadyButtonName,
                switchRoleButtonName,
                leaveRoomButtonName,
                sendChatButtonName,
                modeNextButtonName,
                modeOptions,
                maxRows,
                rowClass,
                selectedRowClass,
                disabledRowClass,
                () => onExitGameRequested?.Invoke(),
                value => onDisplayNameSubmitted?.Invoke(value),
                modeId => onCreateRoomRequested?.Invoke(modeId),
                (roomId, asViewer) => onJoinRoomRequested?.Invoke(roomId, asViewer),
                ready => onReadyChanged?.Invoke(ready),
                () => onStartGameRequested?.Invoke(),
                () => onLeaveRoomRequested?.Invoke(),
                asViewer => onSwitchRoleRequested?.Invoke(asViewer),
                modeId => onChangeModeRequested?.Invoke(modeId),
                text => onSendChatRequested?.Invoke(text));
        }
    }

    public sealed class MiniGameStartToolkitBinding : ToolkitViewBindingBase<MiniGameStartUIUpdate, MiniGameStartPanelViewModel>
    {
        private readonly string _homeRootName;
        private readonly string _namingRootName;
        private readonly string _prepareRootName;
        private readonly string _roomInputRootName;
        private readonly string _lobbyRootName;
        private readonly string _statusName;
        private readonly string _roomIdName;
        private readonly string _modeName;
        private readonly string _playerCountName;
        private readonly string _displayNameInputName;
        private readonly string _roomIdInputName;
        private readonly string _chatInputName;
        private readonly string _modeImageName;
        private readonly string _playersListName;
        private readonly string _viewersListName;
        private readonly string _chatListName;
        private readonly string _modeListName;
        private readonly string _playersEmptyName;
        private readonly string _viewersEmptyName;
        private readonly string _chatEmptyName;
        private readonly string _modeEmptyName;
        private readonly string _enterGameButtonName;
        private readonly string _exitGameButtonName;
        private readonly string _confirmNameButtonName;
        private readonly string _namingBackButtonName;
        private readonly string _createRoomButtonName;
        private readonly string _joinRoomButtonName;
        private readonly string _joinAsViewerButtonName;
        private readonly string _prepareBackButtonName;
        private readonly string _roomInputEnterButtonName;
        private readonly string _roomInputBackButtonName;
        private readonly string _startGameButtonName;
        private readonly string _readyButtonName;
        private readonly string _unreadyButtonName;
        private readonly string _switchRoleButtonName;
        private readonly string _leaveRoomButtonName;
        private readonly string _sendChatButtonName;
        private readonly string _modeNextButtonName;
        private readonly MiniGameToolkitModeOption[] _modeOptions;
        private readonly int _maxRows;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private readonly Action _exitGame;
        private readonly Action<string> _displayNameSubmitted;
        private readonly Action<string> _createRoom;
        private readonly Action<string, bool> _joinRoom;
        private readonly Action<bool> _readyChanged;
        private readonly Action _startGame;
        private readonly Action _leaveRoom;
        private readonly Action<bool> _switchRole;
        private readonly Action<string> _changeMode;
        private readonly Action<string> _sendChat;

        private VisualElement _homeRoot;
        private VisualElement _namingRoot;
        private VisualElement _prepareRoot;
        private VisualElement _roomInputRoot;
        private VisualElement _lobbyRoot;
        private Label _status;
        private Label _roomId;
        private Label _mode;
        private Label _playerCount;
        private TextField _displayNameInput;
        private TextField _roomIdInput;
        private TextField _chatInput;
        private Image _modeImage;
        private Button _startGameButton;
        private Button _readyButton;
        private Button _unreadyButton;
        private Button _switchRoleButton;
        private readonly ToolkitListBinding<ToolkitTextRowData> _players = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly ToolkitListBinding<ToolkitTextRowData> _viewers = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly ToolkitListBinding<ToolkitTextRowData> _chats = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly ToolkitListBinding<ToolkitTextRowData> _modes = new ToolkitListBinding<ToolkitTextRowData>();

        public MiniGameStartToolkitBinding(
            string homeRootName,
            string namingRootName,
            string prepareRootName,
            string roomInputRootName,
            string lobbyRootName,
            string statusName,
            string roomIdName,
            string modeName,
            string playerCountName,
            string displayNameInputName,
            string roomIdInputName,
            string chatInputName,
            string modeImageName,
            string playersListName,
            string viewersListName,
            string chatListName,
            string modeListName,
            string playersEmptyName,
            string viewersEmptyName,
            string chatEmptyName,
            string modeEmptyName,
            string enterGameButtonName,
            string exitGameButtonName,
            string confirmNameButtonName,
            string namingBackButtonName,
            string createRoomButtonName,
            string joinRoomButtonName,
            string joinAsViewerButtonName,
            string prepareBackButtonName,
            string roomInputEnterButtonName,
            string roomInputBackButtonName,
            string startGameButtonName,
            string readyButtonName,
            string unreadyButtonName,
            string switchRoleButtonName,
            string leaveRoomButtonName,
            string sendChatButtonName,
            string modeNextButtonName,
            MiniGameToolkitModeOption[] modeOptions,
            int maxRows,
            string rowClass,
            string selectedClass,
            string disabledClass,
            Action exitGame,
            Action<string> displayNameSubmitted,
            Action<string> createRoom,
            Action<string, bool> joinRoom,
            Action<bool> readyChanged,
            Action startGame,
            Action leaveRoom,
            Action<bool> switchRole,
            Action<string> changeMode,
            Action<string> sendChat)
        {
            _homeRootName = homeRootName;
            _namingRootName = namingRootName;
            _prepareRootName = prepareRootName;
            _roomInputRootName = roomInputRootName;
            _lobbyRootName = lobbyRootName;
            _statusName = statusName;
            _roomIdName = roomIdName;
            _modeName = modeName;
            _playerCountName = playerCountName;
            _displayNameInputName = displayNameInputName;
            _roomIdInputName = roomIdInputName;
            _chatInputName = chatInputName;
            _modeImageName = modeImageName;
            _playersListName = playersListName;
            _viewersListName = viewersListName;
            _chatListName = chatListName;
            _modeListName = modeListName;
            _playersEmptyName = playersEmptyName;
            _viewersEmptyName = viewersEmptyName;
            _chatEmptyName = chatEmptyName;
            _modeEmptyName = modeEmptyName;
            _enterGameButtonName = enterGameButtonName;
            _exitGameButtonName = exitGameButtonName;
            _confirmNameButtonName = confirmNameButtonName;
            _namingBackButtonName = namingBackButtonName;
            _createRoomButtonName = createRoomButtonName;
            _joinRoomButtonName = joinRoomButtonName;
            _joinAsViewerButtonName = joinAsViewerButtonName;
            _prepareBackButtonName = prepareBackButtonName;
            _roomInputEnterButtonName = roomInputEnterButtonName;
            _roomInputBackButtonName = roomInputBackButtonName;
            _startGameButtonName = startGameButtonName;
            _readyButtonName = readyButtonName;
            _unreadyButtonName = unreadyButtonName;
            _switchRoleButtonName = switchRoleButtonName;
            _leaveRoomButtonName = leaveRoomButtonName;
            _sendChatButtonName = sendChatButtonName;
            _modeNextButtonName = modeNextButtonName;
            _modeOptions = modeOptions ?? Array.Empty<MiniGameToolkitModeOption>();
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-minigame-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
            _exitGame = exitGame;
            _displayNameSubmitted = displayNameSubmitted;
            _createRoom = createRoom;
            _joinRoom = joinRoom;
            _readyChanged = readyChanged;
            _startGame = startGame;
            _leaveRoom = leaveRoom;
            _switchRole = switchRole;
            _changeMode = changeMode;
            _sendChat = sendChat;
        }

        protected override void OnInitializeTyped()
        {
            _homeRoot = Query<VisualElement>(_homeRootName);
            _namingRoot = Query<VisualElement>(_namingRootName);
            _prepareRoot = Query<VisualElement>(_prepareRootName);
            _roomInputRoot = Query<VisualElement>(_roomInputRootName);
            _lobbyRoot = Query<VisualElement>(_lobbyRootName);
            _status = QLabel(_statusName);
            _roomId = QLabel(_roomIdName);
            _mode = QLabel(_modeName);
            _playerCount = QLabel(_playerCountName);
            _displayNameInput = Query<TextField>(_displayNameInputName);
            _roomIdInput = Query<TextField>(_roomIdInputName);
            _chatInput = Query<TextField>(_chatInputName);
            _modeImage = Query<Image>(_modeImageName);
            _startGameButton = QButton(_startGameButtonName);
            _readyButton = QButton(_readyButtonName);
            _unreadyButton = QButton(_unreadyButtonName);
            _switchRoleButton = QButton(_switchRoleButtonName);

            _players.Bind(Root, _playersListName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, null), _playersEmptyName);
            _viewers.Bind(Root, _viewersListName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, null), _viewersEmptyName);
            _chats.Bind(Root, _chatListName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, null), _chatEmptyName);
            _modes.Bind(Root, _modeListName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, HandleModeRowClicked), _modeEmptyName);

            Callbacks.RegisterValueChanged(_displayNameInput, value => ViewModel.DisplayNameInput = value);
            Callbacks.RegisterValueChanged(_roomIdInput, value => ViewModel.RoomIdInput = value);
            Callbacks.RegisterValueChanged(_chatInput, value => ViewModel.ChatInput = value);
            Callbacks.RegisterButton(Root, _enterGameButtonName, () => { ViewModel.ShowNaming(); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _exitGameButtonName, () => _exitGame?.Invoke());
            Callbacks.RegisterButton(Root, _confirmNameButtonName, HandleConfirmName);
            Callbacks.RegisterButton(Root, _namingBackButtonName, () => { ViewModel.ShowHome(); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _createRoomButtonName, () => _createRoom?.Invoke(SelectedModeId()));
            Callbacks.RegisterButton(Root, _joinRoomButtonName, () => { ViewModel.ShowRoomInput(false); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _joinAsViewerButtonName, () => { ViewModel.ShowRoomInput(true); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _prepareBackButtonName, () => { ViewModel.ShowNaming(); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _roomInputEnterButtonName, HandleJoinRoom);
            Callbacks.RegisterButton(Root, _roomInputBackButtonName, () => { ViewModel.ShowPrepare(); ApplyVisualState(ViewModel); });
            Callbacks.RegisterButton(Root, _startGameButtonName, () => _startGame?.Invoke());
            Callbacks.RegisterButton(Root, _readyButtonName, () => _readyChanged?.Invoke(true));
            Callbacks.RegisterButton(Root, _unreadyButtonName, () => _readyChanged?.Invoke(false));
            Callbacks.RegisterButton(Root, _switchRoleButtonName, HandleSwitchRole);
            Callbacks.RegisterButton(Root, _leaveRoomButtonName, () => _leaveRoom?.Invoke());
            Callbacks.RegisterButton(Root, _sendChatButtonName, HandleSendChat);
            Callbacks.RegisterButton(Root, _modeNextButtonName, HandleNextMode);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(MiniGameStartUIUpdate viewData, MiniGameStartPanelViewModel viewModel)
        {
            viewModel.Apply(viewData, _modeOptions, _maxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _players.Clear();
            _viewers.Clear();
            _chats.Clear();
            _modes.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _players.Dispose();
            _viewers.Dispose();
            _chats.Dispose();
            _modes.Dispose();
        }

        private void ApplyVisualState(MiniGameStartPanelViewModel vm)
        {
            if (vm == null)
                return;

            SetPageVisible(vm.Page);
            SetText(_status, vm.ToastText);
            var room = vm.Panel?.Room;
            SetText(_roomId, room != null ? $"房间号：{room.RoomId}" : "房间号：-");
            SetText(_mode, $"模式：{MiniGameToolkitText.Text(SelectedModeName(vm.SelectedModeId), vm.SelectedModeId)}");
            SetText(_playerCount, room != null ? $"玩家 {room.Players?.Length ?? 0} / 观战 {room.Viewers?.Length ?? 0}" : "玩家 0 / 观战 0");
            SetTextField(_displayNameInput, vm.DisplayNameInput);
            SetTextField(_roomIdInput, vm.RoomIdInput);
            SetTextField(_chatInput, vm.ChatInput);
            SetModeImage(vm.SelectedModeId);
            SetLobbyButtons(vm.Panel);

            _players.ReplaceAll(vm.PlayerRows);
            _viewers.ReplaceAll(vm.ViewerRows);
            _chats.ReplaceAll(vm.ChatRows);
            _modes.ReplaceAll(vm.ModeRows);
        }

        private void SetPageVisible(MiniGameStartPage page)
        {
            SetElementVisible(_homeRoot, page == MiniGameStartPage.Home);
            SetElementVisible(_namingRoot, page == MiniGameStartPage.Naming);
            SetElementVisible(_prepareRoot, page == MiniGameStartPage.Prepare);
            SetElementVisible(_roomInputRoot, page == MiniGameStartPage.RoomInput);
            SetElementVisible(_lobbyRoot, page == MiniGameStartPage.RoomLobby);
        }

        private void SetLobbyButtons(MiniGamePanelViewData panel)
        {
            var local = FindLocalPlayer(panel);
            var isHost = local != null && local.IsHost;
            var isViewer = panel != null && panel.IsLocalViewer;
            var isReady = local != null && local.IsReady;

            SetElementVisible(_startGameButton, isHost && !isViewer);
            SetElementVisible(_readyButton, !isHost && !isViewer && !isReady);
            SetElementVisible(_unreadyButton, !isHost && !isViewer && isReady);
            SetElementEnabled(_startGameButton, isHost && !isViewer);
            SetElementEnabled(_readyButton, !isHost && !isViewer);
            SetElementEnabled(_unreadyButton, !isHost && !isViewer);

            if (_switchRoleButton != null)
                _switchRoleButton.text = isViewer ? "转为玩家" : "转为观战";
        }

        private void HandleConfirmName()
        {
            var name = string.IsNullOrWhiteSpace(ViewModel.DisplayNameInput) ? "玩家" : ViewModel.DisplayNameInput.Trim();
            ViewModel.DisplayNameInput = name;
            _displayNameSubmitted?.Invoke(name);
            ViewModel.ShowPrepare();
            ApplyVisualState(ViewModel);
        }

        private void HandleJoinRoom()
        {
            var roomId = string.IsNullOrWhiteSpace(ViewModel.RoomIdInput) ? string.Empty : ViewModel.RoomIdInput.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(roomId))
            {
                SetText(_status, "请输入房间号");
                return;
            }

            _joinRoom?.Invoke(roomId, ViewModel.JoinAsViewer);
        }

        private void HandleSendChat()
        {
            var text = string.IsNullOrWhiteSpace(ViewModel.ChatInput) ? string.Empty : ViewModel.ChatInput.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _sendChat?.Invoke(text);
            ViewModel.ChatInput = string.Empty;
            SetTextField(_chatInput, string.Empty);
        }

        private void HandleSwitchRole()
        {
            var asViewer = !(ViewModel.Panel?.IsLocalViewer ?? false);
            _switchRole?.Invoke(asViewer);
        }

        private void HandleModeRowClicked(ToolkitTextRowData row)
        {
            if (row == null)
                return;

            ViewModel.SelectMode(row.Id);
            _changeMode?.Invoke(row.Id);
            ApplyVisualState(ViewModel);
        }

        private void HandleNextMode()
        {
            if (_modeOptions == null || _modeOptions.Length == 0)
                return;

            var current = SelectedModeId();
            var nextIndex = 0;
            for (var i = 0; i < _modeOptions.Length; i++)
            {
                if (string.Equals(_modeOptions[i]?.ModeId, current, StringComparison.Ordinal))
                {
                    nextIndex = (i + 1) % _modeOptions.Length;
                    break;
                }
            }

            var next = _modeOptions[nextIndex]?.ModeId;
            if (string.IsNullOrWhiteSpace(next))
                return;

            ViewModel.SelectMode(next);
            _changeMode?.Invoke(next);
            ApplyVisualState(ViewModel);
        }

        private string SelectedModeId()
        {
            return string.IsNullOrWhiteSpace(ViewModel.SelectedModeId) ? "draw_telephone" : ViewModel.SelectedModeId;
        }

        private string SelectedModeName(string modeId)
        {
            var option = FindMode(modeId);
            return option?.DisplayName;
        }

        private void SetModeImage(string modeId)
        {
            if (_modeImage == null)
                return;

            var option = FindMode(modeId);
            _modeImage.image = option?.DisplaySprite != null ? option.DisplaySprite.texture : null;
        }

        private MiniGameToolkitModeOption FindMode(string modeId)
        {
            if (_modeOptions == null)
                return null;

            for (var i = 0; i < _modeOptions.Length; i++)
            {
                var option = _modeOptions[i];
                if (option != null && string.Equals(option.ModeId, modeId, StringComparison.Ordinal))
                    return option;
            }

            return null;
        }

        private static MiniGamePlayerViewData FindLocalPlayer(MiniGamePanelViewData panel)
        {
            var players = panel?.Room?.Players ?? Array.Empty<MiniGamePlayerViewData>();
            for (var i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsLocalPlayer)
                    return players[i];
            }

            var viewers = panel?.Room?.Viewers ?? Array.Empty<MiniGamePlayerViewData>();
            for (var i = 0; i < viewers.Length; i++)
            {
                if (viewers[i] != null && viewers[i].IsLocalPlayer)
                    return viewers[i];
            }

            return null;
        }

        private static void SetTextField(TextField field, string value)
        {
            if (field != null && !string.Equals(field.value, value ?? string.Empty, StringComparison.Ordinal))
                field.SetValueWithoutNotify(value ?? string.Empty);
        }
    }
}
