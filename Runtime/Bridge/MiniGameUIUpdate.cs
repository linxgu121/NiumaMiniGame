using NiumaMiniGame.ViewData;

namespace NiumaMiniGame.Bridge
{
    public enum MiniGameUIUpdateType
    {
        Refresh = 0,
        Cleared = 1
    }

    public readonly struct MiniGameUIUpdate
    {
        public readonly MiniGameUIUpdateType UpdateType;
        public readonly int Revision;
        public readonly MiniGamePanelViewData PanelData;
        public readonly MiniGamePanelViewData PreviousPanelData;

        public MiniGameUIUpdate(
            MiniGameUIUpdateType updateType,
            int revision,
            MiniGamePanelViewData panelData,
            MiniGamePanelViewData previousPanelData)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
        }
    }
}
