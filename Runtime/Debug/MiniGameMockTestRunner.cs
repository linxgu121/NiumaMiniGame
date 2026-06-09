using System;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Mock;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using NiumaMiniGame.Telephone;
using UnityEngine;

namespace NiumaMiniGame.Debugging
{
    /// <summary>
    /// MiniGame Mock 流程测试入口。
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
            var viewer = CreateClient("viewer-a", "观战者");

            Expect(host.IsConnected, "房主连接成功", ref passed, ref failed);
            Expect(guest.IsConnected, "成员连接成功", ref passed, ref failed);
            Expect(viewer.IsConnected, "观战者连接成功", ref passed, ref failed);
            Expect(DrainUntil<ConnectAccepted>(host, MessageType.ConnectAccepted, out _), "房主收到 ConnectAccepted", ref passed, ref failed);
            Expect(DrainUntil<ConnectAccepted>(guest, MessageType.ConnectAccepted, out _), "成员收到 ConnectAccepted", ref passed, ref failed);
            Expect(DrainUntil<ConnectAccepted>(viewer, MessageType.ConnectAccepted, out _), "观战者收到 ConnectAccepted", ref passed, ref failed);

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

            viewer.SendReliable(new JoinRoomRequest
            {
                roomId = roomId,
                displayName = "观战者",
                viewer = true
            });

            Expect(DrainUntil<JoinRoomResult>(viewer, MessageType.JoinRoomResult, out var viewerJoinResult), "观战者收到加房结果", ref passed, ref failed);
            Expect(viewerJoinResult != null && viewerJoinResult.succeeded, "观战者加房成功", ref passed, ref failed);

            viewer.SendReliable(new SwitchRoleRequest { viewer = false });
            Expect(DrainLatest<RoomSnapshot>(host, MessageType.RoomSnapshot, out var switchToPlayerSnapshot), "房主收到观战者切玩家快照", ref passed, ref failed);
            Expect(switchToPlayerSnapshot != null && switchToPlayerSnapshot.players != null && switchToPlayerSnapshot.players.Length == 3, "观战者切换为玩家后玩家数为 3", ref passed, ref failed);
            Expect(switchToPlayerSnapshot != null && switchToPlayerSnapshot.viewers != null && switchToPlayerSnapshot.viewers.Length == 0, "观战者切换为玩家后观战者数为 0", ref passed, ref failed);

            viewer.SendReliable(new SwitchRoleRequest { viewer = true });
            Expect(DrainLatest<RoomSnapshot>(guest, MessageType.RoomSnapshot, out var switchToViewerSnapshot), "成员收到玩家切观战者快照", ref passed, ref failed);
            Expect(switchToViewerSnapshot != null && switchToViewerSnapshot.players != null && switchToViewerSnapshot.players.Length == 2, "玩家切换为观战者后玩家数为 2", ref passed, ref failed);
            Expect(switchToViewerSnapshot != null && switchToViewerSnapshot.viewers != null && switchToViewerSnapshot.viewers.Length == 1, "玩家切换为观战者后观战者数为 1", ref passed, ref failed);

            host.SendReliable(new PlayerReadyRequest { ready = true });
            guest.SendReliable(new PlayerReadyRequest { ready = true });

            DrainAll(host);
            Expect(DrainLatest<RoomSnapshot>(guest, MessageType.RoomSnapshot, out var snapshot), "成员收到房间快照", ref passed, ref failed);
            Expect(snapshot != null && snapshot.players != null && snapshot.players.Length == 2, "房间快照包含两名玩家", ref passed, ref failed);
            Expect(snapshot != null && snapshot.viewers != null && snapshot.viewers.Length == 1, "房间快照包含一名观战者", ref passed, ref failed);

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

