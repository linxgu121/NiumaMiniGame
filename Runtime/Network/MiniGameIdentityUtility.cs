using System;

namespace NiumaMiniGame.Network
{
    /// <summary>
    /// 第一版本地身份工具。
    /// 后续接账号系统后，应由认证服务提供可信 playerId。
    /// </summary>
    public static class MiniGameIdentityUtility
    {
        public const int DefaultDisplayNameMaxLength = 16;

        public static string CreateLocalPlayerId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string NormalizeDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "玩家";
            }

            var trimmed = displayName.Trim();
            return trimmed.Length <= DefaultDisplayNameMaxLength
                ? trimmed
                : trimmed.Substring(0, DefaultDisplayNameMaxLength);
        }
    }
}
