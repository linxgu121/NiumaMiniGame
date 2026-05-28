using System;
using NiumaMiniGame.Protocol;

namespace NiumaMiniGame.Room
{
    [Serializable]
    public sealed class SelectWordRequest : IRealtimeMessage
    {
        public string wordId;
    }

    [Serializable]
    public sealed class WordSelected : IRealtimeMessage
    {
        public string wordId;
        public string drawerPlayerId;
    }

    [Serializable]
    public sealed class SubmitGuessRequest : IRealtimeMessage
    {
        public string guessText;
    }

    [Serializable]
    public sealed class GuessResult : IRealtimeMessage
    {
        public string playerId;
        public bool correct;
        public int scoreGained;
    }
}
