using System;
using System.Text;
using NiumaMiniGame.Bridge;
using NiumaMiniGame.Controller;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NiumaMiniGame.UIBridge
{
    [Serializable]
    public sealed class MiniGameModeOption
    {
        [Tooltip("模式 ID，必须与前后端协议中的 modeId 保持一致。")]
        public string ModeId = "draw_telephone";

        [Tooltip("模式显示名称，用于开始场景和房间大厅 UI。")]
        public string DisplayName = "你画我猜";

        [Tooltip("该模式自己的展示图。每个 ModeOption 单独配置一张；ModeDisplayImage 只是场景里的 Image 显示容器。为空时只显示文字。")]
        public Sprite DisplaySprite;

        [Tooltip("最少玩家数量。你画我猜第一版至少需要 2 名玩家。")]
        public int MinPlayers = 2;

        [Tooltip("最大玩家数量。0 表示不限制。")]
        public int MaxPlayers = 8;

        [Tooltip("是否要求玩家数量为偶数。当前你画我猜玩法要求偶数人数。")]
        public bool RequireEvenPlayers = true;
    }

    internal enum MiniGameStartPage
    {
        Home,
        Naming,
        Prepare,
        RoomInput,
        Room
    }

    /// <summary>
    /// 你画我猜 2D 开始界面 UI。
    /// 负责把 MiniGamePanelViewData 显示到 Canvas，并把按钮点击转成 Controller 命令。
    /// </summary>
    public sealed class MiniGameStartScreenUI : MonoBehaviour, IMiniGameUIReceiver
    {
        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动查找场景中的 NiumaMiniGameController。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("NiumaScene 根控制器。退出小游戏返回 RPG 场景时使用。为空时会自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("为 true 时，启用组件会自动查找未绑定的 NiumaMiniGameController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindController = true;

        [Header("面板节点 - 新版分页流程")]
        [Tooltip("开始界面根节点。为空时不主动控制显示隐藏。")]
        [SerializeField] private GameObject startRoot;

        [Tooltip("房间大厅面板：进入房间后显示房间号、玩家、观战者、聊天、准备、开始游戏等内容。")]
        [SerializeField] private GameObject roomPanel;

        [Tooltip("入口页：显示“开始游戏”和“退出游戏”。正式新版 UI 必填；为空时仅作为旧版兼容，使用 entryPanel 流程。")]
        [SerializeField] private GameObject homePanel;

        [Tooltip("取名页：玩家点击开始游戏后输入昵称，并带返回按钮。为空时会直接进入预备页。")]
        [SerializeField] private GameObject namingPanel;

        [Tooltip("预备页：显示创建房间、加入房间、观战加入、返回。正式新版 UI 必填；为空时复用旧版 entryPanel。")]
        [SerializeField] private GameObject preparePanel;

        [Tooltip("房间号输入页：加入房间和观战加入共用，必须带进入和返回按钮。为空时点击加入按钮会沿用旧版直接加入逻辑。")]
        [SerializeField] private GameObject roomInputPanel;

        [Header("面板节点 - 房间身份控件")]
        [Tooltip("房主专用控件根节点：模式切换、开始游戏等。")]
        [SerializeField] private GameObject hostRoomControls;

        [Tooltip("普通玩家专用控件根节点：准备 / 取消准备等。")]
        [SerializeField] private GameObject guestRoomControls;

        [Tooltip("观战者专用控件根节点。第一版通常只显示退出和聊天。")]
        [SerializeField] private GameObject viewerRoomControls;

        [Header("输入框")]
        [Tooltip("玩家昵称输入框。创建或加入房间前会写入 MiniGameController。")]
        [SerializeField] private TMP_InputField displayNameInput;

        [Tooltip("房间 ID 输入框。加入房间或观战时使用。")]
        [SerializeField] private TMP_InputField roomIdInput;

        [Tooltip("聊天输入框。第一版只在已进入房间后发送普通房间聊天。")]
        [SerializeField] private TMP_InputField chatInput;

        [Header("按钮 - 入口页 HomePanel")]
        [Tooltip("入口页“开始游戏”按钮。点击后进入取名页；未绑定取名页时直接进入预备页。")]
        [SerializeField] private Button enterGameButton;

        [Tooltip("退出游戏按钮。点击后会离开房间并返回 RPG 场景；入口页、预备页、房间页都可以共用这个按钮。")]
        [SerializeField] private Button exitGameButton;

        [Header("按钮 - 取名页 NamingPanel")]
        [Tooltip("取名页确认按钮。点击后写入昵称并进入预备页。")]
        [SerializeField] private Button confirmNameButton;

        [Tooltip("取名页返回按钮。点击后回到入口页。")]
        [SerializeField] private Button namingBackButton;

        [Header("按钮 - 预备页 PreparePanel")]
        [Tooltip("创建房间按钮。点击后自动连接并创建房间。")]
        [SerializeField] private Button createRoomButton;

        [Tooltip("作为玩家加入房间按钮。点击后进入房间号输入页。")]
        [SerializeField] private Button joinRoomButton;

        [Tooltip("作为观战者加入房间按钮。点击后进入房间号输入页，并以观战身份加入。")]
        [SerializeField] private Button joinAsViewerButton;

        [Tooltip("预备页返回按钮。点击后回到入口页，不退出小游戏。")]
        [SerializeField] private Button prepareBackButton;

        [Header("按钮 - 房间号输入页 RoomInputPanel")]
        [Tooltip("房间号输入页进入按钮。根据当前入口区分玩家加入或观战加入。")]
        [SerializeField] private Button roomInputEnterButton;

        [Tooltip("房间号输入页返回按钮。点击后回到预备页，不退出小游戏。")]
        [SerializeField] private Button roomInputBackButton;

        [Header("按钮 - 房间大厅 RoomPanel")]
        [Tooltip("准备按钮。普通玩家点击后进入已准备状态；房主不使用该按钮。")]
        [SerializeField] private Button readyButton;

        [Tooltip("取消准备按钮。普通玩家点击后取消准备；房主不使用该按钮。")]
        [SerializeField] private Button unreadyButton;

        [Tooltip("放在 RoomPanel 的身份切换按钮。玩家点击后切为观战者；观战者点击后切为玩家。只在房间大厅 Lobby 状态可用。")]
        [SerializeField] private Button switchRoleButton;

        [Tooltip("房间页返回按钮。点击后离开当前房间并回到预备页，不返回 RPG。新版 UI 只绑定这个，不再绑定 LeaveRoomButton。")]
        [SerializeField] private Button roomBackButton;

        [Tooltip("房主开始游戏按钮。点击后发送 StartGameRequest，由后端统一校验人数和模式规则。")]
        [SerializeField] private Button hostStartGameButton;

        [Tooltip("模式选择按钮。房间外只修改待创建模式；房间内由房主发送 ChangeModeRequest。")]
        [SerializeField] private Button modeSelectButton;

        [Tooltip("发送聊天按钮。")]
        [SerializeField] private Button sendChatButton;

        [Header("可选调试按钮")]
        [Tooltip("可选：手动连接按钮。正常新版流程中，创建/加入房间会自动连接；只有需要单独测试连接状态时才绑定。")]
        [SerializeField] private Button connectButton;

        [Tooltip("可选：模式 ID 输入框。正式新版 UI 推荐用 ModeOptions + ModeSelectButton，不建议让策划在界面上手填 modeId。")]
        [SerializeField] private TMP_InputField modeIdInput;

        [Header("文本显示")]
        [Tooltip("建议放在 RoomPanel 或调试区的连接状态文本。拖 TMP_Text 组件即可，运行时自动写入“已连接/未连接”，策划不需要在这里填写文字内容。")]
        [SerializeField] private TMP_Text connectionText;

        [Tooltip("建议放在 RoomPanel 顶部信息区的房间摘要文本。拖 TMP_Text 组件，运行时自动写入房间号、模式、房主和本机身份。")]
        [SerializeField] private TMP_Text roomText;

        [Tooltip("放在 RoomPanel 的玩家列表区域。拖 TMP_Text 组件，运行时自动显示玩家昵称、房主、准备/未准备、状态、离线。")]
        [SerializeField] private TMP_Text playersText;

        [Tooltip("放在 RoomPanel 的观战者列表区域。拖 TMP_Text 组件，运行时自动显示观战者昵称、观战状态、离线；如果没有单独区域，可只用 NicknameListText。")]
        [SerializeField] private TMP_Text viewersText;

        [Tooltip("建议放在 StartScreen 顶层或当前页面底部的流程提示文本。拖 TMP_Text 组件，运行时自动写入当前可执行操作。")]
        [SerializeField] private TMP_Text hintText;

        [Tooltip("建议放在 StartScreen 顶层错误提示区域。拖 TMP_Text 组件，运行时自动显示服务器或 Mock 返回的错误。")]
        [SerializeField] private TMP_Text errorText;

        [Tooltip("放在 RoomPanel 的模式显示区域。拖 TMP_Text 组件，运行时自动显示当前模式的 DisplayName。")]
        [SerializeField] private TMP_Text modeDisplayText;

        [Tooltip("放在 RoomPanel 的房间号显示区域。拖 TMP_Text 组件，运行时自动显示系统分配或玩家加入的房间号。")]
        [SerializeField] private TMP_Text roomIdText;

        [Tooltip("放在 RoomPanel 的人数显示区域。拖 TMP_Text 组件，运行时自动显示当前玩家数量，不包含观战者。")]
        [SerializeField] private TMP_Text playerCountText;

        [Tooltip("放在 RoomPanel 的合并名单区域。拖 TMP_Text 组件，运行时自动显示玩家列表 + 观战者列表；如果 UI 已分开显示，可不绑定这里，只绑定 PlayersText 和 ViewersText。")]
        [SerializeField] private TMP_Text nicknameListText;

        [Tooltip("放在 RoomPanel 的聊天记录框。拖 TMP_Text 组件，运行时自动追加房间聊天内容。")]
        [SerializeField] private TMP_Text chatMessagesText;

        [Tooltip("建议放在 StartScreen 顶层短提示区域。拖 TMP_Text 组件，运行时显示人数不足等 2 秒提示；为空时复用 HintText。")]
        [SerializeField] private TMP_Text toastText;

        [Header("图片显示")]
        [Tooltip("放在 RoomPanel 的模式展示图片区。拖 Image 组件，不是在这里填固定图片；实际图片来自当前 ModeOption.DisplaySprite。")]
        [SerializeField] private Image modeDisplayImage;

        [Header("默认配置")]
        [Tooltip("默认模式 ID。当前你画我猜使用 draw_telephone。")]
        [SerializeField] private string defaultModeId = "draw_telephone";

        [Tooltip("可切换的模式列表。每个元素配置一个 ModeId、显示名、展示图和人数规则；点击模式选择按钮会切换并显示对应 DisplaySprite。")]
        [SerializeField] private MiniGameModeOption[] modeOptions;

        [Tooltip("本地短提示显示秒数。")]
        [SerializeField] private float toastSeconds = 2f;

        [Header("玩法场景跳转")]
        [Tooltip("真正你画我猜玩法场景名（开始/房间场景 → 游戏中 UI 场景；为空时房间进入 Playing 后不会自动切场景）。")]
        [SerializeField] private string gameplaySceneName;

        [Tooltip("返回上下文缺失时兜底加载的 RPG 场景名（直接从 Unity 打开 MiniGame 场景测试时填写；正式 RPG 入口通常依赖 ReturnContext）。")]
        [SerializeField] private string fallbackReturnSceneName;

        [Tooltip("房间状态离开 Lobby 后，是否自动切到真正玩法场景（开始场景和游戏中场景分离时开启；共用一个场景时关闭）。")]
        [SerializeField] private bool loadGameplaySceneWhenGameStarts;

        [Tooltip("进入玩法场景时是否压入返回上下文（MiniGame_Start→MiniGame_Gameplay 后需要回房间/开始页时开启；单场景玩法可关闭）。")]
        [SerializeField] private bool pushReturnContextWhenEnterGameplay = true;

        [Tooltip("进入玩法场景时是否冻结输入（切换到游戏中 UI 场景时建议开启）。")]
        [SerializeField] private bool freezeInputWhenEnterGameplay = true;

        [Tooltip("进入玩法场景时是否显示 Loading UI（跨场景加载建议开启；同场景 UI 切页可关闭）。")]
        [SerializeField] private bool showLoadingUIWhenEnterGameplay = true;

        [Tooltip("返回上一场景时是否冻结输入（退出 MiniGame 回 RPG 时建议开启）。")]
        [SerializeField] private bool freezeInputDuringReturn = true;

        [Tooltip("返回上一场景时是否请求显示 Loading UI（退出 MiniGame 回 RPG 时建议开启；第七阶段接入 LoadingPanel 后生效）。")]
        [SerializeField] private bool showLoadingUIOnReturn = true;

        [Tooltip("为 true 时，开始界面显示期间解锁并显示鼠标，避免从 3D 场景切入后鼠标仍被 TPC 锁定。")]
        [SerializeField] private bool unlockCursorWhileVisible = true;

        [Tooltip("为 true 时，缺少引用或 EventSystem 时输出警告。")]
        [SerializeField] private bool logWarnings = true;

        // 旧版兼容字段保留序列化，避免旧场景丢失引用；新版 UI 不再在 Inspector 暴露，减少策划误绑。
        [HideInInspector]
        [SerializeField] private GameObject entryPanel;

        [HideInInspector]
        [SerializeField] private Button leaveRoomButton;

        [HideInInspector]
        [SerializeField] private Button returnSceneButton;

        [HideInInspector]
        [SerializeField] private bool returnToPreviousSceneAfterLeaveRoom;

        private readonly StringBuilder _builder = new StringBuilder(512);
        private CursorLockMode _previousLockMode;
        private bool _previousCursorVisible;
        private bool _hasCursorSnapshot;
        private MiniGameStartPage _currentPage = MiniGameStartPage.Home;
        private MiniGamePanelViewData _lastPanel;
        private bool _pendingJoinAsViewer;
        private int _selectedModeIndex = -1;
        private float _toastHideTime = -1f;
        private bool _gameplaySceneLoadRequested;
        private long _lastAppliedToastTimeMs;
        private bool _warnedMissingRoomPanel;

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
            EnsureDefaultModeSelection();
            ApplyVisibleCursorState();
            WarnIfEventSystemMissing();
        }

        private void Update()
        {
            if (_toastHideTime > 0f && Time.unscaledTime >= _toastHideTime)
            {
                _toastHideTime = -1f;
                SetText(GetToastTarget(), string.Empty);
            }
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
            _lastPanel = panel;
            if (HasRoomSnapshot(panel))
            {
                _currentPage = MiniGameStartPage.Room;
            }
            else if (_currentPage == MiniGameStartPage.Room)
            {
                _currentPage = MiniGameStartPage.Prepare;
            }

            RefreshPanels(panel);
            RefreshTexts(panel);
            RefreshButtonStates(panel);
            ApplyServerToast(panel?.LastToast);
            TryLoadGameplayScene(panel);
        }

        public void ClickEnterGame()
        {
            _currentPage = namingPanel != null ? MiniGameStartPage.Naming : MiniGameStartPage.Prepare;
            RefreshFromLastPanel();
        }

        public void ClickConfirmName()
        {
            if (ResolveController(false))
            {
                WriteInputValuesToController();
                EnsureDefaultInputValues();
            }

            _currentPage = MiniGameStartPage.Prepare;
            RefreshFromLastPanel();
        }

        public void ClickBackToHome()
        {
            _currentPage = MiniGameStartPage.Home;
            RefreshFromLastPanel();
        }

        public void ClickBackToPrepare()
        {
            _currentPage = MiniGameStartPage.Prepare;
            RefreshFromLastPanel();
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
            OpenRoomInput(false);
        }

        public void ClickJoinAsViewer()
        {
            OpenRoomInput(true);
        }

        public void ClickConfirmRoomInput()
        {
            JoinRoom(_pendingJoinAsViewer);
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

        public void ClickSwitchRole()
        {
            if (!ResolveController(true))
            {
                return;
            }

            if (_lastPanel?.Room == null || _lastPanel.Room.State != MiniGameRoomState.Lobby)
            {
                ShowRoomTip("只有房间大厅状态可以切换玩家/观战身份。", false);
                return;
            }

            miniGameController.SwitchRole(!_lastPanel.IsLocalViewer);
        }

        public void ClickLeaveRoom()
        {
            if (ResolveController(true))
            {
                miniGameController.LeaveRoom();
            }

            if (returnToPreviousSceneAfterLeaveRoom)
            {
                ReturnToPreviousScene();
                return;
            }

            _currentPage = MiniGameStartPage.Prepare;
            RefreshFromLastPanel();
        }

        public void ClickReturnToPreviousScene()
        {
            if (ResolveController(false))
            {
                miniGameController.LeaveRoom();
            }

            ReturnToPreviousScene();
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

        public void ClickSelectMode()
        {
            SelectNextMode();
            RefreshModeDisplay(_lastPanel);

            if (_lastPanel != null && !string.IsNullOrWhiteSpace(_lastPanel.RoomId))
            {
                if (ResolveController(true))
                {
                    miniGameController.ChangeMode(ReadModeId());
                }
            }
        }

        public void ClickHostStartGame()
        {
            if (!ResolveController(true))
            {
                return;
            }

            if (!IsLocalHost(_lastPanel))
            {
                ShowRoomTip("只有房主可以开始游戏。", false);
                return;
            }

            if (!CanStartCurrentMode(_lastPanel, out var reason))
            {
                ShowRoomTip(string.IsNullOrWhiteSpace(reason)
                    ? "当前玩家数量没满足模式需求无法开始游戏"
                    : reason, false);
            }

            miniGameController.StartGame();
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

        private void OpenRoomInput(bool asViewer)
        {
            _pendingJoinAsViewer = asViewer;

            if (roomInputPanel == null)
            {
                JoinRoom(asViewer);
                return;
            }

            _currentPage = MiniGameStartPage.RoomInput;
            SetText(hintText, asViewer ? "输入房间号后，将以观战者身份加入。" : "输入房间号后，将以玩家身份加入。");
            RefreshFromLastPanel();
        }

        private void RefreshFromLastPanel()
        {
            RefreshPanels(_lastPanel);
            RefreshTexts(_lastPanel);
            RefreshButtonStates(_lastPanel);
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
            var selected = GetSelectedModeOption();
            if (selected != null && !string.IsNullOrWhiteSpace(selected.ModeId))
            {
                return selected.ModeId.Trim();
            }

            if (modeIdInput == null || string.IsNullOrWhiteSpace(modeIdInput.text))
            {
                return defaultModeId;
            }

            return modeIdInput.text.Trim();
        }

        private void RefreshPanels(MiniGamePanelViewData panel)
        {
            var hasRoom = HasRoomSnapshot(panel);
            var showRoomFallback = hasRoom && roomPanel == null;
            if (hasRoom)
            {
                _currentPage = MiniGameStartPage.Room;
            }
            else if (_currentPage == MiniGameStartPage.Room)
            {
                _currentPage = MiniGameStartPage.Prepare;
            }

            if (showRoomFallback && !_warnedMissingRoomPanel)
            {
                _warnedMissingRoomPanel = true;
                Warn("已进入房间，但 RoomPanel 未绑定。请把房间大厅根节点拖到 MiniGameStartScreenUI 的 RoomPanel 字段，否则无法显示房间页面。");
            }

            SetActive(startRoot, true);
            if (!UsesPagedFlow())
            {
                SetActive(entryPanel, !hasRoom || showRoomFallback);
                SetActive(roomPanel, hasRoom);
            }
            else
            {
                SetActive(homePanel, !hasRoom && _currentPage == MiniGameStartPage.Home);
                SetActive(namingPanel, !hasRoom && _currentPage == MiniGameStartPage.Naming);
                SetActive(preparePanel, (!hasRoom && _currentPage == MiniGameStartPage.Prepare) || (showRoomFallback && preparePanel != null));
                SetActive(roomInputPanel, !hasRoom && _currentPage == MiniGameStartPage.RoomInput);
                SetActive(entryPanel, (!hasRoom && preparePanel == null && _currentPage == MiniGameStartPage.Prepare) || (showRoomFallback && preparePanel == null));
                SetActive(roomPanel, hasRoom);
            }

            var isHost = IsLocalHost(panel);
            var isViewer = panel != null && panel.IsLocalViewer;
            SetActive(hostRoomControls, hasRoom && isHost && !isViewer);
            SetActive(guestRoomControls, hasRoom && !isHost && !isViewer);
            SetActive(viewerRoomControls, hasRoom && isViewer);
        }

        private void RefreshTexts(MiniGamePanelViewData panel)
        {
            RefreshModeDisplay(panel);

            if (panel == null)
            {
                SetText(connectionText, "未连接");
                SetText(roomText, "未进入房间");
                SetText(roomIdText, string.Empty);
                SetText(playerCountText, "当前人数：0");
                SetText(nicknameListText, string.Empty);
                SetText(chatMessagesText, string.Empty);
                SetText(playersText, string.Empty);
                SetText(viewersText, string.Empty);
                SetText(hintText, BuildPageHint());
                SetText(errorText, string.Empty);
                return;
            }

            SetText(connectionText, panel.IsConnected
                ? $"已连接  玩家ID：{ShortId(panel.LocalPlayerId)}"
                : "未连接");

            if (panel.Room == null)
            {
                var waitingForRoomSnapshot = IsWaitingForRoomSnapshot(panel);
                SetText(roomText, waitingForRoomSnapshot ? "房间已创建，正在同步房间大厅数据..." : "未进入房间");
                SetText(roomIdText, waitingForRoomSnapshot ? $"房间号：{panel.RoomId}" : string.Empty);
                SetText(playerCountText, waitingForRoomSnapshot ? "当前人数：同步中" : "当前人数：0");
                SetText(nicknameListText, string.Empty);
                SetText(chatMessagesText, BuildChatList(panel.Chats));
                SetText(playersText, string.Empty);
                SetText(viewersText, string.Empty);
                SetText(hintText, waitingForRoomSnapshot ? "服务器已分配房间号，正在等待 RoomSnapshot。若长时间停留，请检查后端是否广播 RoomSnapshot。" : BuildPageHint());
            }
            else
            {
                SetText(roomText, BuildRoomSummary(panel.Room, panel.IsLocalViewer));
                SetText(roomIdText, $"房间号：{panel.Room.RoomId}");
                SetText(playerCountText, $"当前人数：{CountPlayers(panel.Room.Players)}");
                SetText(nicknameListText, BuildRoomMemberList(panel.Room));
                SetText(playersText, BuildPlayerList("玩家", panel.Room.Players));
                SetText(viewersText, BuildPlayerList("观战", panel.Room.Viewers));
                SetText(chatMessagesText, BuildChatList(panel.Chats));
                SetText(hintText, BuildHint(panel));
            }

            SetText(errorText, BuildErrorText(panel.LastError));
        }

        private void RefreshButtonStates(MiniGamePanelViewData panel)
        {
            var connected = panel != null && panel.IsConnected;
            var hasRoom = HasRoomSnapshot(panel);
            var waitingForRoomSnapshot = IsWaitingForRoomSnapshot(panel);
            var inLobby = panel?.Room != null && panel.Room.State == MiniGameRoomState.Lobby;
            var isViewer = panel != null && panel.IsLocalViewer;
            var isHost = IsLocalHost(panel);

            SetInteractable(connectButton, !connected);
            SetInteractable(createRoomButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(joinRoomButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(joinAsViewerButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(roomInputEnterButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(readyButton, connected && hasRoom && inLobby && !isViewer && !isHost);
            SetInteractable(unreadyButton, connected && hasRoom && inLobby && !isViewer && !isHost);
            SetInteractable(switchRoleButton, connected && hasRoom && inLobby);
            SetInteractable(leaveRoomButton, connected && hasRoom);
            SetInteractable(roomBackButton, connected && hasRoom);
            SetInteractable(exitGameButton, true);
            SetInteractable(returnSceneButton, true);
            SetInteractable(sendChatButton, connected && hasRoom);
            SetInteractable(enterGameButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(confirmNameButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(namingBackButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(prepareBackButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(roomInputBackButton, !hasRoom && !waitingForRoomSnapshot);
            SetInteractable(hostStartGameButton, connected && hasRoom && inLobby && isHost && !isViewer);
            SetInteractable(modeSelectButton, !hasRoom || (hasRoom && isHost && inLobby));
        }

        private string BuildRoomSummary(MiniGameRoomViewData room, bool isLocalViewer)
        {
            return $"房间：{room.RoomId}\n模式：{room.ModeId}\n状态：{room.State}\n房主：{ShortId(room.HostPlayerId)}\n身份：{(isLocalViewer ? "观战者" : "玩家")}";
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

                AppendMemberLine(player);
            }

            return _builder.ToString();
        }

        private string BuildRoomMemberList(MiniGameRoomViewData room)
        {
            _builder.Clear();
            _builder.Append("房间成员");

            AppendMemberGroup("玩家", room?.Players);
            AppendMemberGroup("观战者", room?.Viewers);

            return _builder.ToString();
        }

        private void AppendMemberGroup(string title, MiniGamePlayerViewData[] members)
        {
            _builder.AppendLine();
            _builder.Append(title).Append("：");

            if (members == null || members.Length == 0)
            {
                _builder.Append("无");
                return;
            }

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null)
                {
                    continue;
                }

                AppendMemberLine(member);
            }
        }

        private void AppendMemberLine(MiniGamePlayerViewData member)
        {
            if (member == null)
            {
                return;
            }

            _builder.AppendLine();
            _builder.Append("- ");
            _builder.Append(SafeDisplayName(member.DisplayName, member.PlayerId));

            if (member.IsLocalPlayer)
            {
                _builder.Append("  我");
            }

            if (member.IsViewer)
            {
                _builder.Append("  观战者");
            }
            else
            {
                if (member.IsHost)
                {
                    _builder.Append("  房主");
                }

                _builder.Append(member.IsReady ? "  已准备" : "  未准备");
            }

            _builder.Append("  状态：");
            _builder.Append(GetMemberStateText(member));

            if (!member.IsConnected)
            {
                _builder.Append("  离线");
            }
        }

        private static string GetMemberStateText(MiniGamePlayerViewData member)
        {
            if (member == null)
            {
                return "未知";
            }

            if (!string.IsNullOrWhiteSpace(member.PlayerStateText))
            {
                return member.PlayerStateText;
            }

            if (member.IsViewer)
            {
                return "观战中";
            }

            return member.IsReady ? "已准备" : "等待中";
        }

        private string BuildChatList(MiniGameChatViewData[] chats)
        {
            _builder.Clear();
            if (chats == null || chats.Length == 0)
            {
                return string.Empty;
            }

            var start = Mathf.Max(0, chats.Length - 12);
            for (var i = start; i < chats.Length; i++)
            {
                var chat = chats[i];
                if (chat == null || string.IsNullOrWhiteSpace(chat.Text))
                {
                    continue;
                }

                if (_builder.Length > 0)
                {
                    _builder.AppendLine();
                }

                _builder.Append(chat.IsLocalPlayer ? "我" : SafeDisplayName(chat.DisplayName, chat.PlayerId));
                _builder.Append("：");
                _builder.Append(chat.Text);
            }

            return _builder.ToString();
        }

        private string BuildPageHint()
        {
            switch (_currentPage)
            {
                case MiniGameStartPage.Home:
                    return "选择开始游戏进入取名流程，或退出返回外部游戏。";
                case MiniGameStartPage.Naming:
                    return "输入昵称后进入预备界面。";
                case MiniGameStartPage.Prepare:
                    return "可以创建房间、输入房间号加入，或作为观战者加入。";
                case MiniGameStartPage.RoomInput:
                    return _pendingJoinAsViewer ? "输入房间号后以观战者身份加入。" : "输入房间号后以玩家身份加入。";
                default:
                    return "等待房间状态同步。";
            }
        }

        private void RefreshModeDisplay(MiniGamePanelViewData panel)
        {
            var modeId = panel?.Room != null && !string.IsNullOrWhiteSpace(panel.Room.ModeId)
                ? panel.Room.ModeId
                : ReadModeId();
            var mode = FindModeOption(modeId) ?? GetSelectedModeOption();
            var displayName = mode != null && !string.IsNullOrWhiteSpace(mode.DisplayName)
                ? mode.DisplayName
                : modeId;

            SetText(modeDisplayText, $"模式：{displayName}");
            if (modeDisplayImage != null)
            {
                modeDisplayImage.sprite = mode?.DisplaySprite;
                modeDisplayImage.enabled = mode?.DisplaySprite != null;
            }
        }

        private bool CanStartCurrentMode(MiniGamePanelViewData panel, out string reason)
        {
            reason = null;
            if (panel?.Room == null)
            {
                reason = "当前不在房间中，无法开始游戏。";
                return false;
            }

            var mode = FindModeOption(panel.Room.ModeId) ?? GetSelectedModeOption();
            var count = CountPlayers(panel.Room.Players);
            var minPlayers = Mathf.Max(1, mode?.MinPlayers ?? 2);
            var maxPlayers = Mathf.Max(0, mode?.MaxPlayers ?? 0);
            var requireEven = mode == null || mode.RequireEvenPlayers;

            if (count < minPlayers)
            {
                reason = "当前玩家数量没满足模式需求无法开始游戏";
                return false;
            }

            if (maxPlayers > 0 && count > maxPlayers)
            {
                reason = $"当前玩家数量超过模式上限（{maxPlayers}人），无法开始游戏。";
                return false;
            }

            if (requireEven && count % 2 != 0)
            {
                reason = "当前模式需要偶数玩家，无法开始游戏。";
                return false;
            }

            return true;
        }

        private bool IsLocalHost(MiniGamePanelViewData panel)
        {
            return !string.IsNullOrWhiteSpace(panel?.Room?.HostPlayerId)
                   && !string.IsNullOrWhiteSpace(panel.LocalPlayerId)
                   && string.Equals(panel.Room.HostPlayerId, panel.LocalPlayerId, StringComparison.Ordinal);
        }

        private void ShowRoomTip(string message, bool syncToChat)
        {
            SetText(GetToastTarget(), message);
            _toastHideTime = Time.unscaledTime + Mathf.Max(0.1f, toastSeconds);

            if (syncToChat && ResolveController(false) && miniGameController.IsConnected && !string.IsNullOrWhiteSpace(_lastPanel?.RoomId))
            {
                miniGameController.SendChat($"[系统提示] {message}");
            }
        }

        private void ApplyServerToast(MiniGameToastViewData toast)
        {
            if (toast == null || string.IsNullOrWhiteSpace(toast.Text))
            {
                return;
            }

            if (toast.ServerTimeMs > 0L && toast.ServerTimeMs == _lastAppliedToastTimeMs)
            {
                return;
            }

            _lastAppliedToastTimeMs = toast.ServerTimeMs;
            ShowRoomTip(toast.Text, false);
        }

        private TMP_Text GetToastTarget()
        {
            return toastText != null ? toastText : hintText;
        }

        private static bool HasRoomSnapshot(MiniGamePanelViewData panel)
        {
            return panel?.Room != null && !string.IsNullOrWhiteSpace(panel.Room.RoomId);
        }

        private static bool IsWaitingForRoomSnapshot(MiniGamePanelViewData panel)
        {
            return panel?.Room == null && !string.IsNullOrWhiteSpace(panel?.RoomId);
        }

        private bool UsesPagedFlow()
        {
            return homePanel != null
                   || namingPanel != null
                   || preparePanel != null
                   || roomInputPanel != null;
        }

        private int CountPlayers(MiniGamePlayerViewData[] players)
        {
            var count = 0;
            if (players == null)
            {
                return count;
            }

            for (var i = 0; i < players.Length; i++)
            {
                if (players[i] != null && !players[i].IsViewer)
                {
                    count++;
                }
            }

            return count;
        }

        private MiniGameModeOption GetSelectedModeOption()
        {
            EnsureDefaultModeSelection();
            return IsModeIndexValid(_selectedModeIndex) ? modeOptions[_selectedModeIndex] : null;
        }

        private MiniGameModeOption FindModeOption(string modeId)
        {
            if (modeOptions == null || string.IsNullOrWhiteSpace(modeId))
            {
                return null;
            }

            for (var i = 0; i < modeOptions.Length; i++)
            {
                var option = modeOptions[i];
                if (option != null && string.Equals(option.ModeId, modeId, StringComparison.Ordinal))
                {
                    return option;
                }
            }

            return null;
        }

        private void SelectNextMode()
        {
            if (modeOptions == null || modeOptions.Length == 0)
            {
                return;
            }

            var start = _selectedModeIndex;
            for (var offset = 1; offset <= modeOptions.Length; offset++)
            {
                var index = start < 0 ? 0 : (start + offset) % modeOptions.Length;
                if (IsModeIndexValid(index))
                {
                    _selectedModeIndex = index;
                    if (modeIdInput != null)
                    {
                        modeIdInput.text = modeOptions[index].ModeId;
                    }
                    return;
                }
            }
        }

        private void EnsureDefaultModeSelection()
        {
            if (IsModeIndexValid(_selectedModeIndex))
            {
                return;
            }

            _selectedModeIndex = -1;
            if (modeOptions == null)
            {
                return;
            }

            var targetModeId = modeIdInput != null && !string.IsNullOrWhiteSpace(modeIdInput.text)
                ? modeIdInput.text.Trim()
                : defaultModeId;
            for (var i = 0; i < modeOptions.Length; i++)
            {
                var option = modeOptions[i];
                if (option != null && string.Equals(option.ModeId, targetModeId, StringComparison.Ordinal))
                {
                    _selectedModeIndex = i;
                    return;
                }
            }

            for (var i = 0; i < modeOptions.Length; i++)
            {
                if (IsModeIndexValid(i))
                {
                    _selectedModeIndex = i;
                    return;
                }
            }
        }

        private bool IsModeIndexValid(int index)
        {
            return modeOptions != null
                   && index >= 0
                   && index < modeOptions.Length
                   && modeOptions[index] != null
                   && !string.IsNullOrWhiteSpace(modeOptions[index].ModeId);
        }

        private static string SafeDisplayName(string displayName, string playerId)
        {
            return string.IsNullOrWhiteSpace(displayName) ? ShortId(playerId) : displayName;
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
            BindButton(switchRoleButton, ClickSwitchRole);
            BindButton(leaveRoomButton, ClickLeaveRoom);
            BindButton(roomBackButton, ClickLeaveRoom);
            BindButton(exitGameButton, ClickReturnToPreviousScene);
            BindButton(returnSceneButton, ClickReturnToPreviousScene);
            BindButton(sendChatButton, ClickSendChat);
            BindButton(enterGameButton, ClickEnterGame);
            BindButton(confirmNameButton, ClickConfirmName);
            BindButton(namingBackButton, ClickBackToHome);
            BindButton(prepareBackButton, ClickBackToHome);
            BindButton(roomInputEnterButton, ClickConfirmRoomInput);
            BindButton(roomInputBackButton, ClickBackToPrepare);
            BindButton(hostStartGameButton, ClickHostStartGame);
            BindButton(modeSelectButton, ClickSelectMode);
        }

        private void UnbindButtons()
        {
            UnbindButton(connectButton, ClickConnect);
            UnbindButton(createRoomButton, ClickCreateRoom);
            UnbindButton(joinRoomButton, ClickJoinRoom);
            UnbindButton(joinAsViewerButton, ClickJoinAsViewer);
            UnbindButton(readyButton, ClickReady);
            UnbindButton(unreadyButton, ClickUnready);
            UnbindButton(switchRoleButton, ClickSwitchRole);
            UnbindButton(leaveRoomButton, ClickLeaveRoom);
            UnbindButton(roomBackButton, ClickLeaveRoom);
            UnbindButton(exitGameButton, ClickReturnToPreviousScene);
            UnbindButton(returnSceneButton, ClickReturnToPreviousScene);
            UnbindButton(sendChatButton, ClickSendChat);
            UnbindButton(enterGameButton, ClickEnterGame);
            UnbindButton(confirmNameButton, ClickConfirmName);
            UnbindButton(namingBackButton, ClickBackToHome);
            UnbindButton(prepareBackButton, ClickBackToHome);
            UnbindButton(roomInputEnterButton, ClickConfirmRoomInput);
            UnbindButton(roomInputBackButton, ClickBackToPrepare);
            UnbindButton(hostStartGameButton, ClickHostStartGame);
            UnbindButton(modeSelectButton, ClickSelectMode);
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

            EnsureDefaultModeSelection();
        }

        private bool ResolveController(bool warn)
        {
            if (miniGameController != null)
            {
                ResolveSceneController(false);
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

            ResolveSceneController(false);
            return miniGameController != null;
        }

        private bool ResolveSceneController(bool warn)
        {
            if (sceneController != null)
            {
                return true;
            }

            if (autoFindController)
            {
#if UNITY_2023_1_OR_NEWER
                sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
                sceneController = FindObjectOfType<NiumaSceneController>();
#endif
            }

            if (sceneController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameStartScreenUI] 未找到 NiumaSceneController，无法返回上一场景。", this);
            }

            return sceneController != null;
        }

        private void TryLoadGameplayScene(MiniGamePanelViewData panel)
        {
            if (panel?.Room == null || panel.Room.State == MiniGameRoomState.Lobby || panel.Room.State == MiniGameRoomState.Closed)
            {
                _gameplaySceneLoadRequested = false;
                return;
            }

            if (!loadGameplaySceneWhenGameStarts
                || _gameplaySceneLoadRequested
                || string.IsNullOrWhiteSpace(gameplaySceneName)
                || !ResolveSceneController(true))
            {
                return;
            }

            var handle = sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = SceneLoadPurpose.MiniGame,
                Target = new SceneTransitionTarget(gameplaySceneName),
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = pushReturnContextWhenEnterGameplay
                },
                Options = new SceneTransitionOptions
                {
                    FreezeInputDuringLoad = freezeInputWhenEnterGameplay,
                    ShowLoadingUI = showLoadingUIWhenEnterGameplay,
                    ReplacePendingRequest = true
                }
            });

            _gameplaySceneLoadRequested = true;
            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded) && logWarnings)
            {
                _gameplaySceneLoadRequested = false;
                Debug.LogWarning($"[MiniGameStartScreenUI] 进入玩法场景失败：{handle.Result?.ErrorCode} {handle.Result?.ErrorMessage}", this);
            }
        }

        private void ReturnToPreviousScene()
        {
            if (!ResolveSceneController(true))
            {
                return;
            }

            var handle = sceneController.ReturnToPreviousScene(new SceneTransitionOptions
            {
                FreezeInputDuringLoad = freezeInputDuringReturn,
                ShowLoadingUI = showLoadingUIOnReturn,
                ReplacePendingRequest = true
            });

            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded) && logWarnings)
            {
                Debug.LogWarning($"[MiniGameStartScreenUI] 返回上一场景失败：{handle.Result?.ErrorCode} {handle.Result?.ErrorMessage}", this);
            }

            if (handle.IsDone &&
                handle.Result != null &&
                !handle.Result.Succeeded &&
                handle.Result.ErrorCode == SceneLoadErrorCode.ReturnContextMissing)
            {
                LoadFallbackReturnScene();
            }
        }

        private void LoadFallbackReturnScene()
        {
            if (string.IsNullOrWhiteSpace(fallbackReturnSceneName))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[MiniGameStartScreenUI] 没有可返回的场景上下文，也未配置 fallbackReturnSceneName。请从 RPG 入口进入 MiniGame，或在开始界面配置兜底 RPG 场景名。", this);
                }

                return;
            }

            var fallbackHandle = sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = SceneLoadPurpose.MiniGame,
                Target = new SceneTransitionTarget(fallbackReturnSceneName.Trim()),
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = false
                },
                Options = new SceneTransitionOptions
                {
                    FreezeInputDuringLoad = freezeInputDuringReturn,
                    ShowLoadingUI = showLoadingUIOnReturn,
                    ReplacePendingRequest = true
                }
            });

            if (fallbackHandle.IsDone &&
                (fallbackHandle.Result == null || !fallbackHandle.Result.Succeeded) &&
                logWarnings)
            {
                Debug.LogWarning($"[MiniGameStartScreenUI] 兜底返回场景失败：{fallbackHandle.Result?.ErrorCode} {fallbackHandle.Result?.ErrorMessage}", this);
            }
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
