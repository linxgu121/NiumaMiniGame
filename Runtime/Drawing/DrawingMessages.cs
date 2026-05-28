using System;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Drawing
{
    /// <summary>
    /// 归一化绘画点位，坐标范围为 0-1。
    /// </summary>
    [Serializable]
    public sealed class DrawPointData
    {
        public float x;
        public float y;
        public float pressure;
        public long timeMs;
    }

    /// <summary>
    /// 笔画开始消息，必须走可靠通道。
    /// </summary>
    [Serializable]
    public sealed class StrokeBegin : IRealtimeMessage
    {
        public string strokeId;
        public float colorR;
        public float colorG;
        public float colorB;
        public float brushSize;
    }

    /// <summary>
    /// 笔画结束消息，必须走可靠通道。
    /// </summary>
    [Serializable]
    public sealed class StrokeEnd : IRealtimeMessage
    {
        public string strokeId;
        public int totalPoints;
    }

    /// <summary>
    /// 高频点位批量消息。
    /// 第一版优先走 UDP，UDP 不可用时可降频走 TCP。
    /// </summary>
    [Serializable]
    public sealed class StrokePointBatch : IRealtimeMessage
    {
        public string roomId;
        public string strokeId;
        public int strokeSequence;
        public int batchIndex;
        public int batchCount;
        public DrawPointData[] points;
    }

    [Serializable]
    public sealed class CursorPreview : IRealtimeMessage
    {
        public string playerId;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class CanvasCleared : IRealtimeMessage
    {
        public string roomId;
        public long clearedAtMs;
    }

    [Serializable]
    public sealed class UdpBindRequest : IRealtimeMessage
    {
        public string roomId;
        public string playerId;
        public string sessionId;
        public string udpBindToken;
    }

    [Serializable]
    public sealed class UdpBindAccepted : IRealtimeMessage
    {
        public bool succeeded;
        public MiniGameErrorCode errorCode;
    }
}
