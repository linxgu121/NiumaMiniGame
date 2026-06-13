using NiumaMiniGame.Bridge;
using NiumaMiniGame.ViewData;

namespace NiumaMiniGame.ToolkitBridge
{
    public readonly struct MiniGameStartUIUpdate
    {
        public readonly MiniGameUIUpdateType UpdateType;
        public readonly int Revision;
        public readonly MiniGamePanelViewData PanelData;
        public readonly MiniGamePanelViewData PreviousPanelData;

        public MiniGameStartUIUpdate(MiniGameUIUpdate update)
        {
            UpdateType = update.UpdateType;
            Revision = update.Revision;
            PanelData = update.PanelData;
            PreviousPanelData = update.PreviousPanelData;
        }
    }

    public readonly struct MiniGamePlayUIUpdate
    {
        public readonly MiniGameUIUpdateType UpdateType;
        public readonly int Revision;
        public readonly MiniGamePanelViewData PanelData;
        public readonly MiniGamePanelViewData PreviousPanelData;
        public readonly MiniGameGameplayViewData Gameplay;
        public readonly MiniGameGameplayViewData PreviousGameplay;

        public MiniGamePlayUIUpdate(MiniGameUIUpdate update)
        {
            UpdateType = update.UpdateType;
            Revision = update.Revision;
            PanelData = update.PanelData;
            PreviousPanelData = update.PreviousPanelData;
            Gameplay = update.PanelData?.Gameplay;
            PreviousGameplay = update.PreviousPanelData?.Gameplay;
        }
    }
}
