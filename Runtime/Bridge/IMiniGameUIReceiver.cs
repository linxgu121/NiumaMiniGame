namespace NiumaMiniGame.Bridge
{
    /// <summary>
    /// MiniGame UI 接收接口。
    /// Canvas、UI Toolkit 或调试面板只实现该接口，不直接依赖网络客户端。
    /// </summary>
    public interface IMiniGameUIReceiver
    {
        void ApplyMiniGameUpdate(MiniGameUIUpdate update);
    }
}
