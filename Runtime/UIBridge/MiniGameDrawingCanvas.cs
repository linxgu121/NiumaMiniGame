using System;
using System.Collections.Generic;
using NiumaMiniGame.Controller;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.ViewData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NiumaMiniGame.UIBridge
{
    /// <summary>
    /// 你画我猜游戏中的基础画布。
    /// 第一版负责本地绘制、撤销/还原/清空，并把点位批量发送给 MiniGameController。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class MiniGameDrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private enum DrawingTool
        {
            Pencil = 0,
            Line = 1,
            Eraser = 2
        }

        private sealed class CanvasStroke
        {
            public string StrokeId;
            public Color32 Color;
            public int Radius;
            public readonly List<Vector2Int> Pixels = new List<Vector2Int>(64);
        }

        [Header("核心引用")]
        [Tooltip("MiniGame 根控制器。为空时会自动在场景中查找。")]
        [SerializeField] private NiumaMiniGameController miniGameController;

        [Tooltip("用于显示画布贴图的 RawImage。为空时会使用当前物体上的 RawImage。")]
        [SerializeField] private RawImage canvasImage;

        [Tooltip("未手动绑定控制器时，是否自动查找场景中的 NiumaMiniGameController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindController = true;

        [Header("画布参数")]
        [Tooltip("画布贴图宽度。数值越大越清晰，但绘制和撤销成本越高。")]
        [SerializeField] private int textureWidth = 1024;

        [Tooltip("画布贴图高度。数值越大越清晰，但绘制和撤销成本越高。")]
        [SerializeField] private int textureHeight = 768;

        [Tooltip("画布底色。你画我猜第一版默认白底。")]
        [SerializeField] private Color backgroundColor = Color.white;

        [Tooltip("最小笔刷像素半径。")]
        [SerializeField] private int minBrushRadius = 2;

        [Tooltip("最大笔刷像素半径。")]
        [SerializeField] private int maxBrushRadius = 32;

        [Tooltip("默认笔刷颜色。")]
        [SerializeField] private Color brushColor = Color.black;

        [Tooltip("默认笔刷粗细，范围 0-1，会映射到最小/最大笔刷半径。")]
        [Range(0f, 1f)]
        [SerializeField] private float brushSize01 = 0.25f;

        [Header("网络发送")]
        [Tooltip("绘制时是否向 MiniGameController 发送笔画消息。关闭后只做本地画布预览。")]
        [SerializeField] private bool sendNetworkStrokes = true;

        [Tooltip("点位批量消息是否强制走可靠通道。默认 false，优先走 UDP/不可靠通道。")]
        [SerializeField] private bool reliablePointFallback;

        [Tooltip("每批最多发送多少个点位。需要与后端 UDP 包大小限制一起控制。")]
        [SerializeField] private int maxPointsPerBatch = 16;

        [Tooltip("是否输出画布警告日志。")]
        [SerializeField] private bool logWarnings = true;

        private readonly List<CanvasStroke> _strokes = new List<CanvasStroke>(64);
        private readonly Stack<CanvasStroke> _redoStack = new Stack<CanvasStroke>(16);
        private readonly List<DrawPointData> _pendingNetworkPoints = new List<DrawPointData>(32);

        private Texture2D _texture;
        private DrawingTool _tool;
        private CanvasStroke _activeStroke;
        private Vector2Int _lastPixel;
        private Vector2Int _lineStartPixel;
        private Color32[] _linePreviewPixels;
        private int _strokeSequence;
        private bool _isDrawing;
        private bool _isInteractable = true;
        private string _strokeGroupId;

        public string CurrentStrokeGroupId
        {
            get
            {
                EnsureStrokeGroupId();
                return _strokeGroupId;
            }
        }

        public bool HasLocalContent => _strokes.Count > 0;

        private void Awake()
        {
            EnsureTexture();
            EnsureStrokeGroupId();
        }

        private void OnEnable()
        {
            EnsureTexture();
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInteractable || !TryScreenToPixel(eventData, out var pixel, out var normalized))
            {
                return;
            }

            EnsureController(false);
            BeginStroke(pixel, normalized);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDrawing || !_isInteractable || !TryScreenToPixel(eventData, out var pixel, out var normalized))
            {
                return;
            }

            AddPoint(pixel, normalized, false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDrawing)
            {
                return;
            }

            if (TryScreenToPixel(eventData, out var pixel, out var normalized))
            {
                AddPoint(pixel, normalized, true);
            }

            EndStroke();
        }

        /// <summary>
        /// 设置画布是否允许玩家输入。UI 桥接层根据当前玩家状态调用。
        /// </summary>
        public void SetCanvasInteractable(bool interactable)
        {
            _isInteractable = interactable;
            if (!interactable && _isDrawing)
            {
                EndStroke();
            }
        }

        public void SelectPencilTool()
        {
            _tool = DrawingTool.Pencil;
        }

        public void SelectLineTool()
        {
            _tool = DrawingTool.Line;
        }

        public void SelectEraserTool()
        {
            _tool = DrawingTool.Eraser;
        }

        public void SetBrushSize01(float value)
        {
            brushSize01 = Mathf.Clamp01(value);
        }

        public void SetBrushColor(Color color)
        {
            brushColor = color;
            if (_tool == DrawingTool.Eraser)
            {
                _tool = DrawingTool.Pencil;
            }
        }

        public void SetBlack() => SetBrushColor(Color.black);
        public void SetRed() => SetBrushColor(Color.red);
        public void SetYellow() => SetBrushColor(Color.yellow);
        public void SetBlue() => SetBrushColor(Color.blue);
        public void SetGreen() => SetBrushColor(Color.green);
        public void SetPurple() => SetBrushColor(new Color(0.45f, 0.12f, 0.85f, 1f));

        /// <summary>
        /// 撤销上一笔。会同步发送 UndoStrokeRequest，后端可决定是否接受。
        /// </summary>
        public void Undo()
        {
            if (_strokes.Count == 0)
            {
                return;
            }

            var lastIndex = _strokes.Count - 1;
            var stroke = _strokes[lastIndex];
            _strokes.RemoveAt(lastIndex);
            _redoStack.Push(stroke);
            RepaintFromStrokes();

            if (sendNetworkStrokes && !string.IsNullOrWhiteSpace(stroke.StrokeId))
            {
                EnsureController(false);
                miniGameController?.RequestUndoStroke(stroke.StrokeId);
            }
        }

        public void Redo()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            var stroke = _redoStack.Pop();
            _strokes.Add(stroke);
            RepaintFromStrokes();
        }

        /// <summary>
        /// 清空当前画布，并生成新的 strokeGroupId，避免后端把新旧画面混在一起。
        /// </summary>
        public void Clear()
        {
            _strokes.Clear();
            _redoStack.Clear();
            _pendingNetworkPoints.Clear();
            ResetStrokeGroupId();
            FillTexture();

            if (sendNetworkStrokes)
            {
                EnsureController(false);
                miniGameController?.RequestClearCanvas();
            }
        }

        /// <summary>
        /// 显示上一位玩家传来的画布。协议第一版的历史画布只带点位，不带颜色和笔刷，先按黑色细线回放。
        /// </summary>
        public void LoadReadonlyCanvas(DrawTelephoneCanvasViewData canvas)
        {
            _strokes.Clear();
            _redoStack.Clear();
            FillTexture();

            if (canvas?.Strokes == null)
            {
                return;
            }

            for (var i = 0; i < canvas.Strokes.Length; i++)
            {
                var sourceStroke = canvas.Strokes[i];
                if (sourceStroke?.Points == null || sourceStroke.Points.Length == 0)
                {
                    continue;
                }

                var stroke = new CanvasStroke
                {
                    StrokeId = sourceStroke.StrokeId,
                    Color = Color.black,
                    Radius = Mathf.Max(1, minBrushRadius)
                };

                for (var pointIndex = 0; pointIndex < sourceStroke.Points.Length; pointIndex++)
                {
                    var point = sourceStroke.Points[pointIndex];
                    var pixel = NormalizedToPixel(point.x, point.y);
                    stroke.Pixels.Add(pixel);
                    if (stroke.Pixels.Count > 1)
                    {
                        DrawSegment(stroke.Pixels[stroke.Pixels.Count - 2], pixel, stroke.Color, stroke.Radius);
                    }
                    else
                    {
                        DrawCircle(pixel, stroke.Color, stroke.Radius);
                    }
                }

                _strokes.Add(stroke);
            }

            _texture.Apply(false);
        }

        private void BeginStroke(Vector2Int pixel, Vector2 normalized)
        {
            EnsureTexture();

            _activeStroke = new CanvasStroke
            {
                StrokeId = CreateStrokeId(),
                Color = _tool == DrawingTool.Eraser ? (Color32)backgroundColor : (Color32)brushColor,
                Radius = CurrentBrushRadius()
            };
            _activeStroke.Pixels.Add(pixel);
            _isDrawing = true;
            _lastPixel = pixel;
            _lineStartPixel = pixel;
            _pendingNetworkPoints.Clear();
            _strokeSequence = 0;

            if (_tool == DrawingTool.Line)
            {
                _linePreviewPixels = _texture.GetPixels32();
            }
            else
            {
                DrawCircle(pixel, _activeStroke.Color, _activeStroke.Radius);
                _texture.Apply(false);
            }

            if (sendNetworkStrokes)
            {
                miniGameController?.SendStrokeBegin(_activeStroke.StrokeId, _activeStroke.Color, _activeStroke.Radius);
                AddNetworkPoint(normalized);
            }
        }

        private void AddPoint(Vector2Int pixel, Vector2 normalized, bool force)
        {
            if (_activeStroke == null)
            {
                return;
            }

            if (!force && pixel == _lastPixel)
            {
                return;
            }

            if (_tool == DrawingTool.Line)
            {
                RestoreLinePreview();
                DrawSegment(_lineStartPixel, pixel, _activeStroke.Color, _activeStroke.Radius);
                _texture.Apply(false);

                if (_activeStroke.Pixels.Count == 1)
                {
                    _activeStroke.Pixels.Add(pixel);
                }
                else
                {
                    _activeStroke.Pixels[_activeStroke.Pixels.Count - 1] = pixel;
                }
            }
            else
            {
                DrawSegment(_lastPixel, pixel, _activeStroke.Color, _activeStroke.Radius);
                _texture.Apply(false);
                _activeStroke.Pixels.Add(pixel);
            }

            _lastPixel = pixel;
            if (sendNetworkStrokes)
            {
                AddNetworkPoint(normalized);
            }
        }

        private void EndStroke()
        {
            if (_activeStroke == null)
            {
                _isDrawing = false;
                return;
            }

            if (_tool == DrawingTool.Line && _activeStroke.Pixels.Count == 1)
            {
                DrawCircle(_activeStroke.Pixels[0], _activeStroke.Color, _activeStroke.Radius);
                _texture.Apply(false);
            }

            _strokes.Add(_activeStroke);
            _redoStack.Clear();

            if (sendNetworkStrokes)
            {
                FlushNetworkPoints();
                miniGameController?.SendStrokeEnd(_activeStroke.StrokeId, _activeStroke.Pixels.Count);
            }

            _activeStroke = null;
            _linePreviewPixels = null;
            _isDrawing = false;
        }

        private void AddNetworkPoint(Vector2 normalized)
        {
            _pendingNetworkPoints.Add(new DrawPointData
            {
                x = Mathf.Clamp01(normalized.x),
                y = Mathf.Clamp01(normalized.y),
                pressure = 1f,
                timeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            if (_pendingNetworkPoints.Count >= Mathf.Max(1, maxPointsPerBatch))
            {
                FlushNetworkPoints();
            }
        }

        private void FlushNetworkPoints()
        {
            if (_activeStroke == null || _pendingNetworkPoints.Count == 0)
            {
                return;
            }

            var points = new DrawPointData[_pendingNetworkPoints.Count];
            for (var i = 0; i < _pendingNetworkPoints.Count; i++)
            {
                points[i] = _pendingNetworkPoints[i];
            }

            miniGameController?.SendStrokePointBatch(new StrokePointBatch
            {
                strokeId = _activeStroke.StrokeId,
                strokeSequence = _strokeSequence++,
                points = points
            }, reliablePointFallback);
            _pendingNetworkPoints.Clear();
        }

        private void RepaintFromStrokes()
        {
            FillTexture(false);
            for (var i = 0; i < _strokes.Count; i++)
            {
                var stroke = _strokes[i];
                if (stroke == null || stroke.Pixels.Count == 0)
                {
                    continue;
                }

                DrawCircle(stroke.Pixels[0], stroke.Color, stroke.Radius);
                for (var p = 1; p < stroke.Pixels.Count; p++)
                {
                    DrawSegment(stroke.Pixels[p - 1], stroke.Pixels[p], stroke.Color, stroke.Radius);
                }
            }

            _texture.Apply(false);
        }

        private void DrawSegment(Vector2Int from, Vector2Int to, Color32 color, int radius)
        {
            var dx = Mathf.Abs(to.x - from.x);
            var dy = Mathf.Abs(to.y - from.y);
            var steps = Mathf.Max(dx, dy, 1);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var point = new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)));
                DrawCircle(point, color, radius);
            }
        }

        private void DrawCircle(Vector2Int center, Color32 color, int radius)
        {
            var radiusSquared = radius * radius;
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > radiusSquared)
                    {
                        continue;
                    }

                    var px = center.x + x;
                    var py = center.y + y;
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        _texture.SetPixel(px, py, color);
                    }
                }
            }
        }

        private bool TryScreenToPixel(PointerEventData eventData, out Vector2Int pixel, out Vector2 normalized)
        {
            pixel = default;
            normalized = default;
            if (canvasImage == null)
            {
                return false;
            }

            var rectTransform = canvasImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var local))
            {
                return false;
            }

            var rect = rectTransform.rect;
            var x01 = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            var y01 = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            if (x01 < 0f || x01 > 1f || y01 < 0f || y01 > 1f)
            {
                return false;
            }

            normalized = new Vector2(x01, y01);
            pixel = NormalizedToPixel(x01, y01);
            return true;
        }

        private Vector2Int NormalizedToPixel(float x01, float y01)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(x01 * (textureWidth - 1)), 0, textureWidth - 1),
                Mathf.Clamp(Mathf.RoundToInt(y01 * (textureHeight - 1)), 0, textureHeight - 1));
        }

        private int CurrentBrushRadius()
        {
            return Mathf.RoundToInt(Mathf.Lerp(
                Mathf.Max(1, minBrushRadius),
                Mathf.Max(minBrushRadius, maxBrushRadius),
                Mathf.Clamp01(brushSize01)));
        }

        private void EnsureTexture()
        {
            if (canvasImage == null)
            {
                canvasImage = GetComponent<RawImage>();
            }

            textureWidth = Mathf.Max(64, textureWidth);
            textureHeight = Mathf.Max(64, textureHeight);
            if (_texture != null && _texture.width == textureWidth && _texture.height == textureHeight)
            {
                return;
            }

            if (_texture != null)
            {
                Destroy(_texture);
            }

            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "NiumaMiniGameDrawingCanvas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (canvasImage != null)
            {
                canvasImage.texture = _texture;
            }

            FillTexture();
        }

        private void FillTexture(bool apply = true)
        {
            EnsureTextureReferenceOnly();
            var pixels = new Color32[textureWidth * textureHeight];
            var color = (Color32)backgroundColor;
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            _texture.SetPixels32(pixels);
            if (apply)
            {
                _texture.Apply(false);
            }
        }

        private void EnsureTextureReferenceOnly()
        {
            if (_texture == null && logWarnings)
            {
                Debug.LogWarning("[MiniGameDrawingCanvas] 画布贴图尚未初始化，正在重新创建。", this);
            }

            if (_texture == null)
            {
                EnsureTexture();
            }
        }

        private void RestoreLinePreview()
        {
            if (_linePreviewPixels == null)
            {
                return;
            }

            _texture.SetPixels32(_linePreviewPixels);
        }

        private void EnsureStrokeGroupId()
        {
            if (string.IsNullOrWhiteSpace(_strokeGroupId))
            {
                ResetStrokeGroupId();
            }
        }

        private void ResetStrokeGroupId()
        {
            var playerPart = miniGameController != null && !string.IsNullOrWhiteSpace(miniGameController.LocalPlayerId)
                ? miniGameController.LocalPlayerId
                : "local";
            _strokeGroupId = $"{playerPart}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
        }

        private string CreateStrokeId()
        {
            EnsureStrokeGroupId();
            return $"{_strokeGroupId}_stroke_{Guid.NewGuid():N}";
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
                Debug.LogWarning("[MiniGameDrawingCanvas] 未找到 NiumaMiniGameController，画布只能本地预览，无法同步到房间。", this);
            }

            return miniGameController != null;
        }
    }
}
