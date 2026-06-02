using System;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Gift
{
    [Serializable]
    public sealed class SendRoomGiftRequest : IRealtimeMessage
    {
        public string giftType;
        public string toPlayerId;
        public string targetModule;
        public float normalizedX;
        public float normalizedY;
    }

    [Serializable]
    public sealed class RoomGiftSent : IRealtimeMessage
    {
        public string fromPlayerId;
        public string fromDisplayName;
        public string toPlayerId;
        public string giftType;
        public string targetModule;
        public float normalizedX;
        public float normalizedY;
        public long serverTimeMs;
    }
}
