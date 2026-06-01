using System;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.ViewData
{
    [Serializable]
    public sealed class MiniGamePanelViewData
    {
        public int Revision;
        public bool IsConnected;
        public bool IsLocalViewer;
        public string LocalPlayerId;
        public string SessionId;
        public string RoomId;
        public string LastMessageType;
        public MiniGameRoomViewData Room;
        public DrawTelephoneTaskViewData CurrentTask;
        public DrawTelephoneReviewViewData Review;
        public DrawTelephoneVotingViewData Voting;
        public DrawTelephoneVotingResultViewData VotingResult;
        public MiniGameChatViewData[] Chats;
        public MiniGameGiftViewData[] Gifts;
        public MiniGameErrorViewData LastError;
        public MiniGameToastViewData LastToast;
    }

    /// <summary>
    /// 房间面板表现数据。
    /// ViewData 不是网络协议，不要求字段名与后端 JSON 一致。
    /// </summary>
    [Serializable]
    public sealed class MiniGameRoomViewData
    {
        public string RoomId;
        public string ModeId;
        public string HostPlayerId;
        public MiniGameRoomState State;
        public int RoundIndex;
        public int MaxRoundCount;
        public string DrawerPlayerId;
        public float RemainingSeconds;
        public MiniGamePlayerViewData[] Players;
        public MiniGamePlayerViewData[] Viewers;
        public MiniGameScoreViewData[] Scores;
    }

    [Serializable]
    public sealed class MiniGamePlayerViewData
    {
        public string PlayerId;
        public string DisplayName;
        public bool IsReady;
        public bool IsConnected;
        public bool IsLocalPlayer;
        public bool IsHost;
        public bool IsViewer;
    }

    [Serializable]
    public sealed class MiniGameScoreViewData
    {
        public string PlayerId;
        public string DisplayName;
        public int Score;
        public int Rank;
    }

    [Serializable]
    public sealed class MiniGameChatViewData
    {
        public string PlayerId;
        public string DisplayName;
        public string Text;
        public long ServerTimeMs;
        public bool IsLocalPlayer;
    }

    [Serializable]
    public sealed class MiniGameGiftViewData
    {
        public string FromPlayerId;
        public string FromDisplayName;
        public string ToPlayerId;
        public string GiftType;
        public long ServerTimeMs;
        public bool IsFromLocalPlayer;
    }

    [Serializable]
    public sealed class MiniGameErrorViewData
    {
        public string ErrorCode;
        public string MessageKey;
        public string DebugMessage;
    }

    [Serializable]
    public sealed class MiniGameToastViewData
    {
        public string MessageKey;
        public string Text;
        public string SourcePlayerId;
        public long ServerTimeMs;
    }

    [Serializable]
    public sealed class DrawTelephoneTaskViewData
    {
        public string ChainId;
        public int StageIndex;
        public TelephoneActionType ActionType;
        public string PromptWord;
        public string PreviousGuess;
        public string PreviousStrokeGroupId;
        public DrawTelephoneCanvasViewData PreviousCanvas;
        public float RemainingSeconds;
    }

    [Serializable]
    public sealed class DrawTelephoneCanvasViewData
    {
        public string StrokeGroupId;
        public DrawTelephoneStrokeViewData[] Strokes;
    }

    [Serializable]
    public sealed class DrawTelephoneStrokeViewData
    {
        public string StrokeId;
        public DrawPointData[] Points;
    }

    [Serializable]
    public sealed class DrawTelephoneReviewViewData
    {
        public DrawTelephoneChainState[] Chains;
        public float ReviewSecondsPerChain;
    }

    [Serializable]
    public sealed class DrawTelephoneVotingViewData
    {
        public ChainVoteInfo[] Chains;
        public float RemainingSeconds;
    }

    [Serializable]
    public sealed class DrawTelephoneVotingResultViewData
    {
        public ChainVoteResult[] Results;
    }
}
