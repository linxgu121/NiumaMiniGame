using System.Collections.Generic;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.Mock
{
    internal sealed class MockRoomRuntime
    {
        public string RoomId;
        public string ModeId;
        public string State = nameof(MiniGameRoomState.Lobby);
        public int RoundIndex = 0;
        public int MaxRoundCount = 0;
        public string DrawerPlayerId = null;
        public long StateEnterTimeMs;
        public long StateDeadlineTimeMs;
        public int CurrentStageIndex = -1;
        public string CurrentActionType;
        public long RoomSeed;
        public readonly Dictionary<string, MockRoomPlayerRuntime> Players = new Dictionary<string, MockRoomPlayerRuntime>();
        public readonly Dictionary<string, DrawTelephoneTask> CurrentTasks = new Dictionary<string, DrawTelephoneTask>();
        public readonly Dictionary<string, bool> SubmittedPlayers = new Dictionary<string, bool>();
        public readonly List<MockDrawTelephoneChainRuntime> Chains = new List<MockDrawTelephoneChainRuntime>();
        public readonly Dictionary<string, MockStrokeGroupRuntime> StrokeGroups = new Dictionary<string, MockStrokeGroupRuntime>();
    }

    internal sealed class MockRoomPlayerRuntime
    {
        public string PlayerId;
        public string DisplayName;
        public bool Ready;
        public bool Connected;
        public int Score = 0;
    }

    internal sealed class MockServerSession
    {
        public string SessionId;
        public string PlayerId;
        public string DisplayName;
        public string RoomId;
        public bool Connected;
        public bool UdpBound;
        public MockRealtimeNetworkClient Client;
    }

    internal sealed class MockDrawTelephoneChainRuntime
    {
        public string ChainId;
        public string OriginalWordId;
        public string OriginalWordText;
        public string StarterPlayerId;
        public MockDrawTelephoneStageEntryRuntime[] Entries;
    }

    internal sealed class MockDrawTelephoneStageEntryRuntime
    {
        public int StageIndex;
        public string PlayerId;
        public string ActionType;
        public string StrokeGroupId;
        public string GuessText;
        public long SubmittedTimeMs;
        public bool IsTimeoutSubmit;
    }

    internal sealed class MockStrokeGroupRuntime
    {
        public string StrokeGroupId;
        public string PlayerId;
        public string ChainId;
        public int StageIndex;
        public readonly List<DrawTelephoneStrokeData> Strokes = new List<DrawTelephoneStrokeData>();
    }
}
