using NiumaMiniGame.Controller;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NiumaMiniGame.UIBridge
{
    /// <summary>
    /// 礼物拖拽按钮。拖到画布或答案区域时发送礼物消息，其他位置松手无效。
    /// </summary>
    public sealed class MiniGameGiftDragButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动在场景中查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("拖拽到该区域时，按画布目标发送礼物。")]
        [SerializeField] private RectTransform canvasTarget;

        [Tooltip("拖拽到该区域时，按答案目标发送礼物。")]
        [SerializeField] private RectTransform answerTarget;

        [Tooltip("拖拽过程中的可选视觉物体。为空时只发送逻辑，不显示拖拽影子。")]
        [SerializeField] private RectTransform dragVisual;

        [Tooltip("未手动绑定控制器时，是否自动查找场景中的 NiumaMiniGameController。")]
        [SerializeField] private bool autoFindController = true;

        [Header("礼物参数")]
        [Tooltip("礼物类型。建议使用 flower 或 egg，与后端协议保持一致。")]
        [SerializeField] private string giftType = "flower";

        [Tooltip("画布目标模块名，后端与 UI 表现层可用它区分落点。")]
        [SerializeField] private string canvasModuleName = "canvas";

        [Tooltip("答案目标模块名，后端与 UI 表现层可用它区分落点。")]
        [SerializeField] private string answerModuleName = "answer";

        [Tooltip("没有指定接收玩家时是否允许发送。房间礼物第一版主要面向模块落点，可不填 toPlayerId。")]
        [SerializeField] private bool allowEmptyTargetPlayer = true;

        private Canvas _rootCanvas;

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            if (dragVisual != null)
            {
                dragVisual.gameObject.SetActive(false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragVisual != null)
            {
                dragVisual.gameObject.SetActive(true);
                dragVisual.position = eventData.position;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragVisual == null)
            {
                return;
            }

            dragVisual.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragVisual != null)
            {
                dragVisual.gameObject.SetActive(false);
            }

            if (!EnsureController(false))
            {
                return;
            }

            if (TrySendToTarget(eventData, canvasTarget, canvasModuleName))
            {
                return;
            }

            TrySendToTarget(eventData, answerTarget, answerModuleName);
        }

        private bool TrySendToTarget(PointerEventData eventData, RectTransform target, string moduleName)
        {
            if (target == null || string.IsNullOrWhiteSpace(moduleName))
            {
                return false;
            }

            var camera = eventData.pressEventCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, camera))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, eventData.position, camera, out var localPoint))
            {
                return false;
            }

            var rect = target.rect;
            var x01 = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            var y01 = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
            var toPlayerId = allowEmptyTargetPlayer ? null : miniGameController.LocalPlayerId;
            miniGameController.SendGift(giftType, toPlayerId, moduleName, x01, y01);
            return true;
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
            if (miniGameController == null && warn)
            {
                Debug.LogWarning("[MiniGameGiftDragButton] 未找到 NiumaMiniGameController，无法发送礼物。", this);
            }

            return miniGameController != null;
        }
    }
}
