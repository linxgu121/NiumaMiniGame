using System;
using System.Collections.Generic;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;
using NiumaMiniGame.Telephone;

namespace NiumaMiniGame.Mock
{
    /// <summary>
    /// 本地内存房间服务端。
    /// 它用于 Unity 前端在不启动 Java 后端时验证网络协议和房间基础流程。
    /// </summary>
    public sealed class MockRoomServer
    {
        private const int MaxRoomIdRetryCount = 10;
        private const int MaxPlayers = 10;
        private const int MaxViewers = 20;
        private const int DrawTelephoneMinPlayers = 2;
        private const float MockDrawStageSeconds = 90f;
        private const float MockGuessStageSeconds = 45f;
        private const float MockReviewSecondsPerChain = 8f;
        private const float MockVotingDurationSeconds = 45f;
        private const string DrawActionType = "DRAW";
        private const string GuessActionType = "GUESS";
        private static readonly char[] RoomIdChars =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        private readonly Dictionary<string, MockServerSession> _sessions = new Dictionary<string, MockServerSession>();
        private readonly Dictionary<string, MockRoomRuntime> _rooms = new Dictionary<string, MockRoomRuntime>();
        private readonly Random _random = new Random();

        public static MockRoomServer Shared { get; } = new MockRoomServer();

        public int RoomCount => _rooms.Count;
        public int SessionCount => _sessions.Count;

        public string Connect(MockRealtimeNetworkClient client, string playerId, string displayName)
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var session = new MockServerSession
            {
                SessionId = sessionId,
                PlayerId = playerId,
                DisplayName = displayName,
                Client = client,
                Connected = true
            };

            _sessions[sessionId] = session;
            client.SetSessionId(sessionId);
            client.EnqueueReliable(MessageType.ConnectAccepted, new ConnectAccepted
            {
                sessionId = sessionId,
                playerId = playerId,
                serverTimeMs = MockMiniGameTime.NowMs
            });

            return sessionId;
        }

        public void Disconnect(string sessionId, string reason)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            session.Connected = false;

