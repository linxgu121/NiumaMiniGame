using System;
namespace NiumaMiniGame.Protocol
{
    [Serializable]
    public sealed class Heartbeat : IRealtimeMessage
    {
        public long clientTimeMs;
        public long serverTimeMs;
    }

    [Serializable]
    public sealed class ErrorMessage : IRealtimeMessage
    {
        public string requestId;
        public string errorCode;
        public string messageKey;
        public string debugMessage;
    }

    [Serializable]
    public sealed class ServerShutdown : IRealtimeMessage
    {
        public string reason;
        public long shutdownAtMs;
    }
}
