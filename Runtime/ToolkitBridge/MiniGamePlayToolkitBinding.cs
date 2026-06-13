using System;
using NiumaMiniGame.Bridge;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.ViewData;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace NiumaMiniGame.ToolkitBridge
{
    public sealed class MiniGamePlayToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }
        [Serializable] public sealed class StrokeBeginEvent : UnityEvent<string, Color, float> { }
        [Serializable] public sealed class StrokeEndEvent : UnityEvent<string, int> { }
        [Serializable] public sealed class StrokeBatchEvent : UnityEvent<StrokePointBatch> { }

        [Header("子模块根节点 name")]
        [SerializeField] private string drawingBoardRootName = "DrawingBoardRoot";
        [SerializeField] private string brushToolsRootName = "BrushToolsRoot";
        [SerializeField] private string colorPaletteRootName = "ColorPaletteRoot";
        [SerializeField] private string chatRootName = "ChatRoot";
        [SerializeField] private string answerRootName = "AnswerRoot";
        [SerializeField] private string menuRootName = "MenuRoot";
        [SerializeField] private string topicRootName = "TopicRoot";
        [SerializeField] private string timerRootName = "TimerRoot";
        [SerializeField] private string playerListRootName = "PlayerListRoot";
        [SerializeField] private string drawPromptRootName = "DrawPromptRoot";
        [SerializeField] private string answerPromptRootName = "AnswerPromptRoot";
        [SerializeField] private string evaluationRootName = "EvaluationRoot";

        [Header("文本/输入/列表 name")]
        [SerializeField, Tooltip("画布 VisualElement 的 name。可以是 MiniGameDrawingBoardElement，也可以是普通 VisualElement。")]
        private string drawingBoardElementName = "DrawingBoardElement";
        [SerializeField] private string drawerNameLabelName = "DrawerNameText";
        [SerializeField] private string topicLabelName = "TopicText";
        [SerializeField] private string answerLabelName = "AnswerText";
        [SerializeField] private string answererLabelName = "AnswererText";
        [SerializeField] private string timerLabelName = "TimerText";
        [SerializeField] private string statusLabelName = "StatusText";
        [SerializeField] private string answerInputName = "AnswerInput";
        [SerializeField] private string chatInputName = "ChatInput";
        [SerializeField] private string brushSizeSliderName = "BrushSizeSlider";
        [SerializeField] private string playersListName = "PlayersList";
        [SerializeField] private string chatListName = "ChatList";
        [SerializeField] private string evaluationListName = "EvaluationList";
        [SerializeField] private string playersEmptyRootName = "PlayersEmptyRoot";
        [SerializeField] private string chatEmptyRootName = "ChatEmptyRoot";
        [SerializeField] private string evaluationEmptyRootName = "EvaluationEmptyRoot";

        [Header("按钮 name")]
        [SerializeField] private string pencilButtonName = "PencilButton";
        [SerializeField] private string penButtonName = "PenButton";
        [SerializeField] private string eraserButtonName = "EraserButton";
        [SerializeField] private string blackButtonName = "BlackColorButton";
        [SerializeField] private string redButtonName = "RedColorButton";
        [SerializeField] private string yellowButtonName = "YellowColorButton";
        [SerializeField] private string blueButtonName = "BlueColorButton";
        [SerializeField] private string greenButtonName = "GreenColorButton";
        [SerializeField] private string purpleButtonName = "PurpleColorButton";
        [SerializeField] private string undoButtonName = "UndoButton";
        [SerializeField] private string redoButtonName = "RedoButton";
        [SerializeField] private string clearButtonName = "ClearButton";
        [SerializeField] private string finishDrawingButtonName = "FinishDrawingButton";
        [SerializeField] private string submitAnswerButtonName = "SubmitAnswerButton";
        [SerializeField] private string continueButtonName = "ContinueButton";
        [SerializeField] private string quitButtonName = "QuitButton";
        [SerializeField] private string sendChatButtonName = "SendChatButton";
        [SerializeField] private string flowerGiftButtonName = "FlowerGiftButton";
        [SerializeField] private string eggGiftButtonName = "EggGiftButton";
        [SerializeField] private string agreeButtonName = "AgreeButton";
        [SerializeField] private string disagreeButtonName = "DisagreeButton";

        [Header("画板参数")]
        [SerializeField] private float defaultBrushSize = 6f;
        [SerializeField] private float minBrushSize = 2f;
        [SerializeField] private float maxBrushSize = 32f;
        [SerializeField] private int strokeBatchPointLimit = 12;
        [SerializeField] private string emptyAnswerText = "未作答";
        [SerializeField] private int maxRows = 120;
        [SerializeField] private string rowClass = "niuma-minigame-row";
        [SerializeField] private string selectedRowClass = "is-selected";
        [SerializeField] private string disabledRowClass = "is-disabled";

        [Header("交互事件：拖 MiniGameToolkitCommandRelay 或 NiumaMiniGameController 对应方法")]
        [SerializeField] private StringEvent onSendChatRequested = new StringEvent();
        [SerializeField] private StringEvent onSubmitAnswerRequested = new StringEvent();
        [SerializeField] private StringEvent onFinishDrawingRequested = new StringEvent();
        [SerializeField] private UnityEvent onContinueRequested = new UnityEvent();
        [SerializeField] private UnityEvent onQuitRequested = new UnityEvent();
        [SerializeField] private UnityEvent onClearCanvasRequested = new UnityEvent();
        [SerializeField] private StringEvent onUndoStrokeRequested = new StringEvent();
        [SerializeField] private StringEvent onGiftRequested = new StringEvent();
        [SerializeField] private BoolEvent onEvaluationSubmitted = new BoolEvent();
        [SerializeField] private StrokeBeginEvent onStrokeBegin = new StrokeBeginEvent();
        [SerializeField] private StrokeBatchEvent onStrokePointBatch = new StrokeBatchEvent();
        [SerializeField] private StrokeEndEvent onStrokeEnd = new StrokeEndEvent();

        protected override string DefaultProviderId => "MiniGamePlayPanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new MiniGamePlayToolkitBinding(this);
        }

        internal string DrawingBoardRootName => drawingBoardRootName;
        internal string BrushToolsRootName => brushToolsRootName;
        internal string ColorPaletteRootName => colorPaletteRootName;
        internal string ChatRootName => chatRootName;
        internal string AnswerRootName => answerRootName;
        internal string MenuRootName => menuRootName;
        internal string TopicRootName => topicRootName;
        internal string TimerRootName => timerRootName;
        internal string PlayerListRootName => playerListRootName;
        internal string DrawPromptRootName => drawPromptRootName;
        internal string AnswerPromptRootName => answerPromptRootName;
        internal string EvaluationRootName => evaluationRootName;
        internal string DrawingBoardElementName => drawingBoardElementName;
        internal string DrawerNameLabelName => drawerNameLabelName;
        internal string TopicLabelName => topicLabelName;
        internal string AnswerLabelName => answerLabelName;
        internal string AnswererLabelName => answererLabelName;
        internal string TimerLabelName => timerLabelName;
        internal string StatusLabelName => statusLabelName;
        internal string AnswerInputName => answerInputName;
        internal string ChatInputName => chatInputName;
        internal string BrushSizeSliderName => brushSizeSliderName;
        internal string PlayersListName => playersListName;
        internal string ChatListName => chatListName;
        internal string EvaluationListName => evaluationListName;
        internal string PlayersEmptyRootName => playersEmptyRootName;
        internal string ChatEmptyRootName => chatEmptyRootName;
        internal string EvaluationEmptyRootName => evaluationEmptyRootName;
        internal string PencilButtonName => pencilButtonName;
        internal string PenButtonName => penButtonName;
        internal string EraserButtonName => eraserButtonName;
        internal string BlackButtonName => blackButtonName;
        internal string RedButtonName => redButtonName;
        internal string YellowButtonName => yellowButtonName;
        internal string BlueButtonName => blueButtonName;
        internal string GreenButtonName => greenButtonName;
        internal string PurpleButtonName => purpleButtonName;
        internal string UndoButtonName => undoButtonName;
        internal string RedoButtonName => redoButtonName;
        internal string ClearButtonName => clearButtonName;
        internal string FinishDrawingButtonName => finishDrawingButtonName;
        internal string SubmitAnswerButtonName => submitAnswerButtonName;
        internal string ContinueButtonName => continueButtonName;
        internal string QuitButtonName => quitButtonName;
        internal string SendChatButtonName => sendChatButtonName;
        internal string FlowerGiftButtonName => flowerGiftButtonName;
        internal string EggGiftButtonName => eggGiftButtonName;
        internal string AgreeButtonName => agreeButtonName;
        internal string DisagreeButtonName => disagreeButtonName;
        internal float DefaultBrushSize => defaultBrushSize;
        internal float MinBrushSize => minBrushSize;
        internal float MaxBrushSize => maxBrushSize;
        internal int StrokeBatchPointLimit => strokeBatchPointLimit;
        internal string EmptyAnswerText => emptyAnswerText;
        internal int MaxRows => maxRows;
        internal string RowClass => rowClass;
        internal string SelectedRowClass => selectedRowClass;
        internal string DisabledRowClass => disabledRowClass;
        internal void SendChat(string text) => onSendChatRequested?.Invoke(text);
        internal void SubmitAnswer(string answer) => onSubmitAnswerRequested?.Invoke(answer);
        internal void FinishDrawing(string strokeGroupId) => onFinishDrawingRequested?.Invoke(strokeGroupId);
        internal void Continue() => onContinueRequested?.Invoke();
        internal void Quit() => onQuitRequested?.Invoke();
        internal void ClearCanvas() => onClearCanvasRequested?.Invoke();
        internal void UndoStroke(string strokeId) => onUndoStrokeRequested?.Invoke(strokeId);
        internal void Gift(string giftType) => onGiftRequested?.Invoke(giftType);
        internal void Evaluate(bool agreed) => onEvaluationSubmitted?.Invoke(agreed);
        internal void StrokeBegin(string strokeId, Color color, float brushSize) => onStrokeBegin?.Invoke(strokeId, color, brushSize);
        internal void StrokeBatch(StrokePointBatch batch) => onStrokePointBatch?.Invoke(batch);
        internal void StrokeEnd(string strokeId, int totalPoints) => onStrokeEnd?.Invoke(strokeId, totalPoints);
    }

    public sealed class MiniGamePlayToolkitBinding : ToolkitViewBindingBase<MiniGamePlayUIUpdate, MiniGamePlayPanelViewModel>
    {
        private readonly MiniGamePlayToolkitBindingProvider _p;
        private VisualElement _drawingBoardRoot;
        private VisualElement _brushToolsRoot;
        private VisualElement _colorPaletteRoot;
        private VisualElement _chatRoot;
        private VisualElement _answerRoot;
        private VisualElement _menuRoot;
        private VisualElement _topicRoot;
        private VisualElement _timerRoot;
        private VisualElement _playerListRoot;
        private VisualElement _drawPromptRoot;
        private VisualElement _answerPromptRoot;
        private VisualElement _evaluationRoot;
        private VisualElement _drawingBoardElement;
        private Label _drawerName;
        private Label _topic;
        private Label _answer;
        private Label _answerer;
        private Label _timer;
        private Label _status;
        private TextField _answerInput;
        private TextField _chatInput;
        private Slider _brushSizeSlider;
        private Button _finishButton;
        private Button _submitAnswerButton;
        private Button _agreeButton;
        private Button _disagreeButton;
        private readonly ToolkitListBinding<ToolkitTextRowData> _players = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly ToolkitListBinding<ToolkitTextRowData> _chats = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly ToolkitListBinding<ToolkitTextRowData> _evaluations = new ToolkitListBinding<ToolkitTextRowData>();
        private readonly MiniGameDrawingBoardInput _drawingInput = new MiniGameDrawingBoardInput();

        public MiniGamePlayToolkitBinding(MiniGamePlayToolkitBindingProvider provider)
        {
            _p = provider;
        }

        protected override void OnInitializeTyped()
        {
            _drawingBoardRoot = Query<VisualElement>(_p.DrawingBoardRootName);
            _brushToolsRoot = Query<VisualElement>(_p.BrushToolsRootName);
            _colorPaletteRoot = Query<VisualElement>(_p.ColorPaletteRootName);
            _chatRoot = Query<VisualElement>(_p.ChatRootName);
            _answerRoot = Query<VisualElement>(_p.AnswerRootName);
            _menuRoot = Query<VisualElement>(_p.MenuRootName);
            _topicRoot = Query<VisualElement>(_p.TopicRootName);
            _timerRoot = Query<VisualElement>(_p.TimerRootName);
            _playerListRoot = Query<VisualElement>(_p.PlayerListRootName);
            _drawPromptRoot = Query<VisualElement>(_p.DrawPromptRootName);
            _answerPromptRoot = Query<VisualElement>(_p.AnswerPromptRootName);
            _evaluationRoot = Query<VisualElement>(_p.EvaluationRootName);
            _drawingBoardElement = Query<VisualElement>(_p.DrawingBoardElementName);
            _drawerName = QLabel(_p.DrawerNameLabelName);
            _topic = QLabel(_p.TopicLabelName);
            _answer = QLabel(_p.AnswerLabelName);
            _answerer = QLabel(_p.AnswererLabelName);
            _timer = QLabel(_p.TimerLabelName);
            _status = QLabel(_p.StatusLabelName);
            _answerInput = Query<TextField>(_p.AnswerInputName);
            _chatInput = Query<TextField>(_p.ChatInputName);
            _brushSizeSlider = Query<Slider>(_p.BrushSizeSliderName);
            _finishButton = QButton(_p.FinishDrawingButtonName);
            _submitAnswerButton = QButton(_p.SubmitAnswerButtonName);
            _agreeButton = QButton(_p.AgreeButtonName);
            _disagreeButton = QButton(_p.DisagreeButtonName);

            if (_brushSizeSlider != null)
            {
                _brushSizeSlider.lowValue = Mathf.Max(0.001f, _p.MinBrushSize);
                _brushSizeSlider.highValue = Mathf.Max(_brushSizeSlider.lowValue, _p.MaxBrushSize);
                _brushSizeSlider.SetValueWithoutNotify(Mathf.Clamp(_p.DefaultBrushSize, _brushSizeSlider.lowValue, _brushSizeSlider.highValue));
            }

            ViewModel.BrushSize = Mathf.Max(0.001f, _p.DefaultBrushSize);
            _drawingInput.Attach(_drawingBoardElement);
            _drawingInput.Configure(ViewModel.BrushColor, ViewModel.BrushSize, _p.StrokeBatchPointLimit);
            _drawingInput.StrokeBegan += HandleStrokeBegan;
            _drawingInput.StrokeBatchReady += batch => _p.StrokeBatch(batch);
            _drawingInput.StrokeEnded += HandleStrokeEnded;

            _players.Bind(Root, _p.PlayersListName, new ToolkitTextRowItemBinder(_p.RowClass, _p.SelectedRowClass, _p.DisabledRowClass, null), _p.PlayersEmptyRootName);
            _chats.Bind(Root, _p.ChatListName, new ToolkitTextRowItemBinder(_p.RowClass, _p.SelectedRowClass, _p.DisabledRowClass, null), _p.ChatEmptyRootName);
            _evaluations.Bind(Root, _p.EvaluationListName, new ToolkitTextRowItemBinder(_p.RowClass, _p.SelectedRowClass, _p.DisabledRowClass, null), _p.EvaluationEmptyRootName);

            Callbacks.RegisterValueChanged(_answerInput, value => ViewModel.AnswerInput = value);
            Callbacks.RegisterValueChanged(_chatInput, value => ViewModel.ChatInput = value);
            Callbacks.RegisterValueChanged(_brushSizeSlider, value => { ViewModel.BrushSize = Mathf.Clamp(value, _p.MinBrushSize, _p.MaxBrushSize); ConfigureDrawingInput(); });
            Callbacks.RegisterButton(Root, _p.PencilButtonName, () => SetTool(MiniGameBrushTool.Pencil));
            Callbacks.RegisterButton(Root, _p.PenButtonName, () => SetTool(MiniGameBrushTool.Pen));
            Callbacks.RegisterButton(Root, _p.EraserButtonName, () => SetTool(MiniGameBrushTool.Eraser));
            Callbacks.RegisterButton(Root, _p.BlackButtonName, () => SetColor(Color.black));
            Callbacks.RegisterButton(Root, _p.RedButtonName, () => SetColor(Color.red));
            Callbacks.RegisterButton(Root, _p.YellowButtonName, () => SetColor(Color.yellow));
            Callbacks.RegisterButton(Root, _p.BlueButtonName, () => SetColor(Color.blue));
            Callbacks.RegisterButton(Root, _p.GreenButtonName, () => SetColor(Color.green));
            Callbacks.RegisterButton(Root, _p.PurpleButtonName, () => SetColor(new Color(0.55f, 0.2f, 0.85f, 1f)));
            Callbacks.RegisterButton(Root, _p.UndoButtonName, () => _p.UndoStroke(ViewModel.CurrentStrokeId));
            Callbacks.RegisterButton(Root, _p.RedoButtonName, () => SetText(_status, "还原功能需要后端确认 Stroke 历史后再启用"));
            Callbacks.RegisterButton(Root, _p.ClearButtonName, () => _p.ClearCanvas());
            Callbacks.RegisterButton(Root, _p.FinishDrawingButtonName, HandleFinishDrawing);
            Callbacks.RegisterButton(Root, _p.SubmitAnswerButtonName, HandleSubmitAnswer);
            Callbacks.RegisterButton(Root, _p.ContinueButtonName, () => _p.Continue());
            Callbacks.RegisterButton(Root, _p.QuitButtonName, () => _p.Quit());
            Callbacks.RegisterButton(Root, _p.SendChatButtonName, HandleSendChat);
            Callbacks.RegisterButton(Root, _p.FlowerGiftButtonName, () => _p.Gift("flower"));
            Callbacks.RegisterButton(Root, _p.EggGiftButtonName, () => _p.Gift("egg"));
            Callbacks.RegisterButton(Root, _p.AgreeButtonName, () => _p.Evaluate(true));
            Callbacks.RegisterButton(Root, _p.DisagreeButtonName, () => _p.Evaluate(false));
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(MiniGamePlayUIUpdate viewData, MiniGamePlayPanelViewModel viewModel)
        {
            viewModel.Apply(viewData, _p.MaxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _players.Clear();
            _chats.Clear();
            _evaluations.Clear();
            _drawingInput.CancelActiveStroke();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _players.Dispose();
            _chats.Dispose();
            _evaluations.Dispose();
            _drawingInput.Dispose();
        }

        private void ApplyVisualState(MiniGamePlayPanelViewModel vm)
        {
            if (vm == null)
                return;

            var gameplay = vm.Gameplay;
            var access = gameplay?.Access;
            ApplyAccess(_drawingBoardRoot, access?.DrawingBoard ?? MiniGameUIAccessState.Display);
            ApplyAccess(_brushToolsRoot, access?.BrushTools ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_colorPaletteRoot, access?.ColorPalette ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_chatRoot, access?.Chat ?? MiniGameUIAccessState.Open);
            ApplyAccess(_answerRoot, access?.Answer ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_menuRoot, access?.Menu ?? MiniGameUIAccessState.Open);
            ApplyAccess(_topicRoot, access?.Topic ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_timerRoot, access?.Timer ?? MiniGameUIAccessState.Display);
            ApplyAccess(_playerListRoot, access?.PlayerList ?? MiniGameUIAccessState.Display);
            ApplyAccess(_drawPromptRoot, access?.DrawPrompt ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_answerPromptRoot, access?.AnswerPrompt ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_evaluationRoot, access?.Evaluation ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_finishButton, access?.FinishButton ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_submitAnswerButton, access?.Answer ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_agreeButton, access?.AgreeButton ?? MiniGameUIAccessState.Hidden);
            ApplyAccess(_disagreeButton, access?.DisagreeButton ?? MiniGameUIAccessState.Hidden);

            SetText(_drawerName, $"作画人：{MiniGameToolkitText.Text(gameplay?.CurrentDrawerDisplayName, gameplay?.CurrentDrawerPlayerId)}");
            SetText(_topic, gameplay?.PromptIsOriginalWord == true ? $"题目：{gameplay.VisiblePromptText}" : $"上一位回答：{gameplay?.VisiblePromptText}");
            SetText(_answer, $"回答：{MiniGameToolkitText.Text(gameplay?.VisibleAnswerText, gameplay?.FinalGuessText)}");
            SetText(_answerer, $"作答者：{MiniGameToolkitText.Text(gameplay?.AnswererDisplayName, gameplay?.FinalAnswererPlayerId)}");
            SetText(_timer, FormatTimer(gameplay?.RemainingSeconds ?? 0f));
            SetText(_status, vm.Panel?.LastToast?.Text ?? vm.Panel?.LastError?.DebugMessage);
            SetTextField(_answerInput, vm.AnswerInput);
            SetTextField(_chatInput, vm.ChatInput);
            _players.ReplaceAll(vm.PlayerRows);
            _chats.ReplaceAll(vm.ChatRows);
            _evaluations.ReplaceAll(vm.EvaluationRows);
        }

        private void ApplyAccess(VisualElement element, MiniGameUIAccessState state)
        {
            SetElementVisible(element, MiniGameToolkitText.IsVisible(state));
            SetElementEnabled(element, MiniGameToolkitText.IsOpen(state));
        }

        private void SetTool(MiniGameBrushTool tool)
        {
            ViewModel.BrushTool = tool;
            ConfigureDrawingInput();
        }

        private void SetColor(Color color)
        {
            ViewModel.BrushColor = color;
            ConfigureDrawingInput();
        }

        private void ConfigureDrawingInput()
        {
            var color = ViewModel.BrushTool == MiniGameBrushTool.Eraser ? Color.white : ViewModel.BrushColor;
            _drawingInput.Configure(color, ViewModel.BrushSize, _p.StrokeBatchPointLimit);
        }

        private void HandleStrokeBegan(string strokeId, Color color, float brushSize)
        {
            ViewModel.CurrentStrokeId = strokeId;
            ViewModel.CurrentStrokeGroupId = strokeId;
            _p.StrokeBegin(strokeId, color, brushSize);
        }

        private void HandleStrokeEnded(string strokeId, int totalPoints)
        {
            _p.StrokeEnd(strokeId, totalPoints);
        }

        private void HandleFinishDrawing()
        {
            _drawingInput.CancelActiveStroke();
            var strokeGroupId = string.IsNullOrWhiteSpace(ViewModel.CurrentStrokeGroupId) ? ViewModel.CurrentStrokeId : ViewModel.CurrentStrokeGroupId;
            _p.FinishDrawing(strokeGroupId);
        }

        private void HandleSubmitAnswer()
        {
            var answer = string.IsNullOrWhiteSpace(ViewModel.AnswerInput) ? _p.EmptyAnswerText : ViewModel.AnswerInput.Trim();
            _p.SubmitAnswer(answer);
            ViewModel.AnswerInput = string.Empty;
            SetTextField(_answerInput, string.Empty);
        }

        private void HandleSendChat()
        {
            var text = string.IsNullOrWhiteSpace(ViewModel.ChatInput) ? string.Empty : ViewModel.ChatInput.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _p.SendChat(text);
            ViewModel.ChatInput = string.Empty;
            SetTextField(_chatInput, string.Empty);
        }

        private static string FormatTimer(float seconds)
        {
            var value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }

        private static void SetTextField(TextField field, string value)
        {
            if (field != null && !string.Equals(field.value, value ?? string.Empty, StringComparison.Ordinal))
                field.SetValueWithoutNotify(value ?? string.Empty);
        }
    }
}
