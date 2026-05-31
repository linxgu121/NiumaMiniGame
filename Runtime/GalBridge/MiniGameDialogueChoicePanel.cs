using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NiumaMiniGame.GalBridge
{
    /// <summary>
    /// MiniGame 对话入口选项面板。
    /// 只负责显示两个按钮并转发点击事件，具体进入哪个场景由外部入口组件决定。
    /// </summary>
    public sealed class MiniGameDialogueChoicePanel : MonoBehaviour
    {
        [Header("面板节点")]
        [Tooltip("选项面板根节点。为空时使用当前 GameObject。")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("标题文本，例如“要进入你画我猜吗？”。")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("进入按钮文字。")]
        [SerializeField] private TMP_Text enterButtonText;

        [Tooltip("取消按钮文字。")]
        [SerializeField] private TMP_Text cancelButtonText;

        [Tooltip("进入你画我猜按钮。")]
        [SerializeField] private Button enterButton;

        [Tooltip("下次再说按钮。")]
        [SerializeField] private Button cancelButton;

        [Header("自动绑定")]
        [Tooltip("为 true 时，如果按钮未手动绑定，会从子物体中自动查找前两个 Button。建议正式场景仍手动绑定，避免层级变化后绑定错按钮。")]
        [SerializeField] private bool autoFindControls = true;

        [Tooltip("为 true 时，按钮缺失或场景缺少 EventSystem 会输出警告。")]
        [SerializeField] private bool logWarnings = true;

        [Header("鼠标控制")]
        [Tooltip("为 true 时，入口面板显示期间会临时显示并解锁鼠标，避免 TPC 隐藏鼠标导致按钮无法点击。")]
        [SerializeField] private bool unlockCursorWhileVisible = true;

        [Tooltip("为 true 时，面板关闭后恢复打开面板前的鼠标显示与锁定状态。")]
        [SerializeField] private bool restoreCursorOnHide = true;

        [Header("默认文案")]
        [Tooltip("面板默认标题。")]
        [SerializeField] private string defaultTitle = "要进入你画我猜吗？";

        [Tooltip("进入按钮默认文案。")]
        [SerializeField] private string defaultEnterText = "进入你画我猜";

        [Tooltip("取消按钮默认文案。")]
        [SerializeField] private string defaultCancelText = "下次再说";

        private readonly UnityEvent _onEnter = new UnityEvent();
        private readonly UnityEvent _onCancel = new UnityEvent();
        private CursorLockMode _previousLockMode;
        private bool _previousCursorVisible;
        private bool _hasCursorSnapshot;

        public UnityEvent OnEnter => _onEnter;
        public UnityEvent OnCancel => _onCancel;

        private void Awake()
        {
            EnsurePanelRoot();
            ResolveControls(false);
            BindButtons();
            Hide();
        }

        private void OnEnable()
        {
            // 面板如果一开始是隐藏物体，Awake 的绑定时机可能晚于外部 Show 调用。
            // 这里再次兜底绑定，保证按钮激活后一定有监听。
            EnsurePanelRoot();
            ResolveControls(false);
            BindButtons();
        }

        private void OnDestroy()
        {
            if (enterButton != null)
            {
                enterButton.onClick.RemoveListener(HandleEnterClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancelClicked);
            }
        }

        public void Show(string title, string enterText, string cancelText)
        {
            ApplyVisibleCursorState();
            EnsurePanelRoot();
            ResolveControls(true);
            BindButtons();
            WarnIfEventSystemMissing();

            SetText(titleText, string.IsNullOrWhiteSpace(title) ? defaultTitle : title);
            SetText(enterButtonText, string.IsNullOrWhiteSpace(enterText) ? defaultEnterText : enterText);
            SetText(cancelButtonText, string.IsNullOrWhiteSpace(cancelText) ? defaultCancelText : cancelText);

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            RestoreCursorState();
        }

        private void EnsurePanelRoot()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }
        }

        private void ResolveControls(bool warn)
        {
            if (!autoFindControls)
            {
                WarnMissingControls(warn);
                return;
            }

            if (enterButton == null || cancelButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                if (enterButton == null && buttons.Length > 0)
                {
                    enterButton = buttons[0];
                }

                if (cancelButton == null && buttons.Length > 1)
                {
                    cancelButton = buttons[1];
                }
            }

            WarnMissingControls(warn);
        }

        private void BindButtons()
        {
            if (enterButton != null)
            {
                enterButton.onClick.RemoveListener(HandleEnterClicked);
                enterButton.onClick.AddListener(HandleEnterClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancelClicked);
                cancelButton.onClick.AddListener(HandleCancelClicked);
            }
        }

        private void WarnMissingControls(bool warn)
        {
            if (!warn || !logWarnings)
            {
                return;
            }

            if (enterButton == null)
            {
                Debug.LogWarning("[MiniGameDialogueChoicePanel] 未绑定进入按钮，点击“进入你画我猜”不会生效。", this);
            }

            if (cancelButton == null)
            {
                Debug.LogWarning("[MiniGameDialogueChoicePanel] 未绑定取消按钮，点击“下次再说”不会生效。", this);
            }
        }

        private void WarnIfEventSystemMissing()
        {
            if (logWarnings && EventSystem.current == null)
            {
                Debug.LogWarning("[MiniGameDialogueChoicePanel] 当前场景没有 EventSystem，Unity UI Button 无法响应点击。请创建 UI/EventSystem。", this);
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
            if (!unlockCursorWhileVisible || !restoreCursorOnHide || !_hasCursorSnapshot)
            {
                return;
            }

            Cursor.lockState = _previousLockMode;
            Cursor.visible = _previousCursorVisible;
            _hasCursorSnapshot = false;
        }

        private void HandleEnterClicked()
        {
            Hide();
            _onEnter.Invoke();
        }

        private void HandleCancelClicked()
        {
            Hide();
            _onCancel.Invoke();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