        [ContextMenu("NiumaMiniGame/阶段3/运行 DrawTelephone Mock 流程测试")]
        public void RunDrawTelephoneMockFlowTests()
        {
            var failed = 0;
            var passed = 0;
            MockRoomServer.Shared.Reset();

            var clients = new[]
            {
                CreateClient("player-a", "甲"),
                CreateClient("player-b", "乙"),
                CreateClient("player-c", "丙"),
                CreateClient("player-d", "丁")
            };
            var viewer = CreateClient("viewer-a", "观战者");

            for (var i = 0; i < clients.Length; i++)
            {
                Expect(DrainUntil<ConnectAccepted>(clients[i], MessageType.ConnectAccepted, out _), $"玩家 {i + 1} 收到连接确认", ref passed, ref failed);
            }
            Expect(DrainUntil<ConnectAccepted>(viewer, MessageType.ConnectAccepted, out _), "观战者收到连接确认", ref passed, ref failed);

            clients[0].SendReliable(new CreateRoomRequest
            {
                modeId = "draw_telephone",
                displayName = "甲"
            });

            Expect(DrainUntil<CreateRoomResult>(clients[0], MessageType.CreateRoomResult, out var createResult), "房主收到建房结果", ref passed, ref failed);
            Expect(createResult != null && createResult.succeeded, "建房成功", ref passed, ref failed);

            var roomId = createResult?.roomId;
            var udpTokens = new string[clients.Length];
            udpTokens[0] = createResult?.udpBindToken;

            for (var i = 1; i < clients.Length; i++)
            {
                clients[i].SendReliable(new JoinRoomRequest
                {
                    roomId = roomId,
                    displayName = $"玩家{i + 1}"
                });
                Expect(DrainUntil<JoinRoomResult>(clients[i], MessageType.JoinRoomResult, out var joinResult), $"玩家 {i + 1} 收到加房结果", ref passed, ref failed);
                Expect(joinResult != null && joinResult.succeeded, $"玩家 {i + 1} 加房成功", ref passed, ref failed);
                udpTokens[i] = joinResult?.udpBindToken;
            }

            viewer.SendReliable(new JoinRoomRequest
            {
                roomId = roomId,
                displayName = "观战者",
                viewer = true
            });
            Expect(DrainUntil<JoinRoomResult>(viewer, MessageType.JoinRoomResult, out var viewerJoinResult), "观战者收到加房结果", ref passed, ref failed);
            Expect(viewerJoinResult != null && viewerJoinResult.succeeded, "观战者加房成功", ref passed, ref failed);
            var viewerUdpToken = viewerJoinResult?.udpBindToken;

            for (var i = 0; i < clients.Length; i++)
            {
                clients[i].SendReliable(new UdpBindRequest
                {
                    roomId = roomId,
                    playerId = clients[i].ClientId,
                    sessionId = clients[i].SessionId,
                    udpBindToken = udpTokens[i]
                });
                Expect(DrainUntil<UdpBindAccepted>(clients[i], MessageType.UdpBindAccepted, out var bindResult), $"玩家 {i + 1} 收到 UDP 绑定结果", ref passed, ref failed);
                Expect(bindResult != null && bindResult.succeeded, $"玩家 {i + 1} UDP 绑定成功", ref passed, ref failed);
            }

            viewer.SendReliable(new UdpBindRequest
            {
                roomId = roomId,
                playerId = viewer.ClientId,
                sessionId = viewer.SessionId,
                udpBindToken = viewerUdpToken
            });
            Expect(DrainUntil<UdpBindAccepted>(viewer, MessageType.UdpBindAccepted, out var viewerBindResult), "观战者收到 UDP 绑定结果", ref passed, ref failed);
            Expect(viewerBindResult != null && viewerBindResult.succeeded, "观战者 UDP 绑定成功", ref passed, ref failed);

            for (var i = 0; i < clients.Length; i++)
            {
                clients[i].SendReliable(new PlayerReadyRequest { ready = true });
            }

            Expect(DrainUntil<DrawTelephoneStarted>(clients[0], MessageType.DrawTelephoneStarted, out var started), "收到 DrawTelephoneStarted", ref passed, ref failed);
            Expect(started != null && started.stageCount == clients.Length, "阶段数量等于玩家数量", ref passed, ref failed);

            for (var stageIndex = 0; stageIndex < clients.Length; stageIndex++)
            {
                var actionType = stageIndex % 2 == 0 ? "DRAW" : "GUESS";
                for (var i = 0; i < clients.Length; i++)
                {
                    Expect(DrainUntilPersonalStage(clients[i], stageIndex, actionType, out var stage), $"玩家 {i + 1} 收到第 {stageIndex} 阶段个人任务", ref passed, ref failed);

                    var task = stage?.tasks != null && stage.tasks.Length > 0 ? stage.tasks[0] : null;
                    Expect(task != null && !string.IsNullOrWhiteSpace(task.chainId), $"玩家 {i + 1} 任务链有效", ref passed, ref failed);

                    if (string.Equals(actionType, "DRAW", StringComparison.Ordinal))
                    {
                        Expect(!string.IsNullOrWhiteSpace(task?.promptWord), $"玩家 {i + 1} 绘画提示有效", ref passed, ref failed);
                        var strokeId = $"stroke_{stageIndex}_{clients[i].ClientId}";
                        clients[i].SendUnreliable(new StrokePointBatch
                        {
                            roomId = roomId,
                            strokeId = strokeId,
                            strokeSequence = 0,
                            points = new[]
                            {
                                new DrawPointData { x = 0.2f, y = 0.2f, pressure = 1f, timeMs = MockMiniGameTime.NowMs },
                                new DrawPointData { x = 0.8f, y = 0.8f, pressure = 1f, timeMs = MockMiniGameTime.NowMs + 16L }
                            }
                        });
                        if (stageIndex == 0 && i == 0)
                        {
                            Expect(DrainUntil<StrokePointBatch>(viewer, MessageType.StrokePointBatch, out var viewerStroke), "观战者收到绘画点位转发", ref passed, ref failed);
                            Expect(viewerStroke != null && string.Equals(viewerStroke.strokeId, strokeId, StringComparison.Ordinal), "观战者点位来自当前绘画笔画", ref passed, ref failed);
                        }

                        clients[i].SendReliable(new SubmitTelephoneDrawing
                        {
                            chainId = task.chainId,
                            strokeGroupId = strokeId
                        });
                    }
                    else
                    {
                        Expect(task?.previousCanvas != null && task.previousCanvas.strokes != null && task.previousCanvas.strokes.Length > 0, $"玩家 {i + 1} 猜词阶段收到上一轮画布", ref passed, ref failed);
                        clients[i].SendReliable(new SubmitTelephoneGuess
                        {
                            chainId = task.chainId,
                            guessText = $"猜词_{stageIndex}_{i}"
                        });
                    }
                }
            }

            Expect(DrainUntil<DrawTelephoneReviewStarted>(clients[0], MessageType.DrawTelephoneReviewStarted, out var review), "收到 ReviewStarted", ref passed, ref failed);
            Expect(review != null && review.chains != null && review.chains.Length == clients.Length, "Review 包含所有传话链", ref passed, ref failed);

            Expect(DrainUntil<DrawTelephoneVotingStarted>(clients[0], MessageType.DrawTelephoneVotingStarted, out var voting), "收到 VotingStarted", ref passed, ref failed);
            Expect(voting != null && voting.chains != null && voting.chains.Length == clients.Length, "投票阶段包含所有传话链", ref passed, ref failed);
            Expect(voting?.chains != null && !string.IsNullOrWhiteSpace(voting.chains[0].originalWord), "投票信息包含原词", ref passed, ref failed);

            for (var voterIndex = 0; voterIndex < clients.Length; voterIndex++)
            {
                if (voting?.chains == null)
                {
                    break;
                }

                for (var chainIndex = 0; chainIndex < voting.chains.Length; chainIndex++)
                {
                    var chain = voting.chains[chainIndex];
                    if (chain == null || string.Equals(chain.starterPlayerId, clients[voterIndex].ClientId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    clients[voterIndex].SendReliable(new SubmitChainVote
                    {
                        chainId = chain.chainId,
                        score = 60 + voterIndex + chainIndex
                    });
                }
            }

            Expect(DrainUntil<DrawTelephoneVotingEnded>(clients[0], MessageType.DrawTelephoneVotingEnded, out var votingEnded), "收到 VotingEnded", ref passed, ref failed);
            Expect(votingEnded != null && votingEnded.results != null && votingEnded.results.Length == clients.Length, "投票结果包含所有传话链", ref passed, ref failed);
            Expect(DrainUntil<GameEnded>(clients[0], MessageType.GameEnded, out var gameEnded), "收到 GameEnded", ref passed, ref failed);
            Expect(gameEnded != null && gameEnded.finalScores != null && gameEnded.finalScores.Length == clients.Length, "结算包含所有玩家分数", ref passed, ref failed);

            if (failed > 0)
            {
                Debug.LogError($"[NiumaMiniGame][阶段3] DrawTelephone Mock 流程测试失败：Passed={passed}, Failed={failed}", this);
                return;
            }

            Debug.Log($"[NiumaMiniGame][阶段3] DrawTelephone Mock 流程测试通过：Passed={passed}, Failed=0", this);
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
            for (var i = 0; i < 128; i++)
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

        private static bool DrainLatest<TMessage>(
            MockRealtimeNetworkClient client,
            string messageType,
            out TMessage payload)
            where TMessage : class, IRealtimeMessage
        {
            payload = null;
            var found = false;

            for (var i = 0; i < 128; i++)
            {
                if (!client.TryDequeueMessage(out var inbound))
                {
                    break;
                }

                if (!string.Equals(inbound.MessageType, messageType, StringComparison.Ordinal))
                {
                    continue;
                }

                var candidate = JsonUtility.FromJson<TMessage>(inbound.PayloadJson);
                if (candidate == null)
                {
                    continue;
                }

                payload = candidate;
                found = true;
            }

            return found;
        }

        private static bool DrainUntilPersonalStage(
            MockRealtimeNetworkClient client,
            int stageIndex,
            string actionType,
            out DrawTelephoneStageStarted stage)
        {
            for (var i = 0; i < 128; i++)
            {
                if (!client.TryDequeueMessage(out var inbound))
                {
                    break;
                }

                if (!string.Equals(inbound.MessageType, MessageType.DrawTelephoneStageStarted, StringComparison.Ordinal))
                {
                    continue;
                }

                var payload = JsonUtility.FromJson<DrawTelephoneStageStarted>(inbound.PayloadJson);
                if (payload == null
                    || payload.stageIndex != stageIndex
                    || !string.Equals(payload.actionType, actionType, StringComparison.Ordinal)
                    || payload.tasks == null
                    || payload.tasks.Length == 0)
                {
                    continue;
                }

                stage = payload;
                return true;
            }

            stage = null;
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
                    Debug.Log($"[NiumaMiniGame] 通过：{label}", this);
                }

                return;
            }

            failed++;
            Debug.LogError($"[NiumaMiniGame] 失败：{label}", this);
        }
    }
}
