using System;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Mock;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using UnityEngine;

namespace NiumaMiniGame.Debugging
{
    /// <summary>
    /// MiniGame 第二阶段 Mock 流程测试。
    /// 挂到任意 GameObject 后，通过右键菜单运行。
    /// </summary>
    public sealed class MiniGameMockTestRunner : MonoBehaviour
    {
        [Tooltip("测试运行完毕后是否输出详细通过日志。")]
        [SerializeField] private bool logPassedChecks = true;

        [ContextMenu("NiumaMiniGame/阶段2/运行 Mock 基础测试")]
        public void RunMockBasicTests()
        {
            var failed = 0;
            var passed = 0;
            MockRoomServer.Shared.Reset();

            var host = CreateClient("player-a", "甲");
            var guest = CreateClient("player-b", "乙");

            Expect(host.IsConnected, "房主连接成功", ref passed, ref failed);
            Expect(guest.IsConnected, "成员连接成功", ref passed, ref failed);
            Expect(DrainUntil<ConnectAccepted>(host, MessageType.ConnectAccepted, out _), "房主收到 ConnectAccepted", ref passed, ref failed);
            Expect(DrainUntil<ConnectAccepted>(guest, MessageType.ConnectAccepted, out _), "成员收到 ConnectAccepted", ref passed, ref failed);

            host.SendReliable(new CreateRoomRequest
            {
                modeId = "draw_telephone",
                displayName = "甲"
            });

            Expect(DrainUntil<CreateRoomResult>(host, MessageType.CreateRoomResult, out var createResult), "房主收到建房结果", ref passed, ref failed);
            Expect(createResult != null && createResult.succeeded, "建房成功", ref passed, ref failed);

            var roomId = createResult?.roomId;
            guest.SendReliable(new JoinRoomRequest
            {
                roomId = roomId,
                displayName = "乙"
            });

            Expect(DrainUntil<JoinRoomResult>(guest, MessageType.JoinRoomResult, out var joinResult), "成员收到加房结果", ref passed, ref failed);
            Expect(joinResult != null && joinResult.succeeded, "加房成功", ref passed, ref failed);

            host.SendReliable(new PlayerReadyRequest { ready = true });
            guest.SendReliable(new PlayerReadyRequest { ready = true });

            DrainAll(host);
            Expect(DrainUntil<RoomSnapshot>(guest, MessageType.RoomSnapshot, out var snapshot), "成员收到房间快照", ref passed, ref failed);
            Expect(snapshot != null && snapshot.players != null && snapshot.players.Length == 2, "房间快照包含两名玩家", ref passed, ref failed);

            guest.SendReliable(new SendRoomChatRequest { text = "测试聊天" });
            Expect(DrainUntil<RoomChatMessage>(host, MessageType.RoomChatMessage, out var chat), "房主收到聊天广播", ref passed, ref failed);
            Expect(chat != null && chat.text == "测试聊天", "聊天内容正确", ref passed, ref failed);

            host.SendReliable(new SendRoomGiftRequest { giftType = "flower", toPlayerId = "player-b" });
            Expect(DrainUntil<RoomGiftSent>(guest, MessageType.RoomGiftSent, out var gift), "成员收到礼物广播", ref passed, ref failed);
            Expect(gift != null && gift.giftType == "flower", "礼物类型正确", ref passed, ref failed);

            host.SendReliable(new UdpBindRequest
            {
                roomId = roomId,
                playerId = "player-a",
                sessionId = host.SessionId,
                udpBindToken = createResult?.udpBindToken
            });
            Expect(DrainUntil<UdpBindAccepted>(host, MessageType.UdpBindAccepted, out var bind), "房主收到 UDP 绑定结果", ref passed, ref failed);
            Expect(bind != null && bind.succeeded, "UDP 绑定成功", ref passed, ref failed);

            host.SendReliable(new Heartbeat { clientTimeMs = MockMiniGameTime.NowMs });
            Expect(DrainUntil<Heartbeat>(host, MessageType.Heartbeat, out var heartbeat), "心跳返回", ref passed, ref failed);
            Expect(heartbeat != null && heartbeat.serverTimeMs > 0, "心跳携带服务端时间", ref passed, ref failed);

            if (failed > 0)
            {
                Debug.LogError($"[NiumaMiniGame][阶段2] Mock 基础测试失败：Passed={passed}, Failed={failed}", this);
                return;
            }

            Debug.Log($"[NiumaMiniGame][阶段2] Mock 基础测试通过：Passed={passed}, Failed=0", this);
        }

        private static MockRealtimeNetworkClient CreateClient(string playerId, string displayName)
        {
            var client = new MockRealtimeNetworkClient(MockRoomServer.Shared);
            client.Connect(new RealtimeConnectRequest("mock", 0, 0, playerId, displayName));
            return client;
        }

        private static bool DrainUntil<TMessage>(
            MockRealtimeNetworkClient client,
            string messageType,
            out TMessage payload)
            where TMessage : class, IRealtimeMessage
        {
            for (var i = 0; i < 64; i++)
            {
                if (!client.TryDequeueMessage(out var inbound))
                {
                    break;
                }

                if (string.Equals(inbound.MessageType, messageType, StringComparison.Ordinal))
                {
                    payload = JsonUtility.FromJson<TMessage>(inbound.PayloadJson);
                    return payload != null;
                }
            }

            payload = null;
            return false;
        }

        private static void DrainAll(MockRealtimeNetworkClient client)
        {
            while (client.TryDequeueMessage(out _))
            {
            }
        }

        private void Expect(bool condition, string label, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                if (logPassedChecks)
                {
                    Debug.Log($"[NiumaMiniGame][阶段2] 通过：{label}", this);
                }

                return;
            }

            failed++;
            Debug.LogError($"[NiumaMiniGame][阶段2] 失败：{label}", this);
        }
    }
}
