using System;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Telephone
{
    [Serializable]
    public sealed class DrawTelephoneStarted : IRealtimeMessage
    {
        public int stageCount;
        public float drawStageSeconds;
        public float guessStageSeconds;
        public float reviewSecondsPerChain;
        public long roomSeed;
    }

    [Serializable]
    public sealed class DrawTelephoneStageStarted : IRealtimeMessage
    {
        public int stageIndex;
        public string actionType;
        public float stageDurationSeconds;
        public long deadlineTimeMs;
        public DrawTelephoneTask[] tasks;
    }

    [Serializable]
    public sealed class DrawTelephoneTask
    {
        public string chainId;
        public int stageIndex;
        public string actionType;
        public string promptWord;
        public string previousGuess;
        public string previousStrokeGroupId;
        public DrawTelephoneCanvasData previousCanvas;
    }

    [Serializable]
    public sealed class DrawTelephoneCanvasData
    {
        public string strokeGroupId;
        public DrawTelephoneStrokeData[] strokes;
    }

    [Serializable]
    public sealed class DrawTelephoneStrokeData
    {
        public string strokeId;

        /// <summary>
        /// 前端使用 Unity 友好的点位结构；真实后端适配层负责把服务端 float[][] 转为 DrawPointData[]。
        /// </summary>
        public DrawPointData[] points;
    }

    [Serializable]
    public sealed class DrawTelephoneStageEnded : IRealtimeMessage
    {
        public int stageIndex;
        public bool allSubmitted;
        public bool timeoutReached;
    }

    [Serializable]
    public sealed class DrawTelephoneReviewStarted : IRealtimeMessage
    {
        public DrawTelephoneChainState[] chains;
        public float reviewSecondsPerChain;
    }

    [Serializable]
    public sealed class DrawTelephoneChainReviewed : IRealtimeMessage
    {
        public DrawTelephoneChainState chain;
    }

    [Serializable]
    public sealed class DrawTelephoneChainState
    {
        public string chainId;
        public string originalWordId;
        public string starterPlayerId;
        public DrawTelephoneStageEntry[] entries;
        public string finalGuessText;
        public int score;
    }

    [Serializable]
    public sealed class DrawTelephoneStageEntry
    {
        public int stageIndex;
        public string playerId;
        public string actionType;
        public string strokeGroupId;
        public string guessText;
        public long submittedTimeMs;
        public bool isTimeoutSubmit;
    }

    [Serializable]
    public sealed class SubmitTelephoneDrawing : IRealtimeMessage
    {
        public string chainId;
        public string strokeGroupId;
    }

    [Serializable]
    public sealed class SubmitTelephoneGuess : IRealtimeMessage
    {
        public string chainId;
        public string guessText;
    }
}
