using System;
using System.Collections.Generic;
using System.Text;
using NiumaMiniGame.Bridge;
using NiumaMiniGame.Controller;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using NiumaScene.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NiumaMiniGame.UIBridge
{
    [Serializable]
    public sealed class MiniGameColorButtonBinding
    {
        [Tooltip("颜色按钮。为空时不会自动绑定点击事件。")]
        public Button Button;

        [Tooltip("点击按钮后设置的笔刷颜色。")]
        public Color Color = Color.black;

        [Tooltip("可选色块图片。启用时会自动同步成 Color。")]
        public Image Swatch;
    }

    /// <summary>
    /// 你画我猜游戏中界面 Receiver。
    /// 负责把 MiniGameGameplayViewData 显示到 UI，并把按钮输入转发给 MiniGameController。
    /// </summary>
    public sealed class MiniGameGameplayScreenUI : MonoBehaviour, IMiniGameUIReceiver
    {
        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动在场景中查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("场景控制器。点击退出游戏时可用它返回上一个场景。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("游戏中绘画画布。负责本地绘制与点位发送。")]
        [SerializeField] private MiniGameDrawingCanvas drawingCanvas;

        [Tooltip("未手动绑定控制器时，是否自动查找场景中的 NiumaMiniGameController。")]
        [SerializeField] private bool autoFindController = true;

        [Tooltip("未手动绑定场景控制器时，是否自动查找场景中的 NiumaSceneController。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Header("模块根节点")]
        [Tooltip("整个游戏中 UI 根节点。")]
        [SerializeField] private GameObject gameplayRoot;

        [Tooltip("画板模块根节点。")]
        [SerializeField] private GameObject drawingBoardRoot;

        [Tooltip("笔刷工具组根节点。")]
        [SerializeField] private GameObject brushToolsRoot;

        [Tooltip("色彩组根节点。")]
        [SerializeField] private GameObject colorPaletteRoot;

        [Tooltip("画布根节点。用于显示/隐藏画布区域。")]
        [SerializeField] private GameObject canvasRoot;

        [Tooltip("作画人模块根节点。")]
        [SerializeField] private GameObject drawerNameRoot;

        [Tooltip("聊天框模块根节点。")]
        [SerializeField] private GameObject chatRoot;

        [Tooltip("回答模块根节点。")]
        [SerializeField] private GameObject answerRoot;

        [Tooltip("菜单模块根节点。")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("题目模块根节点。")]
        [SerializeField] private GameObject topicRoot;

        [Tooltip("游戏时间显示模块根节点。")]
        [SerializeField] private GameObject timerRoot;

        [Tooltip("玩家列表模块根节点。")]
        [SerializeField] private GameObject playerListRoot;

        [Tooltip("开始作画提示模块根节点。该提示默认只显示几秒钟。")]
        [SerializeField] private GameObject drawPromptRoot;

        [Tooltip("开始回答提示模块根节点。该提示默认只显示几秒钟。")]
        [SerializeField] private GameObject answerPromptRoot;

        [Tooltip("评价模块根节点。")]
        [SerializeField] private GameObject evaluationRoot;

        [Tooltip("评价列表根节点。")]
        [SerializeField] private GameObject evaluationListRoot;

        [Tooltip("可选的菜单弹层根节点。继续游戏按钮会隐藏它。")]
        [SerializeField] private GameObject menuPopupRoot;

        [Header("文本")]
        [Tooltip("当前作画人昵称文本。")]
        [SerializeField] private TMP_Text drawerNameText;

        [Tooltip("题目文本。作画阶段显示当前词条或上一位玩家猜测词。")]
        [SerializeField] private TMP_Text topicText;

        [Tooltip("回答文本。结算阶段显示最终猜测词。")]
        [SerializeField] private TMP_Text answerText;

        [Tooltip("作答者昵称文本。")]
        [SerializeField] private TMP_Text answererText;

        [Tooltip("倒计时文本。")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("玩家列表文本。第一版用多行文本承载昵称和状态，后续可替换为列表项预制体。")]
        [SerializeField] private TMP_Text playerListText;

        [Tooltip("聊天记录文本。第一版用多行文本承载聊天，后续可替换为滚动列表。")]
        [SerializeField] private TMP_Text chatLogText;

        [Tooltip("作画提示文本。")]
        [SerializeField] private TMP_Text drawPromptText;

        [Tooltip("回答提示文本。")]
        [SerializeField] private TMP_Text answerPromptText;

        [Tooltip("评价列表文本。")]
        [SerializeField] private TMP_Text evaluationListText;

        [Tooltip("短提示文本。用于显示本地按钮错误或服务器 Toast。")]
        [SerializeField] private TMP_Text toastText;

        [Header("输入框")]
        [Tooltip("聊天输入框。为空时发送按钮无效。")]
        [SerializeField] private TMP_InputField chatInput;

        [Tooltip("答案输入框。为空提交时由后端替换为默认未作答文本。")]
        [SerializeField] private TMP_InputField answerInput;

        [Header("按钮")]
        [Tooltip("铅笔按钮。")]
        [SerializeField] private Button pencilButton;

        [Tooltip("钢笔/直线按钮。")]
        [SerializeField] private Button lineButton;

        [Tooltip("橡皮擦按钮。")]
        [SerializeField] private Button eraserButton;

        [Tooltip("撤销按钮。")]
        [SerializeField] private Button undoButton;

        [Tooltip("还原按钮。")]
        [SerializeField] private Button redoButton;

        [Tooltip("清空画布按钮。")]
        [SerializeField] private Button clearButton;

        [Tooltip("完成作画按钮。")]
        [SerializeField] private Button finishDrawingButton;

        [Tooltip("发送聊天按钮。")]
        [SerializeField] private Button sendChatButton;

        [Tooltip("确认答案按钮。")]
        [SerializeField] private Button submitAnswerButton;

        [Tooltip("继续游戏/返回按钮。第一版用于关闭菜单弹层。")]
        [SerializeField] private Button continueGameButton;

        [Tooltip("退出游戏按钮。会离开房间并可选返回上一个场景。")]
        [SerializeField] private Button exitGameButton;

        [Tooltip("赞同按钮。")]
        [SerializeField] private Button agreeButton;

        [Tooltip("不赞同按钮。")]
        [SerializeField] private Button disagreeButton;

        [Header("笔刷控件")]
        [Tooltip("笔刷粗细滑动条。建议竖向 Slider，范围 0-1。")]
        [SerializeField] private Slider brushSizeSlider;

        [Tooltip("颜色按钮绑定。可配置黑、红、黄、蓝、绿、紫和自定义颜色。")]
        [SerializeField] private MiniGameColorButtonBinding[] colorButtons;

        [Header("音频提示")]
        [Tooltip("可选 AudioSource。未接入 NiumaAudio 前，用它播放提示音。")]
        [SerializeField] private AudioSource promptAudioSource;

        [Tooltip("开始作画提示音。")]
        [SerializeField] private AudioClip drawPromptClip;

        [Tooltip("开始回答提示音。")]
        [SerializeField] private AudioClip answerPromptClip;

        [Tooltip("最后 5 秒倒计时提示音。")]
        [SerializeField] private AudioClip countdownFiveSecondsClip;

        [Header("显示策略")]
        [Tooltip("开始作画/开始回答提示显示秒数。")]
        [SerializeField] private float rolePromptSeconds = 3f;

        [Tooltip("聊天记录最多显示多少行。")]
        [SerializeField] private int maxChatLines = 20;

        [Tooltip("退出游戏时是否调用 NiumaScene.ReturnToPreviousScene。")]
        [SerializeField] private bool returnToPreviousSceneOnExit = true;

        [Tooltip("是否输出 UI 警告日志。")]
        [SerializeField] private bool logWarnings = true;

        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly List<UnityEngine.Events.UnityAction> _colorButtonActions = new List<UnityEngine.Events.UnityAction>(8);
        private MiniGamePanelViewData _lastPanel;
        private MiniGameGameplayViewData _lastGameplay;
        private float _timerBaseSeconds;
        private float _timerBaseRealtime;
        private float _drawPromptHideTime;
        private float _answerPromptHideTime;
        private MiniGameGameplayPhase _lastPhase = MiniGameGameplayPhase.None;
        private MiniGamePlayerState _lastLocalState = MiniGamePlayerState.None;
        private bool _countdownPlayedForPhase;

        private void OnEnable()
        {
            BindButtons();
            if (brushSizeSlider != null)
            {
                drawingCanvas?.SetBrushSize01(brushSizeSlider.value);
            }
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void Update()
        {
            UpdateTimerText();
            RefreshPromptRoots();
            TryPlayCountdownAudio();
        }

        public void ApplyMiniGameUpdate(MiniGameUIUpdate update)
        {
            if (update.UpdateType == MiniGameUIUpdateType.Cleared || update.PanelData == null)
            {
                ClearView();
                return;
            }

            _lastPanel = update.PanelData;
            _lastGameplay = update.PanelData.Gameplay;
            RefreshGameplay(update.PanelData);
        }

        public void ClickPencil() => drawingCanvas?.SelectPencilTool();
        public void ClickLine() => drawingCanvas?.SelectLineTool();
        public void ClickEraser() => drawingCanvas?.SelectEraserTool();
        public void ClickUndo() => drawingCanvas?.Undo();
        public void ClickRedo() => drawingCanvas?.Redo();
        public void ClickClearCanvas() => drawingCanvas?.Clear();

        public void ClickFinishDrawing()
        {
            if (!EnsureController(true))
            {
                return;
            }

            var strokeGroupId = drawingCanvas != null ? drawingCanvas.CurrentStrokeGroupId : CreateFallbackStrokeGroupId();
            var task = _lastPanel?.CurrentTask;
            var submitted = task != null && !string.IsNullOrWhiteSpace(task.ChainId)
                ? miniGameController.SubmitDrawing(task.ChainId, strokeGroupId)
                : miniGameController.SubmitSequentialDrawing(strokeGroupId);

            if (!submitted)
            {
                ShowToast("提交作画失败，请确认当前是否处于作画阶段。");
            }
        }

        public void ClickSubmitAnswer()
        {
            if (!EnsureController(true))
            {
                return;
            }

            var text = answerInput != null ? answerInput.text : string.Empty;
            var task = _lastPanel?.CurrentTask;
            var submitted = task != null && !string.IsNullOrWhiteSpace(task.ChainId)
                ? miniGameController.SubmitGuess(task.ChainId, text)
                : miniGameController.SubmitSequentialAnswer(text);

            if (submitted)
            {
                if (answerInput != null)
                {
                    answerInput.text = string.Empty;
                }
            }
            else
            {
                ShowToast("提交答案失败，请确认当前是否处于回答阶段。");
            }
        }

        public void ClickSendChat()
        {
            if (!EnsureController(true) || chatInput == null)
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

        public void ClickAgree() => SubmitEvaluation(true);
        public void ClickDisagree() => SubmitEvaluation(false);

        public void ClickContinueGame()
        {
            SetActive(menuPopupRoot, false);
        }

        public void ClickExitGame()
        {
            if (EnsureController(false))
            {
                miniGameController.LeaveRoom();
                miniGameController.Disconnect("GameplayExit");
            }

            if (returnToPreviousSceneOnExit && EnsureSceneController(false))
            {
                sceneController.ReturnToPreviousScene();
            }
        }

        public void SetBrushColor(Color color)
        {
            drawingCanvas?.SetBrushColor(color);
        }

        public void SendFlowerToCanvas() => SendGiftToModule("flower", "canvas", 0.5f, 0.5f);
        public void SendEggToCanvas() => SendGiftToModule("egg", "canvas", 0.5f, 0.5f);
        public void SendFlowerToAnswer() => SendGiftToModule("flower", "answer", 0.5f, 0.5f);
        public void SendEggToAnswer() => SendGiftToModule("egg", "answer", 0.5f, 0.5f);

        public void SendGiftToModule(string giftType, string targetModule, float normalizedX, float normalizedY)
        {
            if (!EnsureController(true))
            {
                return;
            }

            miniGameController.SendGift(giftType, null, targetModule, normalizedX, normalizedY);
        }

        private void RefreshGameplay(MiniGamePanelViewData panel)
        {
            var gameplay = panel.Gameplay;
            SetActive(gameplayRoot, gameplay != null);
            if (gameplay == null)
            {
                return;
            }

            UpdatePromptTimers(gameplay);
            RefreshAccess(gameplay.Access);
            RefreshTexts(panel, gameplay);
            RefreshInputs(gameplay.Access);
            RefreshCanvas(panel, gameplay);
            ResetTimer(gameplay.RemainingSeconds);
        }

        private void RefreshAccess(MiniGameGameplayAccessViewData access)
        {
            access = access ?? new MiniGameGameplayAccessViewData();

            ApplyAccess(drawingBoardRoot, access.DrawingBoard);
            ApplyAccess(brushToolsRoot, access.BrushTools, pencilButton, lineButton, eraserButton, undoButton, redoButton, clearButton, brushSizeSlider);
            ApplyAccess(colorPaletteRoot, access.ColorPalette);
            ApplyAccess(canvasRoot, access.Canvas);
            ApplyAccess(drawerNameRoot, access.DrawerName);
            ApplyAccess(chatRoot, access.Chat, chatInput, sendChatButton);
            ApplyAccess(answerRoot, access.Answer, answerInput, submitAnswerButton);
            ApplyAccess(menuRoot, access.Menu, continueGameButton, exitGameButton);
            ApplyAccess(topicRoot, access.Topic);
            ApplyAccess(timerRoot, access.Timer);
            ApplyAccess(playerListRoot, access.PlayerList);
            ApplyAccess(evaluationRoot, access.Evaluation, agreeButton, disagreeButton);
            ApplyAccess(evaluationListRoot, access.EvaluationList);

            if (drawingCanvas != null)
            {
                drawingCanvas.SetCanvasInteractable(access.Canvas == MiniGameUIAccessState.Open);
            }

            RefreshPromptRoots();
        }

        private void RefreshTexts(MiniGamePanelViewData panel, MiniGameGameplayViewData gameplay)
        {
            SetText(drawerNameText, string.IsNullOrWhiteSpace(gameplay.CurrentDrawerDisplayName)
                ? "作画人：--"
                : $"作画人：{gameplay.CurrentDrawerDisplayName}");

            SetText(topicText, BuildTopicText(gameplay));
            SetText(answerText, BuildAnswerText(gameplay));
            SetText(answererText, BuildAnswererText(gameplay));
            SetText(drawPromptText, "轮到你作画了");
            SetText(answerPromptText, "轮到你回答了");
            SetText(playerListText, BuildPlayerListText(gameplay.Players));
            SetText(chatLogText, BuildChatText(gameplay.Chats));
            SetText(evaluationListText, BuildEvaluationText(gameplay.Evaluations));

            if (panel.LastToast != null && !string.IsNullOrWhiteSpace(panel.LastToast.Text))
            {
                ShowToast(panel.LastToast.Text);
            }
            else if (panel.LastError != null && !string.IsNullOrWhiteSpace(panel.LastError.DebugMessage))
            {
                ShowToast(panel.LastError.DebugMessage);
            }
        }

        private void RefreshInputs(MiniGameGameplayAccessViewData access)
        {
            var answerOpen = access != null && access.Answer == MiniGameUIAccessState.Open;
            var evaluationOpen = access != null && access.Evaluation == MiniGameUIAccessState.Display;

            SetInteractable(answerInput, answerOpen);
            SetInteractable(submitAnswerButton, answerOpen);
            SetInteractable(agreeButton, access != null && access.AgreeButton == MiniGameUIAccessState.Open);
            SetInteractable(disagreeButton, access != null && access.DisagreeButton == MiniGameUIAccessState.Open);

            if (!answerOpen && answerInput != null)
            {
                answerInput.DeactivateInputField();
            }

            SetActive(evaluationRoot, evaluationOpen);
        }

        private void RefreshCanvas(MiniGamePanelViewData panel, MiniGameGameplayViewData gameplay)
        {
            if (drawingCanvas == null)
            {
                return;
            }

            var previousCanvas = panel.CurrentTask?.PreviousCanvas;
            if (gameplay.LocalPlayerState != MiniGamePlayerState.Drawing && previousCanvas != null)
            {
                drawingCanvas.LoadReadonlyCanvas(previousCanvas);
            }
        }

        private void UpdatePromptTimers(MiniGameGameplayViewData gameplay)
        {
            if (gameplay.Phase == _lastPhase && gameplay.LocalPlayerState == _lastLocalState)
            {
                return;
            }

            _lastPhase = gameplay.Phase;
            _lastLocalState = gameplay.LocalPlayerState;
            _countdownPlayedForPhase = false;
            _drawPromptHideTime = 0f;
            _answerPromptHideTime = 0f;

            if (gameplay.LocalPlayerState == MiniGamePlayerState.Drawing)
            {
                _drawPromptHideTime = Time.unscaledTime + Mathf.Max(0.1f, rolePromptSeconds);
                PlayOneShot(drawPromptClip);
            }
            else if (gameplay.LocalPlayerState == MiniGamePlayerState.Answering)
            {
                _answerPromptHideTime = Time.unscaledTime + Mathf.Max(0.1f, rolePromptSeconds);
                PlayOneShot(answerPromptClip);
            }
        }

        private void RefreshPromptRoots()
        {
            var now = Time.unscaledTime;
            var showDrawPrompt = _drawPromptHideTime > now;
            var showAnswerPrompt = _answerPromptHideTime > now;
            SetActive(drawPromptRoot, showDrawPrompt);
            SetActive(answerPromptRoot, showAnswerPrompt);
        }

        private void ResetTimer(float remainingSeconds)
        {
            _timerBaseSeconds = Mathf.Max(0f, remainingSeconds);
            _timerBaseRealtime = Time.unscaledTime;
            UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            if (timerText == null)
            {
                return;
            }

            var current = CurrentRemainingSeconds();
            var totalSeconds = Mathf.CeilToInt(current);
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void TryPlayCountdownAudio()
        {
            if (_countdownPlayedForPhase || countdownFiveSecondsClip == null)
            {
                return;
            }

            var remaining = CurrentRemainingSeconds();
            if (remaining > 0f && remaining <= 5f)
            {
                _countdownPlayedForPhase = true;
                PlayOneShot(countdownFiveSecondsClip);
            }
        }

        private float CurrentRemainingSeconds()
        {
            return Mathf.Max(0f, _timerBaseSeconds - (Time.unscaledTime - _timerBaseRealtime));
        }

        private void SubmitEvaluation(bool agreed)
        {
            if (!EnsureController(true))
            {
                return;
            }

            if (!miniGameController.SubmitSequentialEvaluation(agreed))
            {
                ShowToast("提交评价失败，请确认当前是否处于结算阶段。");
            }
        }

        private string BuildTopicText(MiniGameGameplayViewData gameplay)
        {
            if (gameplay.Phase == MiniGameGameplayPhase.Settlement)
            {
                return string.IsNullOrWhiteSpace(gameplay.FinalOriginalWord)
                    ? "原词：--"
                    : $"原词：{gameplay.FinalOriginalWord}";
            }

            if (string.IsNullOrWhiteSpace(gameplay.VisiblePromptText))
            {
                return "题目：--";
            }

            return gameplay.PromptIsOriginalWord
                ? $"题目：{gameplay.VisiblePromptText}"
                : $"上一位猜测：{gameplay.VisiblePromptText}";
        }

        private string BuildAnswerText(MiniGameGameplayViewData gameplay)
        {
            var text = gameplay.Phase == MiniGameGameplayPhase.Settlement
                ? gameplay.FinalGuessText
                : gameplay.VisibleAnswerText;
            return string.IsNullOrWhiteSpace(text) ? "回答：--" : $"回答：{text}";
        }

        private string BuildAnswererText(MiniGameGameplayViewData gameplay)
        {
            var name = gameplay.Phase == MiniGameGameplayPhase.Settlement
                ? gameplay.FinalAnswererDisplayName
                : gameplay.AnswererDisplayName;
            return string.IsNullOrWhiteSpace(name) ? "作答者：--" : $"作答者：{name}";
        }

        private string BuildPlayerListText(MiniGamePlayerViewData[] players)
        {
            _builder.Length = 0;
            if (players == null || players.Length == 0)
            {
                return "暂无玩家";
            }

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.IsHost)
                {
                    _builder.Append("[房主] ");
                }
                if (player.IsLocalPlayer)
                {
                    _builder.Append("[我] ");
                }

                _builder.Append(string.IsNullOrWhiteSpace(player.DisplayName) ? player.PlayerId : player.DisplayName);
                _builder.Append(" - ");
                _builder.Append(string.IsNullOrWhiteSpace(player.PlayerStateText) ? ToPlayerStateText(player.PlayerState) : player.PlayerStateText);
                if (!player.IsConnected)
                {
                    _builder.Append("（断线）");
                }
                _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private string BuildChatText(MiniGameChatViewData[] chats)
        {
            _builder.Length = 0;
            if (chats == null || chats.Length == 0)
            {
                return string.Empty;
            }

            var start = Mathf.Max(0, chats.Length - Mathf.Max(1, maxChatLines));
            for (var i = start; i < chats.Length; i++)
            {
                var chat = chats[i];
                if (chat == null || string.IsNullOrWhiteSpace(chat.Text))
                {
                    continue;
                }

                _builder.Append(chat.IsLocalPlayer ? "我" : FirstNonEmpty(chat.DisplayName, chat.PlayerId));
                _builder.Append("：");
                _builder.AppendLine(chat.Text);
            }

            return _builder.ToString();
        }

        private string BuildEvaluationText(MiniGameEvaluationViewData[] evaluations)
        {
            _builder.Length = 0;
            if (evaluations == null || evaluations.Length == 0)
            {
                return "暂无评价";
            }

            for (var i = 0; i < evaluations.Length; i++)
            {
                var evaluation = evaluations[i];
                if (evaluation == null)
                {
                    continue;
                }

                _builder.Append(evaluation.IsLocalPlayer ? "我" : FirstNonEmpty(evaluation.DisplayName, evaluation.PlayerId));
                _builder.Append("：");
                if (!evaluation.CanEvaluate)
                {
                    _builder.Append("仅查看");
                }
                else if (!evaluation.HasEvaluated)
                {
                    _builder.Append("待评价");
                }
                else
                {
                    _builder.Append(evaluation.Agreed ? "赞同" : "不赞同");
                }
                _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private void ClearView()
        {
            _lastPanel = null;
            _lastGameplay = null;
            _lastPhase = MiniGameGameplayPhase.None;
            _lastLocalState = MiniGamePlayerState.None;
            SetActive(gameplayRoot, false);
        }

        private void ShowToast(string text)
        {
            if (toastText == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            toastText.text = text;
            toastText.gameObject.SetActive(true);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (promptAudioSource != null && clip != null)
            {
                promptAudioSource.PlayOneShot(clip);
            }
        }

        private void ApplyAccess(GameObject root, MiniGameUIAccessState access, params Selectable[] selectables)
        {
            var visible = access != MiniGameUIAccessState.Hidden;
            var interactable = access == MiniGameUIAccessState.Open;
            SetActive(root, visible);
            if (selectables == null)
            {
                return;
            }

            for (var i = 0; i < selectables.Length; i++)
            {
                SetInteractable(selectables[i], interactable);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private void BindButtons()
        {
            Bind(pencilButton, ClickPencil);
            Bind(lineButton, ClickLine);
            Bind(eraserButton, ClickEraser);
            Bind(undoButton, ClickUndo);
            Bind(redoButton, ClickRedo);
            Bind(clearButton, ClickClearCanvas);
            Bind(finishDrawingButton, ClickFinishDrawing);
            Bind(sendChatButton, ClickSendChat);
            Bind(submitAnswerButton, ClickSubmitAnswer);
            Bind(continueGameButton, ClickContinueGame);
            Bind(exitGameButton, ClickExitGame);
            Bind(agreeButton, ClickAgree);
            Bind(disagreeButton, ClickDisagree);

            if (brushSizeSlider != null)
            {
                brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
            }

            if (colorButtons != null)
            {
                _colorButtonActions.Clear();
                for (var i = 0; i < colorButtons.Length; i++)
                {
                    var binding = colorButtons[i];
                    if (binding == null)
                    {
                        continue;
                    }

                    if (binding.Swatch != null)
                    {
                        binding.Swatch.color = binding.Color;
                    }

                    if (binding.Button != null)
                    {
                        var color = binding.Color;
                        UnityEngine.Events.UnityAction action = () => SetBrushColor(color);
                        _colorButtonActions.Add(action);
                        binding.Button.onClick.AddListener(action);
                    }
                }
            }
        }

        private void UnbindButtons()
        {
            Unbind(pencilButton, ClickPencil);
            Unbind(lineButton, ClickLine);
            Unbind(eraserButton, ClickEraser);
            Unbind(undoButton, ClickUndo);
            Unbind(redoButton, ClickRedo);
            Unbind(clearButton, ClickClearCanvas);
            Unbind(finishDrawingButton, ClickFinishDrawing);
            Unbind(sendChatButton, ClickSendChat);
            Unbind(submitAnswerButton, ClickSubmitAnswer);
            Unbind(continueGameButton, ClickContinueGame);
            Unbind(exitGameButton, ClickExitGame);
            Unbind(agreeButton, ClickAgree);
            Unbind(disagreeButton, ClickDisagree);

            if (brushSizeSlider != null)
            {
                brushSizeSlider.onValueChanged.RemoveListener(OnBrushSizeChanged);
            }

            if (colorButtons != null)
            {
                var actionIndex = 0;
                for (var i = 0; i < colorButtons.Length; i++)
                {
                    if (colorButtons[i]?.Button != null && actionIndex < _colorButtonActions.Count)
                    {
                        colorButtons[i].Button.onClick.RemoveListener(_colorButtonActions[actionIndex]);
                        actionIndex++;
                    }
                }
            }
            _colorButtonActions.Clear();
        }

        private void OnBrushSizeChanged(float value)
        {
            drawingCanvas?.SetBrushSize01(value);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private bool EnsureController(bool warn)
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
            if (miniGameController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameGameplayScreenUI] 未找到 NiumaMiniGameController，按钮命令无法发送。", this);
            }

            return miniGameController != null;
        }

        private bool EnsureSceneController(bool warn)
        {
            if (sceneController != null)
            {
                return true;
            }

            if (!autoFindSceneController)
            {
                return false;
            }

#if UNITY_2023_1_OR_NEWER
            sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
            sceneController = FindObjectOfType<NiumaSceneController>();
#endif
            if (sceneController == null && warn && logWarnings)
            {
                Debug.LogWarning("[MiniGameGameplayScreenUI] 未找到 NiumaSceneController，无法从游戏中界面直接返回场景。", this);
            }

            return sceneController != null;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
        }

        private static string ToPlayerStateText(MiniGamePlayerState state)
        {
            switch (state)
            {
                case MiniGamePlayerState.Waiting: return "等待中";
                case MiniGamePlayerState.Drawing: return "作画中";
                case MiniGamePlayerState.Answering: return "回答中";
                case MiniGamePlayerState.Done: return "已完成";
                case MiniGamePlayerState.Spectating: return "观战中";
                default: return "--";
            }
        }

        private static string CreateFallbackStrokeGroupId()
        {
            return $"empty_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
        }
    }
}
