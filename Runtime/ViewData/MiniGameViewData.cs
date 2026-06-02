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
        public MiniGameGameplayViewData Gameplay;
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
        public MiniGamePlayerState PlayerState;
        public string PlayerStateText;
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
        public string TargetModule;
        public float NormalizedX;
        public float NormalizedY;
        public long ServerTimeMs;
        public bool IsFromLocalPlayer;
    }

    [Serializable]
    public sealed class MiniGameGameplayViewData
    {
        public string RoomId;
        public string ModeId;
        public MiniGameGameplayPhase Phase;
        public string LocalPlayerId;
        public MiniGamePlayerState LocalPlayerState;
        public string CurrentDrawerPlayerId;
        public string CurrentDrawerDisplayName;
        public string CurrentAnswererPlayerId;
        public string CurrentAnswererDisplayName;
        public string VisiblePromptText;
        public bool PromptIsOriginalWord;
        public string VisibleAnswerText;
        public string AnswererPlayerId;
        public string AnswererDisplayName;
        public string FinalOriginalWord;
        public string FinalGuessText;
        public string FinalAnswererPlayerId;
        public string FinalAnswererDisplayName;
        public float RemainingSeconds;
        public MiniGameGameplayAccessViewData Access;
        public MiniGamePlayerViewData[] Players;
        public MiniGameChatViewData[] Chats;
        public MiniGameGiftViewData[] Gifts;
        public MiniGameEvaluationViewData[] Evaluations;
    }

    [Serializable]
    public sealed class MiniGameGameplayAccessViewData
    {
        public MiniGameUIAccessState DrawingBoard;
        public MiniGameUIAccessState BrushTools;
        public MiniGameUIAccessState ColorPalette;
        public MiniGameUIAccessState Canvas;
        public MiniGameUIAccessState DrawerName;
        public MiniGameUIAccessState FinishButton;
        public MiniGameUIAccessState Chat;
        public MiniGameUIAccessState Answer;
        public MiniGameUIAccessState Menu;
        public MiniGameUIAccessState Topic;
        public MiniGameUIAccessState Timer;
        public MiniGameUIAccessState PlayerList;
        public MiniGameUIAccessState DrawPrompt;
        public MiniGameUIAccessState AnswerPrompt;
        public MiniGameUIAccessState Evaluation;
        public MiniGameUIAccessState AgreeButton;
        public MiniGameUIAccessState DisagreeButton;
        public MiniGameUIAccessState EvaluationList;
    }

    [Serializable]
    public sealed class MiniGameEvaluationViewData
    {
        public string PlayerId;
        public string DisplayName;
        public bool CanEvaluate;
        public bool HasEvaluated;
        public bool Agreed;
        public bool IsLocalPlayer;
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
