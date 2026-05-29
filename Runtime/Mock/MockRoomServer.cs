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
        private const int DrawTelephoneMinPlayers = 2;
        private const float MockDrawStageSeconds = 90f;
        private const float MockGuessStageSeconds = 45f;
        private const float MockReviewSecondsPerChain = 8f;
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

            if (room.Players.Count >= 10 && !room.Players.ContainsKey(session.PlayerId))
            {
                session.Client.EnqueueReliable(MessageType.JoinRoomResult, new JoinRoomResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.RoomFull)
                }, room.RoomId);
                return;
            }

            AddOrReconnectPlayer(room, session, request.displayName);
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
                return;
            }

            room.Players.Remove(session.PlayerId);
            session.RoomId = null;

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
                TryStartDrawTelephone(room);
            }
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

            if (!room.Players.ContainsKey(request.playerId))
            {
                session.Client.EnqueueReliable(MessageType.ReconnectResult, new ReconnectResult
                {
                    succeeded = false,
                    errorCode = nameof(MiniGameErrorCode.PermissionDenied)
                }, room.RoomId);
                return;
            }

            session.PlayerId = request.playerId;
            session.RoomId = room.RoomId;
            room.Players[request.playerId].Connected = true;

            session.Client.EnqueueReliable(MessageType.ReconnectResult, new ReconnectResult
            {
                succeeded = true,
                sessionId = session.SessionId,
                snapshot = BuildSnapshot(room, request.playerId)
            }, room.RoomId);

            BroadcastRoomSnapshot(room);
        }

        private void AddOrReconnectPlayer(MockRoomRuntime room, MockServerSession session, string displayName)
        {
            session.DisplayName = MiniGameIdentityUtility.NormalizeDisplayName(displayName ?? session.DisplayName);
            session.RoomId = room.RoomId;

            if (!room.Players.TryGetValue(session.PlayerId, out var player))
            {
                player = new MockRoomPlayerRuntime
                {
                    PlayerId = session.PlayerId
                };
                room.Players.Add(player.PlayerId, player);
            }

            player.DisplayName = session.DisplayName;
            player.Connected = true;
        }

        private void TryStartDrawTelephone(MockRoomRuntime room)
        {
            if (room == null
                || !string.Equals(room.ModeId, "draw_telephone", StringComparison.Ordinal)
                || !string.Equals(room.State, nameof(MiniGameRoomState.Lobby), StringComparison.Ordinal))
            {
                return;
            }

            if (room.Players.Count < DrawTelephoneMinPlayers || room.Players.Count % 2 != 0)
            {
                return;
            }

            foreach (var player in room.Players.Values)
            {
                if (!player.Ready || !player.Connected)
                {
                    return;
                }
            }

            StartDrawTelephone(room);
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
                score = string.Equals(chain.OriginalWordText, finalGuess, StringComparison.OrdinalIgnoreCase) ? 100 : 0
            };
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
            var players = new RoomPlayerSnapshot[room.Players.Count];
            var scores = new ScoreEntry[room.Players.Count];
            var index = 0;

            foreach (var pair in room.Players)
            {
                var player = pair.Value;
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

            return new RoomSnapshot
            {
                roomId = room.RoomId,
                modeId = room.ModeId,
                state = room.State,
                roundIndex = room.RoundIndex,
                maxRoundCount = room.MaxRoundCount,
                drawerPlayerId = room.DrawerPlayerId,
                stateEnterTimeMs = room.StateEnterTimeMs,
                stateDeadlineTimeMs = room.StateDeadlineTimeMs,
                players = players,
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
