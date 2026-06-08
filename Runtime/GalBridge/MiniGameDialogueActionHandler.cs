using System;
using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Service;
using NiumaGal.Enum;
using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaMiniGame.GalBridge
{
    /// <summary>
    /// MiniGame 对话行为桥接器。
    /// 挂到 NPC 对话所在场景后，会把 DialogueAsset 中的 OpenMiniGame / LoadScene 行为转发给 NiumaScene。
    /// </summary>
    public sealed class MiniGameDialogueActionHandler : MonoBehaviour, IDialogueActionHandler
    {
        private const string SceneNameKey = "sceneName";
        private const string SceneKey = "scene";
        private const string SpawnPointKey = "returnSpawnPointId";
        private const string PushReturnKey = "pushReturnContext";
        private const string RequestCheckpointKey = "requestCheckpointSave";
        private const string FreezeInputKey = "freezeInputDuringLoad";
        private const string ShowLoadingKey = "showLoadingUI";
        private const string PurposeKey = "purpose";

        [Header("对话服务")]
        [Tooltip("当前场景中的 NiumaDialogueController。启用时会把本组件注册为 DialogueService 的行为处理器。")]
        [SerializeField] private NiumaDialogueController dialogueController;

        [Tooltip("备用行为处理脚本。用于继续处理本脚本不认识的对话行为，例如任务、剧情、音频桥接脚本；没有其它行为时可留空。")]
        [SerializeField] private MonoBehaviour fallbackActionHandlerProvider;

        [Tooltip("启用组件时是否自动注册到 DialogueService。正式场景建议开启。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("禁用组件时是否把 DialogueService 的行为处理器恢复为备用处理器；没有备用处理器时会清空。")]
        [SerializeField] private bool restoreFallbackOnDisable = true;

        [Header("场景服务")]
        [Tooltip("NiumaScene 根控制器。为空时可自动在场景中查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("没有手动绑定控制器时是否自动查找场景中的 NiumaDialogueController / NiumaSceneController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindReferences = true;

        [Header("MiniGame 默认参数")]
        [Tooltip("你画我猜开始界面的场景名。DialogueAction.StringValue / TargetId / CustomData(sceneName) 可覆盖该值。")]
        [SerializeField] private string defaultMiniGameSceneName = "MiniGame_DrawTelephone";

        [Tooltip("从小游戏返回 RPG 场景时使用的出生点 ID（建议填 NPC 附近的 SceneSpawnPoint，例如 npc_minigame_exit）。")]
        [SerializeField] private string defaultReturnSpawnPointId = "npc_minigame_exit";

        [Tooltip("进入小游戏前是否压入返回上下文（RPG→MiniGame→RPG 必须开启；直接打开 MiniGame 测试场景可关闭并使用兜底返回场景）。")]
        [SerializeField] private bool pushReturnContext = true;

        [Tooltip("进入小游戏前是否请求检查点保存（剧情入口/重要节点可开启；纯调试入口可关闭）。第一版只发出意图，具体保存由 NiumaScene / NiumaSave 桥接处理。")]
        [SerializeField] private bool requestCheckpointSave;

        [Tooltip("场景加载期间是否冻结玩家输入（从 3D RPG 进入小游戏建议开启，避免切场景时角色继续移动）。")]
        [SerializeField] private bool freezeInputDuringLoad = true;

        [Tooltip("场景加载期间是否显示 Loading UI（正式切换建议开启；快速调试可关闭）。")]
        [SerializeField] private bool showLoadingUI = true;

        [Tooltip("场景加载方式。Single（切换到 MiniGame，会卸载当前业务场景；正式入口推荐）；Additive（叠加加载，适合核心场景/子场景；NiumaScene 第一版会规整为 Single）。")]
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

        [Header("容错")]
        [Tooltip("遇到本组件不认识的 Action 类型时是否直接视为成功。没有统一 ActionRouter 前建议开启，避免阻塞 Quest / Story 等后续行为。")]
        [SerializeField] private bool passUnsupportedActions = true;

        [Tooltip("配置缺失或执行失败时是否输出警告日志。")]
        [SerializeField] private bool logWarnings = true;

        private IDialogueActionHandler _fallbackActionHandler;

        private void OnEnable()
        {
            ResolveReferences(logWarnings);

            if (registerOnEnable)
            {
                RegisterToDialogueService();
            }
        }

        private void OnDisable()
        {
            if (!restoreFallbackOnDisable || dialogueController?.DialogueConfigurationService == null)
            {
                return;
            }

            ResolveFallbackHandler(false);
            dialogueController.DialogueConfigurationService.SetActionHandler(_fallbackActionHandler);
        }

        /// <summary>
        /// 执行 Gal 行为。只处理小游戏入口和通用场景加载，其余行为交给备用处理器或按配置放行。
        /// </summary>
        public DialogueOperationResult Execute(in DialogueActionContext context)
        {
            var action = context.Action;
            if (action == null)
            {
                return DialogueOperationResult.Fail(DialogueOperationFailureReason.InvalidRequest, "对话行为为空。");
            }

            switch (action.Type)
            {
                case DialogueActionType.OpenMiniGame:
                    return ExecuteSceneLoad(in context, true);

                case DialogueActionType.LoadScene:
                    return ExecuteSceneLoad(in context, false);

                default:
                    return ExecuteUnsupported(in context);
            }
        }

        [ContextMenu("NiumaMiniGame/GalBridge/注册为对话行为处理器")]
        private void RegisterToDialogueService()
        {
            if (!ResolveReferences(logWarnings) || dialogueController?.DialogueConfigurationService == null)
            {
                LogWarning("未找到可用的 DialogueConfigurationService，无法注册 MiniGame 对话行为处理器。", true);
                return;
            }

            ResolveFallbackHandler(false);
            dialogueController.DialogueConfigurationService.SetActionHandler(this);
        }

        private DialogueOperationResult ExecuteSceneLoad(in DialogueActionContext context, bool useMiniGameDefaults)
        {
            if (!ResolveSceneController(true))
            {
                return Fail(context, "未找到 NiumaSceneController，无法执行场景切换。");
            }

            var sceneName = ResolveSceneName(context.Action, useMiniGameDefaults);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return Fail(context, "未配置目标场景名，无法执行场景切换。");
            }

            var handle = sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = ResolvePurpose(context.Action, useMiniGameDefaults),
                Target = new SceneTransitionTarget
                {
                    SceneName = sceneName.Trim(),
                    LoadMode = loadSceneMode,
                    RestorePlayerAtSpawnPoint = false
                },
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = ResolveBool(context.Action, PushReturnKey, pushReturnContext),
                    ReturnSpawnPointId = ResolveReturnSpawnPoint(context.Action)
                },
                Options = new SceneTransitionOptions
                {
                    RequestCheckpointSave = ResolveBool(context.Action, RequestCheckpointKey, requestCheckpointSave),
                    FreezeInputDuringLoad = ResolveBool(context.Action, FreezeInputKey, freezeInputDuringLoad),
                    ShowLoadingUI = ResolveBool(context.Action, ShowLoadingKey, showLoadingUI),
                    ReplacePendingRequest = true,
                    ReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew
                }
            });

            if (handle == null)
            {
                return Fail(context, "NiumaSceneController 返回了空的场景切换句柄。");
            }

            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded))
            {
                var message = handle.Result != null
                    ? $"场景切换失败：{handle.Result.ErrorCode} {handle.Result.ErrorMessage}"
                    : $"场景切换失败：{handle.ErrorCode}";
                return Fail(context, message);
            }

            return Success(context);
        }

        private DialogueOperationResult ExecuteUnsupported(in DialogueActionContext context)
        {
            ResolveFallbackHandler(false);
            if (_fallbackActionHandler != null && !ReferenceEquals(_fallbackActionHandler, this))
            {
                return _fallbackActionHandler.Execute(in context);
            }

            if (passUnsupportedActions)
            {
                return Success(context);
            }

            return Fail(context, $"MiniGameDialogueActionHandler 不支持该行为类型：{context.Action.Type}");
        }

        private bool ResolveReferences(bool warn)
        {
            if (dialogueController == null && autoFindReferences)
            {
#if UNITY_2023_1_OR_NEWER
                dialogueController = FindFirstObjectByType<NiumaDialogueController>();
#else
                dialogueController = FindObjectOfType<NiumaDialogueController>();
#endif
            }

            ResolveSceneController(false);
            ResolveFallbackHandler(false);

            if (dialogueController == null)
            {
                LogWarning("未绑定 NiumaDialogueController，无法注册对话行为处理器。", warn);
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
                LogWarning("未绑定 NiumaSceneController，OpenMiniGame / LoadScene 行为无法执行。", warn);
            }

            return sceneController != null;
        }

        private void ResolveFallbackHandler(bool warn)
        {
            _fallbackActionHandler = fallbackActionHandlerProvider as IDialogueActionHandler;
            if (fallbackActionHandlerProvider != null && _fallbackActionHandler == null)
            {
                LogWarning("备用行为处理脚本绑定不正确，请拖任务、剧情、音频等 Dialogue 行为桥接脚本；没有其它行为时可留空。", warn);
            }
        }

        private string ResolveSceneName(DialogueActionData action, bool useMiniGameDefaults)
        {
            var customValue = GetCustomString(action, SceneNameKey);
            if (string.IsNullOrWhiteSpace(customValue))
            {
                customValue = GetCustomString(action, SceneKey);
            }

            if (!string.IsNullOrWhiteSpace(customValue))
            {
                return customValue;
            }

            if (!string.IsNullOrWhiteSpace(action.StringValue))
            {
                return action.StringValue;
            }

            if (!string.IsNullOrWhiteSpace(action.TargetId))
            {
                return action.TargetId;
            }

            return useMiniGameDefaults ? defaultMiniGameSceneName : null;
        }

        private string ResolveReturnSpawnPoint(DialogueActionData action)
        {
            if (!ResolveBool(action, PushReturnKey, pushReturnContext))
            {
                return null;
            }

            var customValue = GetCustomString(action, SpawnPointKey);
            return !string.IsNullOrWhiteSpace(customValue) ? customValue : defaultReturnSpawnPointId;
        }

        private SceneLoadPurpose ResolvePurpose(DialogueActionData action, bool useMiniGameDefaults)
        {
            var customValue = GetCustomString(action, PurposeKey);
            if (!string.IsNullOrWhiteSpace(customValue) &&
                System.Enum.TryParse(customValue, true, out SceneLoadPurpose parsedPurpose))
            {
                return parsedPurpose;
            }

            return useMiniGameDefaults ? SceneLoadPurpose.MiniGame : SceneLoadPurpose.None;
        }

        private bool ResolveBool(DialogueActionData action, string key, bool defaultValue)
        {
            var customValue = GetCustomString(action, key);
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return defaultValue;
            }

            if (bool.TryParse(customValue, out var result))
            {
                return result;
            }

            if (int.TryParse(customValue, out var intResult))
            {
                return intResult != 0;
            }

            return defaultValue;
        }

        private static string GetCustomString(DialogueActionData action, string key)
        {
            if (action?.CustomData == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (var i = 0; i < action.CustomData.Length; i++)
            {
                var entry = action.CustomData[i];
                if (entry != null && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static DialogueOperationResult Success(in DialogueActionContext context)
        {
            return DialogueOperationResult.Success(
                ResolveDialogueId(context.DialogueAsset),
                ResolveSentenceId(context.Sentence),
                context.Choice?.ChoiceId);
        }

        private DialogueOperationResult Fail(in DialogueActionContext context, string message)
        {
            LogWarning(message, true);
            return DialogueOperationResult.Fail(
                DialogueOperationFailureReason.ActionFailed,
                message,
                ResolveDialogueId(context.DialogueAsset),
                ResolveSentenceId(context.Sentence),
                context.Choice?.ChoiceId);
        }

        private static string ResolveDialogueId(DialogueAsset asset)
        {
            return asset != null ? asset.DialogueId : null;
        }

        private static string ResolveSentenceId(DialogueSentence sentence)
        {
            return sentence != null ? sentence.SentenceId : null;
        }

        private void LogWarning(string message, bool enabled)
        {
            if (enabled && logWarnings)
            {
                Debug.LogWarning($"[MiniGameDialogueActionHandler] {message}", this);
            }
        }
    }
}
