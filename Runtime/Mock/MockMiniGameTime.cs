using System;

namespace NiumaMiniGame.Mock
{
    /// <summary>
    /// Mock 服务端时间工具。
    /// 使用 UTC 毫秒，便于与 Java 后端 System.currentTimeMillis 语义对齐。
    /// </summary>
    public static class MockMiniGameTime
    {
        public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
