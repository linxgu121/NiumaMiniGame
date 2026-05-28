using System;
using System.Collections.Generic;
using NiumaMiniGame.Chat;
using NiumaMiniGame.Drawing;
using NiumaMiniGame.Enum;
using NiumaMiniGame.Gift;
using NiumaMiniGame.Network;
using NiumaMiniGame.Protocol;
using NiumaMiniGame.Room;

namespace NiumaMiniGame.Mock
{
    /// <summary>
    /// 本地内存房间服务端。
    /// 它用于 Unity 前端在不启动 Java 后端时验证网络协议和房间基础流程。
    /// </summary>
    public sealed class MockRoomServer
    {
        private const int MaxRoomIdRetryCount = 10;
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
                    BroadcastUnreliableToRoom(session.RoomId, MessageType.StrokePointBatch, batch);
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
                snapshot = BuildSnapshot(room)
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

        private RoomSnapshot BuildSnapshot(MockRoomRuntime room)
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
                scores = scores
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
