using System;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Protocol;

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
        public MiniGameErrorCode errorCode;
        public string roomId;
        public string udpBindToken;
        public int udpPort;
    }

    [Serializable]
    public sealed class JoinRoomRequest : IRealtimeMessage
    {
        public string roomId;
        public string displayName;
    }

    [Serializable]
    public sealed class JoinRoomResult : IRealtimeMessage
    {
        public bool succeeded;
        public MiniGameErrorCode errorCode;
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
        public MiniGameErrorCode errorCode;
        public string sessionId;
        public RoomSnapshot snapshot;
    }

    [Serializable]
    public sealed class RoomSnapshot : IRealtimeMessage
    {
        public string roomId;
        public string modeId;
        public MiniGameRoomState state;
        public int roundIndex;
        public int maxRoundCount;
        public string drawerPlayerId;
        public long stateEnterTimeMs;
        public long stateDeadlineTimeMs;
        public RoomPlayerSnapshot[] players;
        public ScoreEntry[] scores;
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
}
