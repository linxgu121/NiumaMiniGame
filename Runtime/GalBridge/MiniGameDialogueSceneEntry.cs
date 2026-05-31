using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Presenter;
using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NiumaMiniGame.GalBridge
{
    /// <summary>
    /// Gal 对话结束后的 MiniGame 场景入口。
    /// 不修改 Gal 核心流程，只在指定对话完整结束后弹出“进入 / 下次再说”选项。
    /// </summary>
    public sealed class MiniGameDialogueSceneEntry : MonoBehaviour
    {
        [Header("Gal 引用")]
        [Tooltip("当前场景中的 NiumaDialogueController。用于读取刚结束的 DialogueAsset 和播放进度。")]
        [SerializeField] private NiumaDialogueController dialogueController;

        [Tooltip("当前场景中的 DialoguePresenter。入口组件通过它的关闭事件判断对话是否结束。")]
        [SerializeField] private DialoguePresenter dialoguePresenter;

        [Tooltip("触发小游戏入口的对话资源。推荐直接绑定 NPC 对话使用的 DialogueAsset。")]
        [SerializeField] private DialogueAsset triggerDialogueAsset;

        [Tooltip("触发小游戏入口的稳定 DialogueId。资源引用为空时使用它匹配；正式内容建议填写，不依赖资源文件名。")]
        [SerializeField] private string triggerDialogueId;

        [Tooltip("是否允许在 DialogueId 为空时回退到 DialogueAsset.name。正式内容建议关闭，避免资源重命名导致入口失效。")]
        [SerializeField] private bool allowAssetNameFallback;

        [Header("入口 UI")]
        [Tooltip("二选一入口面板。进入小游戏 / 下次再说的按钮事件由该面板转发。")]
        [SerializeField] private MiniGameDialogueChoicePanel choicePanel;

        [Tooltip("入口面板标题。")]
        [SerializeField] private string choiceTitle = "要进入你画我猜吗？";

        [Tooltip("进入按钮文本。")]
        [SerializeField] private string enterText = "进入你画我猜";

        [Tooltip("取消按钮文本。")]
        [SerializeField] private string cancelText = "下次再说";

        [Header("场景跳转")]
        [Tooltip("NiumaScene 根控制器。为空时会自动查找。进入小游戏时通过它压入返回上下文并切换场景。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("你画我猜 2D 开始界面的场景名。需要提前加入 Build Settings。")]
        [SerializeField] private string miniGameSceneName = "MiniGame_DrawTelephone";

        [Tooltip("场景加载方式。NiumaScene 第一版会强制 Single；保留该字段仅用于兼容旧配置。")]
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

        [Tooltip("从小游戏返回 RPG 场景时使用的出生点 ID。应绑定 NPC 附近的 SceneSpawnPoint，例如 npc_minigame_exit。")]
        [SerializeField] private string returnSpawnPointId = "npc_minigame_exit";

        [Tooltip("进入小游戏前是否压入返回上下文。NPC 进入小游戏流程必须开启。")]
        [SerializeField] private bool pushReturnContext = true;

        [Tooltip("加载小游戏场景时是否冻结玩家输入。")]
        [SerializeField] private bool freezeInputDuringLoad = true;

        [Tooltip("加载小游戏场景时是否请求显示 Loading UI。第七阶段接入 LoadingPanel 后生效。")]
        [SerializeField] private bool showLoadingUI = true;

        [Tooltip("进入小游戏前是否请求保存检查点意图。第六阶段接入 NiumaSave 后生效。")]
        [SerializeField] private bool requestCheckpointSave;

        [Header("行为开关")]
        [Tooltip("为 true 时，只有对话完整推进到最后一句之后关闭，才会弹出入口。中途强制关闭不会触发。")]
        [SerializeField] private bool requireCompletedDialogue = true;

        [Tooltip("为 true 时，组件启用时会自动查找未手动绑定的 Controller、Presenter 和入口面板。")]
        [SerializeField] private bool autoFindReferences = true;

        [Tooltip("为 true 时，会在引用缺失或配置错误时输出警告，便于场景搭建排查。")]
        [SerializeField] private bool logWarnings = true;

        [Header("外部事件")]
        [Tooltip("玩家选择进入小游戏时触发。场景加载前调用，可用于播放音效或写入埋点。")]
        [SerializeField] private UnityEvent onEnterMiniGame = new UnityEvent();

        [Tooltip("玩家选择下次再说时触发。")]
        [SerializeField] private UnityEvent onCancelMiniGame = new UnityEvent();

        private bool _presenterSubscribed;
        private bool _panelSubscribed;

        public UnityEvent OnEnterMiniGame => onEnterMiniGame;
        public UnityEvent OnCancelMiniGame => onCancelMiniGame;

        private void OnEnable()
        {
            ResolveReferences(logWarnings);
            SubscribePresenter();
            SubscribePanel();
        }

        private void OnDisable()
        {
            UnsubscribePresenter();
            UnsubscribePanel();

            if (choicePanel != null)
            {
                choicePanel.Hide();
            }
        }

        /// <summary>
        /// 对话 UI 关闭后检查当前对话是否命中小游戏入口条件。
        /// </summary>
        private void HandleDialogueClosed()
        {
            if (!ResolveReferences(logWarnings))
            {
                return;
            }

            var currentDialogue = dialogueController.Blackboard?.CurrentDialogue;
            if (!IsTargetDialogue(currentDialogue))
            {
                return;
            }

            if (requireCompletedDialogue && !IsDialogueCompleted(currentDialogue))
            {
                return;
            }

            choicePanel.Show(choiceTitle, enterText, cancelText);
        }

        private void HandleEnterMiniGame()
        {
            onEnterMiniGame?.Invoke();

            if (string.IsNullOrWhiteSpace(miniGameSceneName))
            {
                LogWarning("未配置 MiniGame 场景名，无法进入你画我猜。", true);
                return;
            }

            if (!ResolveSceneController(true))
            {
                LogWarning("未找到 NiumaSceneController，无法通过统一场景服务进入你画我猜。", true);
                return;
            }

            var handle = sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = SceneLoadPurpose.MiniGame,
                Target = new SceneTransitionTarget
                {
                    SceneName = miniGameSceneName.Trim(),
                    LoadMode = loadSceneMode,
                    RestorePlayerAtSpawnPoint = false
                },
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = pushReturnContext,
                    ReturnSpawnPointId = pushReturnContext ? returnSpawnPointId : null
                },
                Options = new SceneTransitionOptions
                {
                    FreezeInputDuringLoad = freezeInputDuringLoad,
                    ShowLoadingUI = showLoadingUI,
                    RequestCheckpointSave = requestCheckpointSave,
                    ReplacePendingRequest = true,
                    ReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew
                }
            });

            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded))
            {
                LogWarning($"进入 MiniGame 场景失败：{handle.Result?.ErrorCode} {handle.Result?.ErrorMessage}", true);
            }
        }

        private void HandleCancelMiniGame()
        {
            onCancelMiniGame?.Invoke();
        }

        private bool IsTargetDialogue(DialogueAsset currentDialogue)
        {
            if (currentDialogue == null)
            {
                return false;
            }

            if (triggerDialogueAsset != null && currentDialogue == triggerDialogueAsset)
            {
                return true;
            }

            var currentId = ResolveDialogueId(currentDialogue);
            if (!string.IsNullOrWhiteSpace(triggerDialogueId))
            {
                return string.Equals(currentId, triggerDialogueId, System.StringComparison.Ordinal);
            }

            return false;
        }

        private bool IsDialogueCompleted(DialogueAsset currentDialogue)
        {
            if (currentDialogue == null || currentDialogue.Sentences == null)
            {
                return false;
            }

            return dialogueController.Blackboard != null
                   && dialogueController.Blackboard.CurrentSentenceIndex >= currentDialogue.Sentences.Count;
        }

        private string ResolveDialogueId(DialogueAsset dialogue)
        {
            if (dialogue == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dialogue.DialogueId))
            {
                return dialogue.DialogueId;
            }

            return allowAssetNameFallback ? dialogue.name : null;
        }

        private bool ResolveReferences(bool warn)
        {
            if (!autoFindReferences)
            {
                return ValidateReferences(warn);
            }

            if (dialogueController == null)
            {
                dialogueController = FindObjectOfType<NiumaDialogueController>();
            }

            if (dialoguePresenter == null)
            {
                dialoguePresenter = FindObjectOfType<DialoguePresenter>();
            }

            if (choicePanel == null)
            {
                choicePanel = FindObjectOfType<MiniGameDialogueChoicePanel>(true);
            }

            ResolveSceneController(false);

            return ValidateReferences(warn);
        }

        private bool ValidateReferences(bool warn)
        {
            if (dialogueController == null)
            {
                LogWarning("未绑定 NiumaDialogueController，无法监听小游戏入口对话。", warn);
                return false;
            }

            if (dialoguePresenter == null)
            {
                LogWarning("未绑定 DialoguePresenter，无法监听对话关闭事件。", warn);
                return false;
            }

            if (choicePanel == null)
            {
                LogWarning("未绑定 MiniGameDialogueChoicePanel，无法显示小游戏入口选项。", warn);
                return false;
            }

            if (triggerDialogueAsset == null && string.IsNullOrWhiteSpace(triggerDialogueId))
            {
                LogWarning("未配置触发对话资源或 DialogueId，入口组件不会触发。", warn);
                return false;
            }

            return true;
        }

        private bool ResolveSceneController(bool warn)
        {
            if (sceneController != null)
            {
                return true;
            }

            if (autoFindReferences)
            {
#if UNITY_2023_1_OR_NEWER
                sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
                sceneController = FindObjectOfType<NiumaSceneController>();
#endif
            }

            if (sceneController == null)
            {
                LogWarning("未绑定 NiumaSceneController，进入小游戏时无法压入返回上下文。", warn);
            }

            return sceneController != null;
        }

        private void SubscribePresenter()
        {
            if (_presenterSubscribed || dialoguePresenter == null)
            {
                return;
            }

            dialoguePresenter.OnCloseUI += HandleDialogueClosed;
            _presenterSubscribed = true;
        }

        private void UnsubscribePresenter()
        {
            if (!_presenterSubscribed || dialoguePresenter == null)
            {
                _presenterSubscribed = false;
                return;
            }

            dialoguePresenter.OnCloseUI -= HandleDialogueClosed;
            _presenterSubscribed = false;
        }

        private void SubscribePanel()
        {
            if (_panelSubscribed || choicePanel == null)
            {
                return;
            }

            choicePanel.OnEnter.AddListener(HandleEnterMiniGame);
            choicePanel.OnCancel.AddListener(HandleCancelMiniGame);
            _panelSubscribed = true;
        }

        private void UnsubscribePanel()
        {
            if (!_panelSubscribed || choicePanel == null)
            {
                _panelSubscribed = false;
                return;
            }

            choicePanel.OnEnter.RemoveListener(HandleEnterMiniGame);
            choicePanel.OnCancel.RemoveListener(HandleCancelMiniGame);
            _panelSubscribed = false;
        }

        private void LogWarning(string message, bool enabled)
        {
            if (enabled && logWarnings)
            {
                Debug.LogWarning($"[MiniGameDialogueSceneEntry] {message}", this);
            }
        }
    }
}