            if (!string.IsNullOrWhiteSpace(session.RoomId)
                && _rooms.TryGetValue(session.RoomId, out var room)
                && room.Players.TryGetValue(session.PlayerId, out var player))
            {
                player.Connected = false;
                BroadcastRoomSnapshot(room);
            }
            else if (!string.IsNullOrWhiteSpace(session.RoomId)
                     && _rooms.TryGetValue(session.RoomId, out room)
                     && room.Viewers.TryGetValue(session.PlayerId, out var viewer))
            {
                viewer.Connected = false;
                BroadcastRoomSnapshot(room);
            }
        }

        public void ReceiveReliable<TMessage>(string sessionId, TMessage message)
            where TMessage : IRealtimeMessage
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            switch (message)
            {
                case CreateRoomRequest request:
                    HandleCreateRoom(session, request);
                    break;
                case JoinRoomRequest request:
                    HandleJoinRoom(session, request);
                    break;
                case LeaveRoomRequest request:
                    HandleLeaveRoom(session, request);
                    break;
                case PlayerReadyRequest request:
                    HandleReady(session, request);
                    break;
                case StartGameRequest request:
                    HandleStartGame(session, request);
                    break;
                case ChangeModeRequest request:
                    HandleChangeMode(session, request);
                    break;
                case SwitchRoleRequest request:
                    HandleSwitchRole(session, request);
                    break;
                case Heartbeat heartbeat:
                    HandleHeartbeat(session, heartbeat);
                    break;
                case SendRoomChatRequest request:
                    HandleChat(session, request);
                    break;
                case SendRoomGiftRequest request:
                    HandleGift(session, request);
                    break;
                case UdpBindRequest request:
                    HandleUdpBind(session, request);
                    break;
                case ReconnectRequest request:
                    HandleReconnect(session, request);
                    break;
                case SubmitTelephoneDrawing request:
                    HandleSubmitTelephoneDrawing(session, request);
                    break;
                case SubmitTelephoneGuess request:
                    HandleSubmitTelephoneGuess(session, request);
                    break;
                case SubmitChainVote request:
                    HandleSubmitChainVote(session, request);
                    break;
                default:
                    session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidMessage));
                    break;
            }
        }

        public void ReceiveUnreliable<TMessage>(string sessionId, TMessage message)
            where TMessage : IRealtimeMessage
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            switch (message)
            {
                case StrokePointBatch batch:
                    HandleStrokePointBatch(session, batch);
                    break;
                case CursorPreview preview:
                    BroadcastUnreliableToRoom(session.RoomId, MessageType.CursorPreview, preview);
                    break;
            }
        }

        public void Reset()
        {
            _sessions.Clear();
            _rooms.Clear();
        }

        private void HandleCreateRoom(MockServerSession session, CreateRoomRequest request)
        {
            var roomId = CreateRoomId();
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.Client.EnqueueReliable(MessageType.CreateRoomResult, new CreateRoomResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.ServerBusy)
                });
                return;
            }

            var room = new MockRoomRuntime
            {
                RoomId = roomId,
                ModeId = string.IsNullOrWhiteSpace(request.modeId) ? "draw_telephone" : request.modeId,
                HostPlayerId = session.PlayerId,
                State = nameof(MiniGameRoomState.Lobby),
                StateEnterTimeMs = MockMiniGameTime.NowMs,
                StateDeadlineTimeMs = 0L
            };

            _rooms.Add(roomId, room);
            AddOrReconnectPlayer(room, session, request.displayName);

            session.Client.EnqueueReliable(MessageType.CreateRoomResult, new CreateRoomResult
            {
                succeeded = true,
                roomId = roomId,
                udpBindToken = CreateUdpBindToken(roomId, session.PlayerId),
                udpPort = 0
            }, roomId);

            BroadcastPlayerJoined(room, session);
            BroadcastRoomSnapshot(room);
        }

        private void HandleJoinRoom(MockServerSession session, JoinRoomRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.roomId) || !_rooms.TryGetValue(request.roomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.RoomNotFound)
                });
                return;
            }

            if (!request.viewer && room.Players.Count >= MaxPlayers && !room.Players.ContainsKey(session.PlayerId))
            {
                session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.RoomFull)
                }, room.RoomId);
                return;
            }

            if ((request.viewer && room.Players.ContainsKey(session.PlayerId))
                || (!request.viewer && room.Viewers.ContainsKey(session.PlayerId)))
            {
                session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.InvalidState)
                }, room.RoomId);
                return;
            }

            if (request.viewer)
            {
                if (room.Viewers.Count >= MaxViewers && !room.Viewers.ContainsKey(session.PlayerId))
                {
                    session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
                    {
                        succeeded = false,
                        errorCode = nameof(MiniGameErrorCode.RoomFull)
                    }, room.RoomId);
                    return;
                }

                AddOrReconnectViewer(room, session, request.displayName);
            }
            else
            {
                AddOrReconnectPlayer(room, session, request.displayName);
            }

            session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
            {
                succeeded = true,
                roomId = room.RoomId,
                udpBindToken = CreateUdpBindToken(room.RoomId, session.PlayerId),
                udpPort = 0
            }, room.RoomId);

            BroadcastPlayerJoined(room, session);
            BroadcastRoomSnapshot(room);
        }

        private void HandleLeaveRoom(MockServerSession session, LeaveRoomRequest request)
        {
            var roomId = !string.IsNullOrWhiteSpace(request?.roomId) ? request.roomId : session.RoomId;
            if (string.IsNullOrWhiteSpace(roomId) || !_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            if (!room.Players.TryGetValue(session.PlayerId, out var player))
            {
                if (!room.Viewers.TryGetValue(session.PlayerId, out player))
                {
                    return;
                }

                room.Viewers.Remove(session.PlayerId);
                session.RoomId = null;
                session.IsViewer = false;

                BroadcastReliableToRoom(room.RoomId, MessageType.PlayerLeft, new PlayerLeft
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName
                });

                if (room.Players.Count == 0)
                {
                    _rooms.Remove(room.RoomId);
                }
                else
                {
                    BroadcastRoomSnapshot(room);
                }

                return;
            }

            room.Players.Remove(session.PlayerId);
            TransferHostIfNeeded(room);
            session.RoomId = null;
            session.IsViewer = false;

            BroadcastReliableToRoom(room.RoomId, MessageType.PlayerLeft, new PlayerLeft
            {
                playerId = player.PlayerId,
                displayName = player.DisplayName
            });

            if (room.Players.Count == 0)
            {
                _rooms.Remove(room.RoomId);
            }
            else
            {
                BroadcastRoomSnapshot(room);
            }
        }

        private void HandleReady(MockServerSession session, PlayerReadyRequest request)
        {
            if (string.IsNullOrWhiteSpace(session.RoomId) || !_rooms.TryGetValue(session.RoomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomNotFound));
                return;
            }

            if (room.Players.TryGetValue(session.PlayerId, out var player))
            {
                player.Ready = request.ready;
                BroadcastRoomSnapshot(room);
                TryStartDrawTelephone(room, true);
            }
        }

        private static void TransferHostIfNeeded(MockRoomRuntime room)
        {
            if (room == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(room.HostPlayerId) && room.Players.ContainsKey(room.HostPlayerId))
            {
                return;
            }

            MockRoomPlayerRuntime nextHost = null;
            foreach (var player in room.Players.Values)
            {
                if (nextHost == null || player.JoinedAtMs < nextHost.JoinedAtMs)
                {
                    nextHost = player;
                }
            }

            room.HostPlayerId = nextHost?.PlayerId;
        }

        private void HandleStartGame(MockServerSession session, StartGameRequest request)
        {
            var roomId = !string.IsNullOrWhiteSpace(request?.roomId) ? request.roomId : session.RoomId;
            if (string.IsNullOrWhiteSpace(roomId) || !_rooms.TryGetValue(roomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomNotFound));
                return;
            }

            if (!string.Equals(room.HostPlayerId, session.PlayerId, StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.PermissionDenied), room.RoomId);
                return;
            }

            if (!TryStartDrawTelephone(room, false))
            {
                BroadcastReliableToRoom(room.RoomId, MessageType.RoomToastMessage, new RoomToastMessage
                {
                    messageKey = "room_start_not_enough_players",
                    text = "当前玩家数量没满足模式需求无法开始游戏",
                    sourcePlayerId = session.PlayerId,
                    serverTimeMs = MockMiniGameTime.NowMs
                });
            }
        }

        private void HandleChangeMode(MockServerSession session, ChangeModeRequest request)
        {
            if (string.IsNullOrWhiteSpace(session.RoomId) || !_rooms.TryGetValue(session.RoomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomNotFound));
                return;
            }

            if (!string.Equals(room.HostPlayerId, session.PlayerId, StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.PermissionDenied), room.RoomId);
                return;
            }

            if (!string.Equals(room.State, nameof(MiniGameRoomState.Lobby), StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), room.RoomId);
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.modeId))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidMessage), room.RoomId);
                return;
            }

            room.ModeId = request.modeId.Trim();
            foreach (var player in room.Players.Values)
            {
                player.Ready = false;
            }

            BroadcastReliableToRoom(room.RoomId, MessageType.RoomToastMessage, new RoomToastMessage
            {
                messageKey = "room_mode_changed",
                text = "房主已切换模式。",
                sourcePlayerId = session.PlayerId,
                serverTimeMs = MockMiniGameTime.NowMs
            });
            BroadcastRoomSnapshot(room);
        }

        private void HandleSwitchRole(MockServerSession session, SwitchRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(session.RoomId) || !_rooms.TryGetValue(session.RoomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomNotFound));
                return;
            }

            if (!string.Equals(room.State, nameof(MiniGameRoomState.Lobby), StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), room.RoomId);
                return;
            }

            if (request != null && request.viewer)
            {
                SwitchPlayerToViewer(session, room);
            }
            else
            {
                SwitchViewerToPlayer(session, room);
            }
        }

        private void SwitchPlayerToViewer(MockServerSession session, MockRoomRuntime room)
        {
            if (!room.Players.TryGetValue(session.PlayerId, out var player))
            {
                if (room.Viewers.ContainsKey(session.PlayerId))
                {
                    return;
                }

                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidPlayer), room.RoomId);
                return;
            }

            if (room.Players.Count <= 1)
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), room.RoomId);
                return;
            }

            if (room.Viewers.Count >= MaxViewers)
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomFull), room.RoomId);
                return;
            }

            room.Players.Remove(session.PlayerId);
            player.Ready = false;
            player.IsViewer = true;
            room.Viewers[player.PlayerId] = player;
            session.IsViewer = true;
            TransferHostIfNeeded(room);
            ResetAllReady(room);
            BroadcastRoleChanged(room, player, true);
            BroadcastRoomSnapshot(room);
        }

        private void SwitchViewerToPlayer(MockServerSession session, MockRoomRuntime room)
        {
            if (!room.Viewers.TryGetValue(session.PlayerId, out var viewer))
            {
                if (room.Players.ContainsKey(session.PlayerId))
                {
                    return;
                }

                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidPlayer), room.RoomId);
                return;
            }

            if (room.Players.Count >= MaxPlayers)
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.RoomFull), room.RoomId);
                return;
            }

            room.Viewers.Remove(session.PlayerId);
            viewer.Ready = false;
            viewer.IsViewer = false;
            room.Players[viewer.PlayerId] = viewer;
            session.IsViewer = false;
            if (string.IsNullOrWhiteSpace(room.HostPlayerId))
            {
                room.HostPlayerId = viewer.PlayerId;
            }

            ResetAllReady(room);
            BroadcastRoleChanged(room, viewer, false);
            BroadcastRoomSnapshot(room);
        }

        private void ResetAllReady(MockRoomRuntime room)
        {
            if (room == null)
            {
                return;
            }

            foreach (var player in room.Players.Values)
            {
                player.Ready = false;
            }
        }

        private void BroadcastRoleChanged(MockRoomRuntime room, MockRoomPlayerRuntime player, bool viewer)
        {
            BroadcastReliableToRoom(room.RoomId, MessageType.RoomToastMessage, new RoomToastMessage
            {
                messageKey = viewer ? "room_role_switched_to_viewer" : "room_role_switched_to_player",
                text = $"{SafeDisplayName(player.DisplayName, player.PlayerId)} 已切换为{(viewer ? "观战者" : "玩家")}。",
                sourcePlayerId = player.PlayerId,
                serverTimeMs = MockMiniGameTime.NowMs
            });
        }

        private static string SafeDisplayName(string displayName, string playerId)
        {
            return string.IsNullOrWhiteSpace(displayName) ? playerId : displayName;
        }

        private void HandleHeartbeat(MockServerSession session, Heartbeat heartbeat)
        {
            session.Client.EnqueueReliable(MessageType.Heartbeat, new Heartbeat
            {
                clientTimeMs = heartbeat.clientTimeMs,
                serverTimeMs = MockMiniGameTime.NowMs
            }, session.RoomId);
        }

        private void HandleChat(MockServerSession session, SendRoomChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(session.RoomId) || request == null || string.IsNullOrWhiteSpace(request.text))
            {
                return;
            }

            BroadcastReliableToRoom(session.RoomId, MessageType.RoomChatMessage, new RoomChatMessage
            {
                playerId = session.PlayerId,
                displayName = session.DisplayName,
                text = request.text.Trim(),
                serverTimeMs = MockMiniGameTime.NowMs
            });
        }

        private void HandleGift(MockServerSession session, SendRoomGiftRequest request)
        {
            if (string.IsNullOrWhiteSpace(session.RoomId) || request == null || string.IsNullOrWhiteSpace(request.giftType))
            {
                return;
            }

            BroadcastReliableToRoom(session.RoomId, MessageType.RoomGiftSent, new RoomGiftSent
            {
                fromPlayerId = session.PlayerId,
                fromDisplayName = session.DisplayName,
                toPlayerId = request.toPlayerId,
                giftType = request.giftType,
                serverTimeMs = MockMiniGameTime.NowMs
            });
        }

        private void HandleUdpBind(MockServerSession session, UdpBindRequest request)
        {
            var succeeded = request != null
                            && string.Equals(request.roomId, session.RoomId, StringComparison.Ordinal)
                            && string.Equals(request.playerId, session.PlayerId, StringComparison.Ordinal)
                            && string.Equals(request.sessionId, session.SessionId, StringComparison.Ordinal)
                            && string.Equals(request.udpBindToken, CreateUdpBindToken(session.RoomId, session.PlayerId), StringComparison.Ordinal);

            session.UdpBound = succeeded;
            session.Client.EnqueueReliable(MessageType.UdpBindAccepted, new UdpBindAccepted
            {
                succeeded = succeeded,
                errorCode = succeeded ? null : nameof(MiniGameErrorCode.PermissionDenied)
            }, session.RoomId);
        }

        private void HandleReconnect(MockServerSession session, ReconnectRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.roomId) || !_rooms.TryGetValue(request.roomId, out var room))
            {
                session.Client.EnqueueReliable(MessageType.ReconnectResult, new ReconnectResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.RoomNotFound)
                });
                return;
            }

            var isViewer = false;
            if (!room.Players.ContainsKey(request.playerId))
            {
                if (!room.Viewers.ContainsKey(request.playerId))
                {
                    session.Client.EnqueueReliable(MessageType.ReconnectResult, new ReconnectResult
                    {
                        succeeded = false,
                        errorCode = nameof(MiniGameErrorCode.PermissionDenied)
                    }, room.RoomId);
                    return;
                }

                isViewer = true;
            }

            session.PlayerId = request.playerId;
            session.RoomId = room.RoomId;
            session.IsViewer = isViewer;
            if (isViewer)
            {
                room.Viewers[request.playerId].Connected = true;
            }
            else
            {
                room.Players[request.playerId].Connected = true;
            }

            session.Client.EnqueueReliable(MessageType.ReconnectResult, new ReconnectResult
            {
                succeeded = true,
                sessionId = session.SessionId,
                snapshot = BuildSnapshot(room, request.playerId),
                udpBindToken = CreateUdpBindToken(room.RoomId, session.PlayerId),
                udpPort = 0
            }, room.RoomId);

            BroadcastRoomSnapshot(room);
        }

        private void AddOrReconnectPlayer(MockRoomRuntime room, MockServerSession session, string displayName)
        {
            session.DisplayName = MiniGameIdentityUtility.NormalizeDisplayName(displayName ?? session.DisplayName);
            session.RoomId = room.RoomId;
            session.IsViewer = false;

            if (!room.Players.TryGetValue(session.PlayerId, out var player))
            {
                player = new MockRoomPlayerRuntime
                {
                    PlayerId = session.PlayerId,
                    JoinedAtMs = MockMiniGameTime.NowMs
                };
                room.Players.Add(player.PlayerId, player);
            }

            player.DisplayName = session.DisplayName;
            player.Connected = true;
            player.IsViewer = false;
        }

        private void AddOrReconnectViewer(MockRoomRuntime room, MockServerSession session, string displayName)
        {
            session.DisplayName = MiniGameIdentityUtility.NormalizeDisplayName(displayName ?? session.DisplayName);
            session.RoomId = room.RoomId;
            session.IsViewer = true;

            if (!room.Viewers.TryGetValue(session.PlayerId, out var viewer))
            {
                viewer = new MockRoomPlayerRuntime
                {
                    PlayerId = session.PlayerId,
                    JoinedAtMs = MockMiniGameTime.NowMs,
                    IsViewer = true
                };
                room.Viewers.Add(viewer.PlayerId, viewer);
            }

            viewer.DisplayName = session.DisplayName;
            viewer.Connected = true;
            viewer.IsViewer = true;
        }

        private bool TryStartDrawTelephone(MockRoomRuntime room, bool requireReady)
        {
            if (room == null
                || !string.Equals(room.ModeId, "draw_telephone", StringComparison.Ordinal)
                || !string.Equals(room.State, nameof(MiniGameRoomState.Lobby), StringComparison.Ordinal))
            {
                return false;
            }

            if (room.Players.Count < DrawTelephoneMinPlayers || room.Players.Count % 2 != 0)
            {
                return false;
            }

            foreach (var player in room.Players.Values)
            {
                if (!player.Connected)
                {
                    return false;
                }

                if (requireReady && !player.Ready)
                {
                    return false;
                }
            }

            StartDrawTelephone(room);
            return true;
        }

        private void StartDrawTelephone(MockRoomRuntime room)
        {
            var playerIds = new List<string>(room.Players.Keys);
            playerIds.Sort(StringComparer.Ordinal);

            room.RoomSeed = MockMiniGameTime.NowMs;
            ShufflePlayerIds(playerIds, room.RoomSeed);
            room.MaxRoundCount = playerIds.Count;
            room.RoundIndex = 0;
            room.CurrentStageIndex = -1;
            room.Chains.Clear();
            room.StrokeGroups.Clear();
            room.ChainVotes.Clear();

            for (var chainIndex = 0; chainIndex < playerIds.Count; chainIndex++)
            {
                var chain = new MockDrawTelephoneChainRuntime
                {
                    ChainId = $"chain_{chainIndex + 1:00}",
                    OriginalWordId = $"mock_word_{chainIndex + 1:00}",
                    OriginalWordText = MockWordText(chainIndex),
                    StarterPlayerId = playerIds[chainIndex],
                    Entries = new MockDrawTelephoneStageEntryRuntime[playerIds.Count]
                };

                for (var stageIndex = 0; stageIndex < playerIds.Count; stageIndex++)
                {
                    chain.Entries[stageIndex] = new MockDrawTelephoneStageEntryRuntime
                    {
                        StageIndex = stageIndex,
                        PlayerId = playerIds[(chainIndex + stageIndex) % playerIds.Count],
                        ActionType = stageIndex % 2 == 0 ? DrawActionType : GuessActionType,
                        IsTimeoutSubmit = false
                    };
                }

                room.Chains.Add(chain);
            }

            room.State = nameof(MiniGameRoomState.Preparing);
            room.StateEnterTimeMs = MockMiniGameTime.NowMs;
            room.StateDeadlineTimeMs = 0L;

            BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneStarted, new DrawTelephoneStarted
            {
                stageCount = room.MaxRoundCount,
                drawStageSeconds = MockDrawStageSeconds,
                guessStageSeconds = MockGuessStageSeconds,
                reviewSecondsPerChain = MockReviewSecondsPerChain,
                roomSeed = room.RoomSeed
            });

            AdvanceDrawTelephoneStage(room, false);
        }

        private void AdvanceDrawTelephoneStage(MockRoomRuntime room, bool publishPreviousStageEnd)
        {
            if (publishPreviousStageEnd)
            {
                BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneStageEnded, new DrawTelephoneStageEnded
                {
                    stageIndex = room.CurrentStageIndex,
                    allSubmitted = true,
                    timeoutReached = false
                });
            }

            var nextStageIndex = room.CurrentStageIndex + 1;
            if (nextStageIndex >= room.MaxRoundCount)
            {
                StartDrawTelephoneReview(room);
                return;
            }

            room.CurrentStageIndex = nextStageIndex;
            room.RoundIndex = nextStageIndex;
            room.CurrentActionType = nextStageIndex % 2 == 0 ? DrawActionType : GuessActionType;
            room.State = nameof(MiniGameRoomState.Playing);
            room.StateEnterTimeMs = MockMiniGameTime.NowMs;
            var durationSeconds = string.Equals(room.CurrentActionType, DrawActionType, StringComparison.Ordinal)
                ? MockDrawStageSeconds
                : MockGuessStageSeconds;
            room.StateDeadlineTimeMs = room.StateEnterTimeMs + (long)(durationSeconds * 1000f);
            room.CurrentTasks.Clear();
            room.SubmittedPlayers.Clear();

            foreach (var chain in room.Chains)
            {
                var entry = chain.Entries[nextStageIndex];
                var task = BuildDrawTelephoneTask(room, chain, entry);
                room.CurrentTasks[entry.PlayerId] = task;
                room.SubmittedPlayers[entry.PlayerId] = false;
            }

            var publicStage = new DrawTelephoneStageStarted
            {
                stageIndex = nextStageIndex,
                actionType = room.CurrentActionType,
                stageDurationSeconds = durationSeconds,
                deadlineTimeMs = room.StateDeadlineTimeMs,
                tasks = new DrawTelephoneTask[0]
            };
            BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneStageStarted, publicStage);

            foreach (var pair in room.CurrentTasks)
            {
                SendReliableToPlayer(room, pair.Key, MessageType.DrawTelephoneStageStarted, new DrawTelephoneStageStarted
                {
                    stageIndex = nextStageIndex,
                    actionType = room.CurrentActionType,
                    stageDurationSeconds = durationSeconds,
                    deadlineTimeMs = room.StateDeadlineTimeMs,
                    tasks = new[] { pair.Value }
                });
            }

            BroadcastRoomSnapshot(room);
        }

        private DrawTelephoneTask BuildDrawTelephoneTask(
            MockRoomRuntime room,
            MockDrawTelephoneChainRuntime chain,
            MockDrawTelephoneStageEntryRuntime entry)
        {
            var task = new DrawTelephoneTask
            {
                chainId = chain.ChainId,
                stageIndex = entry.StageIndex,
                actionType = entry.ActionType
            };

            if (string.Equals(entry.ActionType, DrawActionType, StringComparison.Ordinal))
            {
                task.promptWord = entry.StageIndex == 0
                    ? chain.OriginalWordText
                    : chain.Entries[entry.StageIndex - 1].GuessText;
                return task;
            }

            var previousEntry = chain.Entries[entry.StageIndex - 1];
            task.previousStrokeGroupId = previousEntry.StrokeGroupId;
            task.previousCanvas = BuildCanvasData(room, previousEntry.StrokeGroupId);
            return task;
        }

        private void HandleSubmitTelephoneDrawing(MockServerSession session, SubmitTelephoneDrawing request)
        {
            if (!TryGetCurrentTelephoneTask(session, DrawActionType, out var room, out var task)
                || request == null
                || !string.Equals(task.chainId, request.chainId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(request.strokeGroupId))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session.RoomId);
                return;
            }

            var chain = FindChain(room, request.chainId);
            if (chain == null)
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session.RoomId);
                return;
            }

            if (!room.StrokeGroups.ContainsKey(request.strokeGroupId))
            {
                room.StrokeGroups[request.strokeGroupId] = new MockStrokeGroupRuntime
                {
                    StrokeGroupId = request.strokeGroupId,
                    PlayerId = session.PlayerId,
                    ChainId = request.chainId,
                    StageIndex = room.CurrentStageIndex
                };
            }

            var entry = chain.Entries[room.CurrentStageIndex];
            entry.StrokeGroupId = request.strokeGroupId;
            entry.SubmittedTimeMs = MockMiniGameTime.NowMs;
            room.SubmittedPlayers[session.PlayerId] = true;

            AdvanceIfAllSubmitted(room);
        }

        private void HandleSubmitTelephoneGuess(MockServerSession session, SubmitTelephoneGuess request)
        {
            if (!TryGetCurrentTelephoneTask(session, GuessActionType, out var room, out var task)
                || request == null
                || !string.Equals(task.chainId, request.chainId, StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session.RoomId);
                return;
            }

            var chain = FindChain(room, request.chainId);
            if (chain == null)
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session.RoomId);
                return;
            }

            var entry = chain.Entries[room.CurrentStageIndex];
            entry.GuessText = request.guessText?.Trim();
            entry.SubmittedTimeMs = MockMiniGameTime.NowMs;
            room.SubmittedPlayers[session.PlayerId] = true;

            AdvanceIfAllSubmitted(room);
        }

        private void HandleStrokePointBatch(MockServerSession session, StrokePointBatch batch)
        {
            if (batch == null
                || !session.UdpBound
                || !TryGetCurrentTelephoneTask(session, DrawActionType, out var room, out var task)
                || !string.Equals(batch.roomId, room.RoomId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(batch.strokeId))
            {
                return;
            }

            if (!room.StrokeGroups.TryGetValue(batch.strokeId, out var group))
            {
                group = new MockStrokeGroupRuntime
                {
                    StrokeGroupId = batch.strokeId,
                    PlayerId = session.PlayerId,
                    ChainId = task.chainId,
                    StageIndex = task.stageIndex
                };
                room.StrokeGroups.Add(batch.strokeId, group);
            }

            if (!string.Equals(group.PlayerId, session.PlayerId, StringComparison.Ordinal)
                || !string.Equals(group.ChainId, task.chainId, StringComparison.Ordinal)
                || group.StageIndex != task.stageIndex)
            {
                return;
            }

            AddStrokePoints(group, batch.strokeId, batch.points);
            BroadcastUnreliableToViewers(room.RoomId, MessageType.StrokePointBatch, batch);
        }

        private bool TryGetCurrentTelephoneTask(
            MockServerSession session,
            string requiredActionType,
            out MockRoomRuntime room,
            out DrawTelephoneTask task)
        {
            room = null;
            task = null;

            if (session == null
                || string.IsNullOrWhiteSpace(session.RoomId)
                || !_rooms.TryGetValue(session.RoomId, out room)
                || !room.CurrentTasks.TryGetValue(session.PlayerId, out task))
            {
                return false;
            }

            return string.Equals(task.actionType, requiredActionType, StringComparison.Ordinal);
        }

        private void AdvanceIfAllSubmitted(MockRoomRuntime room)
        {
            foreach (var pair in room.CurrentTasks)
            {
                if (!room.SubmittedPlayers.TryGetValue(pair.Key, out var submitted) || !submitted)
                {
                    return;
                }
            }

            AdvanceDrawTelephoneStage(room, true);
        }

        private void StartDrawTelephoneReview(MockRoomRuntime room)
        {
            room.State = nameof(MiniGameRoomState.Review);
            room.CurrentActionType = null;
            room.CurrentTasks.Clear();
            room.SubmittedPlayers.Clear();
            room.StateEnterTimeMs = MockMiniGameTime.NowMs;
            room.StateDeadlineTimeMs = 0L;

            var chains = new DrawTelephoneChainState[room.Chains.Count];
            for (var i = 0; i < room.Chains.Count; i++)
            {
                chains[i] = BuildChainState(room.Chains[i]);
            }

            BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneReviewStarted, new DrawTelephoneReviewStarted
            {
                chains = chains,
                reviewSecondsPerChain = MockReviewSecondsPerChain
            });
            BroadcastRoomSnapshot(room);

            // Mock 没有真实计时器，Review 广播后立即进入投票阶段，便于本地一键测试完整流程。
            StartDrawTelephoneVoting(room);
        }

        private static DrawTelephoneChainState BuildChainState(MockDrawTelephoneChainRuntime chain)
        {
            var entries = new DrawTelephoneStageEntry[chain.Entries.Length];
            for (var i = 0; i < chain.Entries.Length; i++)
            {
                var source = chain.Entries[i];
                entries[i] = new DrawTelephoneStageEntry
                {
                    stageIndex = source.StageIndex,
                    playerId = source.PlayerId,
                    actionType = source.ActionType,
                    strokeGroupId = source.StrokeGroupId,
                    guessText = source.GuessText,
                    submittedTimeMs = source.SubmittedTimeMs,
                    isTimeoutSubmit = source.IsTimeoutSubmit
                };
            }

            var finalGuess = entries.Length > 0 ? entries[entries.Length - 1].guessText : null;
            return new DrawTelephoneChainState
            {
                chainId = chain.ChainId,
                originalWordId = chain.OriginalWordId,
                starterPlayerId = chain.StarterPlayerId,
                entries = entries,
                finalGuessText = finalGuess,
                score = 0
            };
        }

        private void StartDrawTelephoneVoting(MockRoomRuntime room)
        {
            room.State = nameof(MiniGameRoomState.Voting);
            room.StateEnterTimeMs = MockMiniGameTime.NowMs;
            room.StateDeadlineTimeMs = room.StateEnterTimeMs + (long)(MockVotingDurationSeconds * 1000f);
            room.ChainVotes.Clear();

            BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneVotingStarted, new DrawTelephoneVotingStarted
            {
                chains = BuildChainVoteInfos(room),
                votingDurationSeconds = MockVotingDurationSeconds,
                deadlineTimeMs = room.StateDeadlineTimeMs
            });
            BroadcastRoomSnapshot(room);
        }

        private void HandleSubmitChainVote(MockServerSession session, SubmitChainVote request)
        {
            if (session == null
                || request == null
                || string.IsNullOrWhiteSpace(session.RoomId)
                || !_rooms.TryGetValue(session.RoomId, out var room)
                || !string.Equals(room.State, nameof(MiniGameRoomState.Voting), StringComparison.Ordinal)
                || request.score < 0
                || request.score > 100)
            {
                session?.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session?.RoomId);
                return;
            }

            var chain = FindChain(room, request.chainId);
            if (chain == null || string.Equals(chain.StarterPlayerId, session.PlayerId, StringComparison.Ordinal))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.PermissionDenied), session.RoomId);
                return;
            }

            if (!room.ChainVotes.TryGetValue(request.chainId, out var votes))
            {
                votes = new Dictionary<string, int>();
                room.ChainVotes.Add(request.chainId, votes);
            }

            if (votes.ContainsKey(session.PlayerId))
            {
                session.Client.EnqueueReliable(MessageType.ErrorMessage, BuildError(MiniGameErrorCode.InvalidState), session.RoomId);
                return;
            }

            votes[session.PlayerId] = request.score;
            if (AllVotesSubmitted(room))
            {
                EndDrawTelephoneVoting(room);
            }
        }

        private void EndDrawTelephoneVoting(MockRoomRuntime room)
        {
            var results = BuildVoteResults(room);
            BroadcastReliableToRoom(room.RoomId, MessageType.DrawTelephoneVotingEnded, new DrawTelephoneVotingEnded
            {
                results = results
            });

            ApplyVoteScores(room, results);
            room.State = nameof(MiniGameRoomState.Settlement);
            BroadcastReliableToRoom(room.RoomId, MessageType.GameEnded, new GameEnded
            {
                finalScores = BuildScoreEntries(room),
                winnerPlayerId = FindWinnerPlayerId(room)
            });

            ReturnRoomToLobby(room);
            BroadcastRoomSnapshot(room);
        }

        private static void ReturnRoomToLobby(MockRoomRuntime room)
        {
            room.State = nameof(MiniGameRoomState.Lobby);
            room.RoundIndex = 0;
            room.MaxRoundCount = 0;
            room.DrawerPlayerId = null;
            room.StateEnterTimeMs = MockMiniGameTime.NowMs;
            room.StateDeadlineTimeMs = 0L;
            room.CurrentStageIndex = -1;
            room.CurrentActionType = null;
            room.CurrentTasks.Clear();
            room.SubmittedPlayers.Clear();
            room.ChainVotes.Clear();
            room.Chains.Clear();
            room.StrokeGroups.Clear();

            foreach (var player in room.Players.Values)
            {
                player.Ready = false;
            }
        }

        private static ChainVoteInfo[] BuildChainVoteInfos(MockRoomRuntime room)
        {
            var result = new ChainVoteInfo[room.Chains.Count];
            for (var i = 0; i < room.Chains.Count; i++)
            {
                var chain = room.Chains[i];
                room.Players.TryGetValue(chain.StarterPlayerId, out var starter);
                result[i] = new ChainVoteInfo
                {
                    chainId = chain.ChainId,
                    starterPlayerId = chain.StarterPlayerId,
                    starterDisplayName = starter?.DisplayName,
                    originalWord = chain.OriginalWordText,
                    finalGuessText = chain.Entries.Length > 0 ? chain.Entries[chain.Entries.Length - 1].GuessText : null,
                    entries = BuildStageEntries(chain)
                };
            }

            return result;
        }

        private static ChainVoteResult[] BuildVoteResults(MockRoomRuntime room)
        {
            var result = new ChainVoteResult[room.Chains.Count];
            for (var i = 0; i < room.Chains.Count; i++)
            {
                var chain = room.Chains[i];
                room.ChainVotes.TryGetValue(chain.ChainId, out var votes);
                var voteCount = votes?.Count ?? 0;
                var totalScore = 0;
                if (votes != null)
                {
                    foreach (var score in votes.Values)
                    {
                        totalScore += score;
                    }
                }

                result[i] = new ChainVoteResult
                {
                    chainId = chain.ChainId,
                    starterPlayerId = chain.StarterPlayerId,
                    originalWord = chain.OriginalWordText,
                    finalGuessText = chain.Entries.Length > 0 ? chain.Entries[chain.Entries.Length - 1].GuessText : null,
                    finalScore = voteCount > 0 ? (int)Math.Round(totalScore / (double)voteCount) : 0,
                    voteCount = voteCount
                };
            }

            return result;
        }

        private static DrawTelephoneStageEntry[] BuildStageEntries(MockDrawTelephoneChainRuntime chain)
        {
            var entries = new DrawTelephoneStageEntry[chain.Entries.Length];
            for (var i = 0; i < chain.Entries.Length; i++)
            {
                var source = chain.Entries[i];
                entries[i] = new DrawTelephoneStageEntry
                {
                    stageIndex = source.StageIndex,
                    playerId = source.PlayerId,
                    actionType = source.ActionType,
                    strokeGroupId = source.StrokeGroupId,
                    guessText = source.GuessText,
                    submittedTimeMs = source.SubmittedTimeMs,
                    isTimeoutSubmit = source.IsTimeoutSubmit
                };
            }

            return entries;
        }

        private static bool AllVotesSubmitted(MockRoomRuntime room)
        {
            foreach (var player in room.Players.Values)
            {
                foreach (var chain in room.Chains)
                {
                    if (string.Equals(chain.StarterPlayerId, player.PlayerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!room.ChainVotes.TryGetValue(chain.ChainId, out var votes)
                        || !votes.ContainsKey(player.PlayerId))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ApplyVoteScores(MockRoomRuntime room, ChainVoteResult[] results)
        {
            if (results == null)
            {
                return;
            }

            for (var i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (result != null && room.Players.TryGetValue(result.starterPlayerId, out var player))
                {
                    player.Score += result.finalScore;
                }
            }
        }

        private static ScoreEntry[] BuildScoreEntries(MockRoomRuntime room)
        {
            var scores = new ScoreEntry[room.Players.Count];
            var index = 0;
            foreach (var player in room.Players.Values)
            {
                scores[index] = new ScoreEntry
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName,
                    score = player.Score
                };
                index++;
            }

            Array.Sort(scores, (a, b) => b.score.CompareTo(a.score));
            return scores;
        }

        private static string FindWinnerPlayerId(MockRoomRuntime room)
        {
            string winnerPlayerId = null;
            var bestScore = int.MinValue;
            foreach (var player in room.Players.Values)
            {
                if (player.Score > bestScore)
                {
                    bestScore = player.Score;
                    winnerPlayerId = player.PlayerId;
                }
            }

            return winnerPlayerId;
        }

        private static MockDrawTelephoneChainRuntime FindChain(MockRoomRuntime room, string chainId)
        {
            for (var i = 0; i < room.Chains.Count; i++)
            {
                if (string.Equals(room.Chains[i].ChainId, chainId, StringComparison.Ordinal))
                {
                    return room.Chains[i];
                }
            }

            return null;
        }

        private static DrawTelephoneCanvasData BuildCanvasData(MockRoomRuntime room, string strokeGroupId)
        {
            if (string.IsNullOrWhiteSpace(strokeGroupId)
                || !room.StrokeGroups.TryGetValue(strokeGroupId, out var group))
            {
                return null;
            }

            var strokes = new DrawTelephoneStrokeData[group.Strokes.Count];
            for (var i = 0; i < group.Strokes.Count; i++)
            {
                strokes[i] = new DrawTelephoneStrokeData
                {
                    strokeId = group.Strokes[i].strokeId,
                    points = ClonePoints(group.Strokes[i].points)
                };
            }

            return new DrawTelephoneCanvasData
            {
                strokeGroupId = strokeGroupId,
                strokes = strokes
            };
        }

        private static void AddStrokePoints(MockStrokeGroupRuntime group, string strokeId, DrawPointData[] points)
        {
            if (group == null || points == null || points.Length == 0)
            {
                return;
            }

            DrawTelephoneStrokeData stroke = null;
            for (var i = 0; i < group.Strokes.Count; i++)
            {
                if (string.Equals(group.Strokes[i].strokeId, strokeId, StringComparison.Ordinal))
                {
                    stroke = group.Strokes[i];
                    break;
                }
            }

            if (stroke == null)
            {
                group.Strokes.Add(new DrawTelephoneStrokeData
                {
                    strokeId = strokeId,
                    points = ClonePoints(points)
                });
                return;
            }

            var oldLength = stroke.points?.Length ?? 0;
            var merged = new DrawPointData[oldLength + points.Length];
            if (oldLength > 0)
            {
                Array.Copy(stroke.points, merged, oldLength);
            }

            var cloned = ClonePoints(points);
            Array.Copy(cloned, 0, merged, oldLength, cloned.Length);
            stroke.points = merged;
        }

        private static DrawPointData[] ClonePoints(DrawPointData[] points)
        {
            if (points == null || points.Length == 0)
            {
                return new DrawPointData[0];
            }

            var result = new DrawPointData[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point == null)
                {
                    continue;
                }

                result[i] = new DrawPointData
                {
                    x = point.x,
                    y = point.y,
                    pressure = point.pressure,
                    timeMs = point.timeMs
                };
            }

            return result;
        }

        private static void ShufflePlayerIds(List<string> playerIds, long seed)
        {
            var random = new Random(unchecked((int)(seed ^ (seed >> 32))));
            for (var i = playerIds.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = playerIds[i];
                playerIds[i] = playerIds[swapIndex];
                playerIds[swapIndex] = temp;
            }
        }

        private static string MockWordText(int index)
        {
            switch (index % 6)
            {
                case 0: return "石狮";
                case 1: return "灯笼";
                case 2: return "瓦当";
                case 3: return "牌楼";
                case 4: return "铜钱";
                default: return "竹简";
            }
        }

        private void BroadcastPlayerJoined(MockRoomRuntime room, MockServerSession session)
        {
            BroadcastReliableToRoom(room.RoomId, MessageType.PlayerJoined, new PlayerJoined
            {
                playerId = session.PlayerId,
                displayName = session.DisplayName
            });
        }

        private void BroadcastRoomSnapshot(MockRoomRuntime room)
        {
            BroadcastReliableToRoom(room.RoomId, MessageType.RoomSnapshot, BuildSnapshot(room));
        }

        private void SendReliableToPlayer<TMessage>(
            MockRoomRuntime room,
            string playerId,
            string messageType,
            TMessage payload)
            where TMessage : IRealtimeMessage
        {
            if (room == null || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            foreach (var session in _sessions.Values)
            {
                if (session.Connected
                    && string.Equals(session.RoomId, room.RoomId, StringComparison.Ordinal)
                    && string.Equals(session.PlayerId, playerId, StringComparison.Ordinal))
                {
                    session.Client.EnqueueReliable(messageType, payload, room.RoomId);
                    return;
                }
            }
        }

        private RoomSnapshot BuildSnapshot(MockRoomRuntime room, string playerId = null)
        {
            var playerRuntimes = new List<MockRoomPlayerRuntime>(room.Players.Values);
            playerRuntimes.Sort((left, right) => left.JoinedAtMs.CompareTo(right.JoinedAtMs));
            var viewerRuntimes = new List<MockRoomPlayerRuntime>(room.Viewers.Values);
            viewerRuntimes.Sort((left, right) => left.JoinedAtMs.CompareTo(right.JoinedAtMs));

            var players = new RoomPlayerSnapshot[playerRuntimes.Count];
            var viewers = new RoomViewerSnapshot[viewerRuntimes.Count];
            var scores = new ScoreEntry[playerRuntimes.Count];
            var index = 0;

            foreach (var player in playerRuntimes)
            {
                players[index] = new RoomPlayerSnapshot
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName,
                    ready = player.Ready,
                    connected = player.Connected
                };
                scores[index] = new ScoreEntry
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName,
                    score = player.Score
                };
                index++;
            }

            index = 0;
            foreach (var viewer in viewerRuntimes)
            {
                viewers[index] = new RoomViewerSnapshot
                {
                    playerId = viewer.PlayerId,
                    displayName = viewer.DisplayName,
                    connected = viewer.Connected
                };
                index++;
            }

            return new RoomSnapshot
            {
                roomId = room.RoomId,
                modeId = room.ModeId,
                hostPlayerId = room.HostPlayerId,
                state = room.State,
                roundIndex = room.RoundIndex,
                maxRoundCount = room.MaxRoundCount,
                drawerPlayerId = room.DrawerPlayerId,
                stateEnterTimeMs = room.StateEnterTimeMs,
                stateDeadlineTimeMs = room.StateDeadlineTimeMs,
                players = players,
                viewers = viewers,
                scores = scores,
                currentTask = !string.IsNullOrWhiteSpace(playerId) && room.CurrentTasks.TryGetValue(playerId, out var task)
                    ? task
                    : null
            };
        }

        private void BroadcastReliableToRoom<TMessage>(string roomId, string messageType, TMessage payload)
            where TMessage : IRealtimeMessage
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            foreach (var session in _sessions.Values)
            {
                if (session.Connected && string.Equals(session.RoomId, roomId, StringComparison.Ordinal))
                {
                    session.Client.EnqueueReliable(messageType, payload, roomId);
                }
            }
        }

        private void BroadcastUnreliableToRoom<TMessage>(string roomId, string messageType, TMessage payload)
            where TMessage : IRealtimeMessage
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            foreach (var session in _sessions.Values)
            {
                if (session.Connected && session.UdpBound && string.Equals(session.RoomId, roomId, StringComparison.Ordinal))
                {
                    session.Client.EnqueueUnreliable(messageType, payload, roomId);
                }
            }
        }

        private void BroadcastUnreliableToViewers<TMessage>(string roomId, string messageType, TMessage payload)
            where TMessage : IRealtimeMessage
        {
            if (string.IsNullOrWhiteSpace(roomId) || !_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            foreach (var session in _sessions.Values)
            {
                if (session.Connected
                    && session.UdpBound
                    && session.IsViewer
                    && string.Equals(session.RoomId, roomId, StringComparison.Ordinal)
                    && room.Viewers.ContainsKey(session.PlayerId))
                {
                    session.Client.EnqueueUnreliable(messageType, payload, roomId);
                }
            }
        }

        private string CreateRoomId()
        {
            for (var retry = 0; retry < MaxRoomIdRetryCount; retry++)
            {
                var buffer = new char[6];
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = RoomIdChars[_random.Next(RoomIdChars.Length)];
                }

                var roomId = new string(buffer);
                if (!_rooms.ContainsKey(roomId))
                {
                    return roomId;
                }
            }

            return null;
        }

        private static string CreateUdpBindToken(string roomId, string playerId)
        {
            return string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId)
                ? null
                : $"mock-{roomId}-{playerId}";
        }

        private static ErrorMessage BuildError(MiniGameErrorCode errorCode)
        {
            return new ErrorMessage
            {
                errorCode = nameof(errorCode),
                messageKey = errorCode.ToString(),
                debugMessage = errorCode.ToString()
            };
        }
    }
}
