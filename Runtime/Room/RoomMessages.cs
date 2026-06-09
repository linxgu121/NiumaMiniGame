using System;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.Room
{
    [Serializable]
    public sealed class ConnectRequest : IRealtimeMessage
    {
        public string playerId;
        public string displayName;
        public int protocolVersion = RealtimeMessageEnvelope.CurrentVersion;
    }

    [Serializable]
    public sealed class ConnectAccepted : IRealtimeMessage
    {
        public string sessionId;
        public string playerId;
        public long serverTimeMs;
    }

    [Serializable]
    public sealed class CreateRoomRequest : IRealtimeMessage
    {
        public string modeId;
        public string displayName;
    }

    [Serializable]
    public sealed class CreateRoomResult : IRealtimeMessage
    {
        public bool succeeded;
        public string errorCode;
        public string roomId;
        public string udpBindToken;
        public int udpPort;
    }

    [Serializable]
    public sealed class JoinRoomRequest : IRealtimeMessage
    {
        public string roomId;
        public string displayName;
        public bool viewer;
    }

    [Serializable]
    public sealed class JoinRoomResult : IRealtimeMessage
    {
        public bool succeeded;
        public string errorCode;
        public string roomId;
        public string udpBindToken;
        public int udpPort;
    }

    /// <summary>
    /// 离开房间请求。
    /// 后端第一阶段暂未定义字段，保留空消息体即可。
    /// </summary>
    [Serializable]
    public sealed class LeaveRoomRequest : IRealtimeMessage
    {
        public string roomId;
    }

    /// <summary>
    /// Ready 请求。
    /// ready=false 可用于后续取消准备；如果后端只支持准备，服务端可忽略 false。
    /// </summary>
    [Serializable]
    public sealed class PlayerReadyRequest : IRealtimeMessage
    {
        public bool ready = true;
    }

    [Serializable]
    public sealed class StartGameRequest : IRealtimeMessage
    {
        public string roomId;
    }

    [Serializable]
    public sealed class ChangeModeRequest : IRealtimeMessage
    {
        public string modeId;
    }

    /// <summary>
    /// 房间内身份切换请求。
    /// viewer=true 表示切到观战者；viewer=false 表示切到参赛玩家。
    /// </summary>
    [Serializable]
    public sealed class SwitchRoleRequest : IRealtimeMessage
    {
        public bool viewer;
    }

    [Serializable]
    public sealed class RoomToastMessage : IRealtimeMessage
    {
        public string messageKey;
        public string text;
        public string sourcePlayerId;
        public long serverTimeMs;
    }

    [Serializable]
    public sealed class ReconnectRequest : IRealtimeMessage
    {
        public string playerId;
        public string roomId;
        public string lastSessionId;
    }

    [Serializable]
    public sealed class ReconnectResult : IRealtimeMessage
    {
        public bool succeeded;
        public string errorCode;
        public string sessionId;
        public RoomSnapshot snapshot;
        public string udpBindToken;
        public int udpPort;
    }

    [Serializable]
    public sealed class RoomSnapshot : IRealtimeMessage
    {
        public string roomId;
        public string modeId;
        public string hostPlayerId;
        public string state;
        public int roundIndex;
        public int maxRoundCount;
        public string drawerPlayerId;
        public long stateEnterTimeMs;
        public long stateDeadlineTimeMs;
        public RoomPlayerSnapshot[] players;
        public RoomViewerSnapshot[] viewers;
        public ScoreEntry[] scores;
        public DrawTelephoneTask currentTask;
        public SequentialRelayStateSnapshot sequentialRelay;
    }

    [Serializable]
    public sealed class RoomPlayerSnapshot
    {
        public string playerId;
        public string displayName;
        public bool ready;
        public bool connected;
    }

    [Serializable]
    public sealed class RoomViewerSnapshot
    {
        public string playerId;
        public string displayName;
        public bool connected;
    }

    [Serializable]
    public sealed class ScoreEntry
    {
        public string playerId;
        public string displayName;
        public int score;
    }

    [Serializable]
    public sealed class ScoreChanged : IRealtimeMessage
    {
        public string playerId;
        public int totalScore;
        public int delta;
        public string reason;
    }

    [Serializable]
    public sealed class PlayerJoined : IRealtimeMessage
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public sealed class PlayerLeft : IRealtimeMessage
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public sealed class RoundStarted : IRealtimeMessage
    {
        public int roundIndex;
        public int maxRoundCount;
        public string drawerPlayerId;
        public float drawDurationSeconds;
        public long deadlineTimeMs;
    }

    [Serializable]
    public sealed class RoundEnded : IRealtimeMessage
    {
        public int roundIndex;
        public string wordId;
        public string wordText;
        public ScoreEntry[] scores;
    }

    [Serializable]
    public sealed class GameEnded : IRealtimeMessage
    {
        public ScoreEntry[] finalScores;
        public string winnerPlayerId;
    }

    [Serializable]
    public sealed class WordOptionsPushed : IRealtimeMessage
    {
        public WordOption[] options;
    }

    [Serializable]
    public sealed class WordOption
    {
        public string wordId;
        public string wordText;
    }

    /// <summary>
    /// 顺序传画模式当前状态快照。
    /// 服务端权威维护，前端只据此切换游戏中 UI。
    /// </summary>
    [Serializable]
    public sealed class SequentialRelayStateSnapshot : IRealtimeMessage
    {
        public string phase;
        public string currentDrawerPlayerId;
        public string currentDrawerDisplayName;
        public string currentAnswererPlayerId;
        public string currentAnswererDisplayName;
        public string promptText;
        public bool promptIsOriginalWord;
        public string visibleAnswerText;
        public string answererPlayerId;
        public string answererDisplayName;
        public string originalWord;
        public string finalGuessText;
        public string finalAnswererPlayerId;
        public string finalAnswererDisplayName;
        public string defaultEmptyAnswerText = "未作答";
        public long deadlineTimeMs;
        public SequentialRelayPlayerStateSnapshot[] playerStates;
        public SequentialRelayEvaluationSnapshot[] evaluations;
    }

    /// <summary>
    /// 顺序传画模式中的玩家状态项。
    /// state 使用 Waiting / Drawing / Answering / Done / Spectating。
    /// </summary>
    [Serializable]
    public sealed class SequentialRelayPlayerStateSnapshot
    {
        public string playerId;
        public string displayName;
        public string state;
    }

    /// <summary>
    /// 顺序传画结算评价项。
    /// agreed 仅在 hasEvaluated=true 时有意义。
    /// </summary>
    [Serializable]
    public sealed class SequentialRelayEvaluationSnapshot
    {
        public string playerId;
        public string displayName;
        public bool canEvaluate;
        public bool hasEvaluated;
        public bool agreed;
    }

    /// <summary>
    /// 顺序传画状态变更广播。
    /// 后端可在阶段切换、提交答案、评价变化时发送该消息。
    /// </summary>
    [Serializable]
    public sealed class SequentialRelayStateChanged : IRealtimeMessage
    {
        public SequentialRelayStateSnapshot snapshot;
    }

    /// <summary>
    /// 顺序传画作画完成请求。
    /// strokeGroupId 对应当前画布笔画组，由绘画协议生成。
    /// </summary>
    [Serializable]
    public sealed class SubmitSequentialDrawing : IRealtimeMessage
    {
        public string strokeGroupId;
    }

    /// <summary>
    /// 顺序传画回答提交请求。
    /// answerText 为空时后端使用默认未作答文本。
    /// </summary>
    [Serializable]
    public sealed class SubmitSequentialAnswer : IRealtimeMessage
    {
        public string answerText;
    }

    /// <summary>
    /// 顺序传画结算评价请求。
    /// 第一版只做赞同 / 不赞同，不做 0-100 打分。
    /// </summary>
    [Serializable]
    public sealed class SubmitSequentialEvaluation : IRealtimeMessage
    {
        public bool agreed;
    }
}
