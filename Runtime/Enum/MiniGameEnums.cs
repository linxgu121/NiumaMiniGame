namespace NiumaMiniGame.Enum
{
    /// <summary>
    /// 后端返回的结构化错误码。
    /// 数值需要与 Java 后端 ErrorCode 保持一致。
    /// </summary>
    public enum MiniGameErrorCode
    {
        None = 0,
        NetworkDisconnected = 1,
        RoomNotFound = 2,
        RoomFull = 3,
        InvalidState = 4,
        InvalidPlayer = 5,
        PermissionDenied = 6,
        InvalidMessage = 7,
        RateLimited = 8,
        UdpNotBound = 9,
        ProtocolVersionMismatch = 10,
        ServerBusy = 11,
        ServerError = 99
    }

    /// <summary>
    /// 房间通用状态。
    /// 具体玩法阶段由 ModeRuleEvaluator 决定，枚举只承载跨模式公共状态。
    /// </summary>
    public enum MiniGameRoomState
    {
        None = 0,
        Lobby = 1,
        Preparing = 2,
        Playing = 3,
        Review = 4,
        Settlement = 5,
        Closed = 6,
        Voting = 7
    }

    /// <summary>
    /// 游戏中玩法阶段。
    /// 这里比 MiniGameRoomState 更细，用于 UI 判断当前应该展示题目、画板、回答还是评价。
    /// </summary>
    public enum MiniGameGameplayPhase
    {
        None = 0,
        Lobby = 1,
        Preparing = 2,
        TopicReveal = 3,
        Drawing = 4,
        Answering = 5,
        Review = 6,
        Voting = 7,
        Settlement = 8,
        Closed = 9
    }

    /// <summary>
    /// 顺序传画模式中的玩家局内状态。
    /// None 是默认无效值，UI 层不能把 None 当成 Waiting 使用。
    /// </summary>
    public enum MiniGamePlayerState
    {
        None = 0,
        Waiting = 1,
        Drawing = 2,
        Answering = 3,
        Done = 4,
        Spectating = 5
    }

    /// <summary>
    /// UI 模块可用性。
    /// Hidden=不可见，Display=可见但不可交互，Open=可见且可交互。
    /// </summary>
    public enum MiniGameUIAccessState
    {
        Hidden = 0,
        Display = 1,
        Open = 2
    }

    /// <summary>
    /// 绘画传话阶段动作类型。
    /// None 是默认无效值，业务层必须显式防御。
    /// </summary>
    public enum TelephoneActionType
    {
        None = 0,
        Draw = 1,
        Guess = 2
    }

    /// <summary>
    /// 实时消息可靠性。
    /// 第一版 Reliable 对应 TCP，Unreliable 对应 UDP。
    /// </summary>
    public enum RealtimeDeliveryMode
    {
        Reliable = 0,
        Unreliable = 1
    }
}
