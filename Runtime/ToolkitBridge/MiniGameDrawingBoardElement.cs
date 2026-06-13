using System;
using System.Collections.Generic;
using NiumaMiniGame.Drawing;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaMiniGame.ToolkitBridge
{
    /// <summary>
    /// MiniGame 画板 VisualElement。UXML 可直接使用该类型，也可以使用普通 VisualElement 并由 Binding 挂接输入适配器。
    /// </summary>
    public sealed class MiniGameDrawingBoardElement : VisualElement
    {
        public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<MiniGameDrawingBoardElement, UnityEngine.UIElements.VisualElement.UxmlTraits> { }

        public MiniGameDrawingBoardElement()
        {
            AddToClassList("niuma-minigame-drawing-board");
            focusable = true;
        }
    }

    internal sealed class MiniGameDrawingBoardInput : IDisposable
    {
        private readonly List<DrawPointData> _pendingPoints = new List<DrawPointData>(32);
        private VisualElement _target;
        private bool _isDrawing;
        private string _strokeId;
        private int _strokeSequence;
        private Color _color = Color.black;
        private float _brushSize = 6f;
        private int _batchPointLimit = 12;

        public event Action<string, Color, float> StrokeBegan;
        public event Action<StrokePointBatch> StrokeBatchReady;
        public event Action<string, int> StrokeEnded;
        public string CurrentStrokeId => _strokeId;

        public void Attach(VisualElement target)
        {
            Detach();
            _target = target;
            if (_target == null)
                return;
            _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        public void Configure(Color color, float brushSize, int batchPointLimit)
        {
            _color = color;
            _brushSize = Mathf.Max(0.001f, brushSize);
            _batchPointLimit = Mathf.Max(1, batchPointLimit);
        }

        public void CancelActiveStroke()
        {
            if (!_isDrawing)
                return;
            Flush();
            StrokeEnded?.Invoke(_strokeId, _strokeSequence);
            _isDrawing = false;
            _strokeId = null;
            _strokeSequence = 0;
            _pendingPoints.Clear();
        }

        public void Detach()
        {
            if (_target != null)
            {
                _target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            }
            _target = null;
            _isDrawing = false;
            _strokeId = null;
            _strokeSequence = 0;
            _pendingPoints.Clear();
        }

        public void Dispose()
        {
            Detach();
            StrokeBegan = null;
            StrokeBatchReady = null;
            StrokeEnded = null;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_target == null || evt.button != 0)
                return;
            _isDrawing = true;
            _strokeId = Guid.NewGuid().ToString("N");
            _strokeSequence = 0;
            _pendingPoints.Clear();
            _target.CapturePointer(evt.pointerId);
            StrokeBegan?.Invoke(_strokeId, _color, _brushSize);
            AddPoint(evt.localPosition, evt.pressure);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDrawing || _target == null)
                return;
            AddPoint(evt.localPosition, evt.pressure);
            if (_pendingPoints.Count >= _batchPointLimit)
                Flush();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDrawing || _target == null)
                return;
            AddPoint(evt.localPosition, evt.pressure);
            Flush();
            StrokeEnded?.Invoke(_strokeId, _strokeSequence);
            _target.ReleasePointer(evt.pointerId);
            _isDrawing = false;
            _strokeId = null;
            _strokeSequence = 0;
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            CancelActiveStroke();
        }

        private void AddPoint(Vector2 localPosition, float pressure)
        {
            var width = Mathf.Max(1f, _target.resolvedStyle.width);
            var height = Mathf.Max(1f, _target.resolvedStyle.height);
            _pendingPoints.Add(new DrawPointData
            {
                x = Mathf.Clamp01(localPosition.x / width),
                y = Mathf.Clamp01(localPosition.y / height),
                pressure = pressure > 0f ? pressure : 1f,
                timeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void Flush()
        {
            if (_pendingPoints.Count <= 0 || string.IsNullOrWhiteSpace(_strokeId))
                return;
            StrokeBatchReady?.Invoke(new StrokePointBatch
            {
                strokeId = _strokeId,
                strokeSequence = _strokeSequence++,
                points = _pendingPoints.ToArray()
            });
            _pendingPoints.Clear();
        }
    }
}
