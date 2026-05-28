namespace NiumaMiniGame.Network
{
    /// <summary>
    /// 实时网络连接请求。
    /// 第一版 playerId 由客户端 UUID 生成，后续接账号后改为认证服务下发。
    /// </summary>
    public readonly struct RealtimeConnectRequest
    {
        public readonly string Host;
        public readonly int TcpPort;
        public readonly int UdpPort;
        public readonly string PlayerId;
        public readonly string DisplayName;
        public readonly bool EnableUdp;

        public RealtimeConnectRequest(
            string host,
            int tcpPort,
            int udpPort,
            string playerId,
            string displayName,
            bool enableUdp = true)
        {
            Host = host;
            TcpPort = tcpPort;
            UdpPort = udpPort;
            PlayerId = playerId;
            DisplayName = displayName;
            EnableUdp = enableUdp;
        }
    }
}
