namespace NiumaMiniGame.Enum
{
    /// <summary>
    /// 后端返回的结构化错误码。
    /// 数值必须与 Java 后端 ErrorCode 保持一致。
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
    /// 具体玩法阶段由 ModeRuleEvaluator 决定，不在这里膨胀。
    /// </summary>
    public enum MiniGameRoomState
    {
        None = 0,
        Lobby = 1,
        Preparing = 2,
        Playing = 3,
        Review = 4,
        Settlement = 5,
        Closed = 6
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
