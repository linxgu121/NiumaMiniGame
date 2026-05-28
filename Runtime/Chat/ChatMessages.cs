using System;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Chat
{
    [Serializable]
    public sealed class SendRoomChatRequest : IRealtimeMessage
    {
        public string text;
    }

    [Serializable]
    public sealed class RoomChatMessage : IRealtimeMessage
    {
        public string playerId;
        public string displayName;
        public string text;
        public long serverTimeMs;
    }
}
