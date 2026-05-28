namespace NiumaMiniGame.Protocol
{
    /// <summary>
    /// 前后端共享的消息类型常量。
    /// 字符串必须与 Java 后端 MessageType 保持一致。
    /// </summary>
    public static class MessageType
    {
        public const string ConnectRequest = "ConnectRequest";
        public const string ConnectAccepted = "ConnectAccepted";

        public const string CreateRoomRequest = "CreateRoomRequest";
        public const string CreateRoomResult = "CreateRoomResult";
        public const string JoinRoomRequest = "JoinRoomRequest";
        public const string JoinRoomResult = "JoinRoomResult";
        public const string LeaveRoomRequest = "LeaveRoomRequest";
        public const string RoomSnapshot = "RoomSnapshot";
        public const string PlayerJoined = "PlayerJoined";
        public const string PlayerLeft = "PlayerLeft";
        public const string PlayerReadyRequest = "PlayerReadyRequest";

        public const string RoundStarted = "RoundStarted";
        public const string WordOptionsPushed = "WordOptionsPushed";
        public const string SelectWordRequest = "SelectWordRequest";
        public const string WordSelected = "WordSelected";
        public const string SubmitGuessRequest = "SubmitGuessRequest";
        public const string GuessResult = "GuessResult";
        public const string ScoreChanged = "ScoreChanged";
        public const string RoundEnded = "RoundEnded";
        public const string GameEnded = "GameEnded";

        public const string StrokeBegin = "StrokeBegin";
        public const string StrokeEnd = "StrokeEnd";
        public const string CanvasCleared = "CanvasCleared";
        public const string UndoStrokeRequest = "UndoStrokeRequest";
        public const string StrokeUndone = "StrokeUndone";
        public const string StrokeCommitted = "StrokeCommitted";

        public const string DrawTelephoneStarted = "DrawTelephoneStarted";
        public const string DrawTelephoneStageStarted = "DrawTelephoneStageStarted";
        public const string SubmitTelephoneDrawing = "SubmitTelephoneDrawing";
        public const string SubmitTelephoneGuess = "SubmitTelephoneGuess";
        public const string DrawTelephoneStageEnded = "DrawTelephoneStageEnded";
        public const string DrawTelephoneReviewStarted = "DrawTelephoneReviewStarted";
        public const string DrawTelephoneChainReviewed = "DrawTelephoneChainReviewed";

        public const string SendRoomChatRequest = "SendRoomChatRequest";
        public const string RoomChatMessage = "RoomChatMessage";

        public const string SendRoomGiftRequest = "SendRoomGiftRequest";
        public const string RoomGiftSent = "RoomGiftSent";

        public const string ReconnectRequest = "ReconnectRequest";
        public const string ReconnectResult = "ReconnectResult";

        public const string UdpBindRequest = "UdpBindRequest";
        public const string UdpBindAccepted = "UdpBindAccepted";
        public const string StrokePointBatch = "StrokePointBatch";
        public const string CursorPreview = "CursorPreview";

        public const string Heartbeat = "Heartbeat";
        public const string ErrorMessage = "ErrorMessage";
        public const string ServerShutdown = "ServerShutdown";
    }
}
