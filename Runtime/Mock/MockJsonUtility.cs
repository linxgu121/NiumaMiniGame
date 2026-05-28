using NiumaMiniGame.Protocol;
using UnityEngine;

namespace NiumaMiniGame.Mock
{
    /// <summary>
    /// Mock 阶段使用的 JSON 工具。
    /// 这里只负责调试 payload 文本，不作为正式 Netty 序列化实现。
    /// </summary>
    public static class MockJsonUtility
    {
        public static string ToJson<TMessage>(TMessage message) where TMessage : IRealtimeMessage
        {
            return message == null ? null : JsonUtility.ToJson(message);
        }
    }
}
