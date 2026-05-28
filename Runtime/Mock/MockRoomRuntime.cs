using System.Collections.Generic;
using NiumaMiniGame.Enum;

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
        public readonly Dictionary<string, MockRoomPlayerRuntime> Players = new Dictionary<string, MockRoomPlayerRuntime>();
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
}
