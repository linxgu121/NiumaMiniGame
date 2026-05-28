using System;

namespace NiumaMiniGame.Mode
{
    /// <summary>
    /// 通用小游戏模式配置。
    /// 第一阶段只作为前端数据结构，真正配置来源后续由 Mock 或后端下发。
    /// </summary>
    [Serializable]
    public sealed class MiniGameModeConfig
    {
        public string modeId;
        public string displayName;
        public int minPlayers;
        public int maxPlayers;
        public int roundCount;
        public float selectWordSeconds;
        public float drawSeconds;
        public float roundSettlementSeconds;
        public bool allowLateJoin;
        public bool allowSpectator;
        public string ruleEvaluatorId;
    }

    /// <summary>
    /// 绘画传话模式配置。
    /// 与后端 DrawTelephone 配置语义保持一致。
    /// </summary>
    [Serializable]
    public sealed class DrawTelephoneModeConfig
    {
        public string modeId = "draw_telephone";
        public int minPlayers = 4;
        public int maxPlayers = 8;
        public bool requireEvenPlayerCount = true;
        public float drawStageSeconds = 60f;
        public float guessStageSeconds = 20f;
        public float reviewSecondsPerChain = 12f;
        public bool enableChatDuringReview = true;
        public bool enableGiftDuringReview = true;
    }
}
