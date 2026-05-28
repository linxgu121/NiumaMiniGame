using System;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.ViewData
{
    /// <summary>
    /// 房间面板表现数据。
    /// ViewData 不是网络协议，不要求字段名与后端 JSON 一致。
    /// </summary>
    [Serializable]
    public sealed class MiniGameRoomViewData
    {
        public string RoomId;
        public string ModeId;
        public MiniGameRoomState State;
        public int RoundIndex;
        public int MaxRoundCount;
        public string DrawerPlayerId;
        public float RemainingSeconds;
        public MiniGamePlayerViewData[] Players;
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
    public sealed class DrawTelephoneTaskViewData
    {
        public string ChainId;
        public int StageIndex;
        public TelephoneActionType ActionType;
        public string PromptWord;
        public string PreviousGuess;
        public string PreviousStrokeGroupId;
        public float RemainingSeconds;
    }

    [Serializable]
    public sealed class DrawTelephoneReviewViewData
    {
        public DrawTelephoneChainState[] Chains;
        public float ReviewSecondsPerChain;
    }
}
