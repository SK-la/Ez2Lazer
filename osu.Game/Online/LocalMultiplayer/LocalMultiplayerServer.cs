// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Online.Chat;
using osu.Game.Online.Rooms;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.API;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Online.LocalMultiplayer
{
    /// <summary>
    /// In-memory local multiplayer server used when running in local-only mode.
    /// All methods are expected to be called from the API thread.
    /// </summary>
    public class LocalMultiplayerServer
    {
        private readonly List<Room> rooms = new List<Room>();
        private readonly Dictionary<long, List<Message>> channelMessages = new Dictionary<long, List<Message>>();
        private int nextRoomId = 1;
        private long nextMessageId = 1;

        public IReadOnlyList<Room> GetRooms()
        {
            return rooms.Select(r =>
            {
                var copy = new Room();
                copy.CopyFrom(r);
                return copy;
            }).ToArray();
        }

        public APICreatedRoom CreateRoom(Room room, APIUser host)
        {
            room.RoomID = nextRoomId++;
            room.ChannelId = (int)room.RoomID.Value;
            room.StartDate = DateTimeOffset.Now;
            room.EndDate = DateTimeOffset.Now.AddHours(2);
            room.Host = host;
            room.ParticipantCount = 1;
            room.RecentParticipants = new[] { host };

            ensureChannel(room.ChannelId);

            var stored = new Room();
            stored.CopyFrom(room);
            rooms.Add(stored);

            var created = new APICreatedRoom();
            created.CopyFrom(room);
            return created;
        }

        /// <summary>
        /// Upsert an externally discovered room (e.g. via LAN discovery).
        /// If a room with the same RoomID exists, it will be replaced.
        /// </summary>
        public void UpsertRoom(Room room)
        {
            if (room.RoomID != null && room.ChannelId == 0)
                room.ChannelId = (int)room.RoomID.Value;

            if (room.ChannelId != 0)
                ensureChannel(room.ChannelId);

            var existing = rooms.SingleOrDefault(r => r.RoomID == room.RoomID);

            if (existing != null)
                existing.CopyFrom(room);
            else
            {
                var stored = new Room();
                stored.CopyFrom(room);
                rooms.Add(stored);
            }
        }

        public Room? GetRoom(long id)
        {
            var found = rooms.SingleOrDefault(r => r.RoomID == id);
            if (found == null) return null;

            if (found.ChannelId == 0 && found.RoomID != null)
            {
                found.ChannelId = (int)found.RoomID.Value;
                ensureChannel(found.ChannelId);
            }

            var copy = new Room();
            copy.CopyFrom(found);
            return copy;
        }

        public IReadOnlyList<Channel> GetChannels()
        {
            return rooms.Where(r => r.RoomID != null)
                        .Select(r =>
                        {
                            long channelId = r.ChannelId != 0 ? r.ChannelId : r.RoomID!.Value;
                            ensureChannel(channelId);

                            return new Channel
                            {
                                Id = channelId,
                                Name = $"#lazermp_{r.RoomID.Value}",
                                Topic = r.Name,
                                Type = ChannelType.Multiplayer,
                                LastMessageId = channelMessages.TryGetValue(channelId, out List<Message>? msgs) && msgs.Count > 0
                                    ? msgs[^1].Id
                                    : 0,
                            };
                        })
                        .ToArray();
        }

        public IReadOnlyList<Message> GetChannelMessages(long channelId)
        {
            if (!channelMessages.TryGetValue(channelId, out List<Message>? messages))
                return Array.Empty<Message>();

            return messages.Select(cloneMessage).ToArray();
        }

        public Message PostChannelMessage(Message incoming, APIUser sender)
        {
            ensureChannel(incoming.ChannelId);

            var posted = new Message(nextMessageId++)
            {
                ChannelId = incoming.ChannelId,
                Content = incoming.Content,
                IsAction = incoming.IsAction,
                Timestamp = DateTimeOffset.Now,
                Sender = sender,
                Uuid = incoming.Uuid,
            };

            channelMessages[incoming.ChannelId].Add(posted);
            return cloneMessage(posted);
        }

        public (bool success, Room? room, string? error) JoinRoom(Room requested, APIUser user, string? password)
        {
            var room = rooms.SingleOrDefault(r => r.RoomID == requested.RoomID);
            if (room == null) return (false, null, "Room not found.");

            if (!string.IsNullOrEmpty(room.Password) && room.Password != password)
                return (false, null, "Invalid password.");

            room.ParticipantCount = Math.Max(1, room.ParticipantCount + 1);
            var recent = room.RecentParticipants.ToList();
            recent.Insert(0, user);
            room.RecentParticipants = recent;

            var copy = new Room();
            copy.CopyFrom(room);
            return (true, copy, null);
        }

        public void PartRoom(Room requested, APIUser user)
        {
            var room = rooms.SingleOrDefault(r => r.RoomID == requested.RoomID);
            if (room != null)
                room.ParticipantCount = Math.Max(0, room.ParticipantCount - 1);
        }

        public APIScoreToken CreateRoomScore(long roomId, long playlistItemId)
        {
            return new APIScoreToken { ID = 1 };
        }

        public MultiplayerScore SubmitRoomScore(SoloScoreInfo info, long scoreId, long roomId, long playlistItemId, APIUser user)
        {
            return new MultiplayerScore
            {
                ID = 1,
                Accuracy = info.Accuracy,
                EndedAt = DateTimeOffset.Now,
                Passed = info.Passed,
                Rank = info.Rank,
                MaxCombo = info.MaxCombo,
                TotalScore = info.TotalScore,
                User = user,
                Statistics = info.Statistics ?? new Dictionary<HitResult, int>(),
            };
        }

        public APILeaderboard GetLeaderboard(long roomId)
        {
            var lb = new APILeaderboard
            {
                Leaderboard = new List<APIUserScoreAggregate>(),
                UserScore = null
            };

            if (rooms.Count > 0)
            {
                lb.Leaderboard.Add(new APIUserScoreAggregate
                {
                    TotalScore = 1000000,
                    TotalAttempts = 5,
                    CompletedBeatmaps = 2,
                    User = rooms[0].Host,
                    Accuracy = 1,
                });
                lb.Leaderboard.Add(new APIUserScoreAggregate
                {
                    TotalScore = 200000,
                    TotalAttempts = 1,
                    CompletedBeatmaps = 1,
                    User = new APIUser { Username = "CPU Player" },
                    Accuracy = 0.7,
                });

                lb.UserScore = new APIUserScoreAggregate
                {
                    TotalScore = 800000,
                    TotalAttempts = 3,
                    CompletedBeatmaps = 1,
                    User = rooms[0].Host,
                    Accuracy = 0.91,
                };
            }

            return lb;
        }

        public IndexedMultiplayerScores IndexPlaylistScores(long roomId, long playlistItemId)
        {
            var res = new IndexedMultiplayerScores();
            res.Scores.Add(new MultiplayerScore
            {
                ID = 1,
                Accuracy = 1,
                Position = 1,
                EndedAt = DateTimeOffset.Now,
                Passed = true,
                Rank = ScoreRank.S,
                MaxCombo = 1000,
                TotalScore = 1000000,
                User = rooms.Count > 0 ? rooms[0].Host : new APIUser { Username = "Local" },
                Statistics = new Dictionary<HitResult, int>(),
            });

            res.Scores.Add(new MultiplayerScore
            {
                ID = 2,
                Accuracy = 0.7,
                Position = 2,
                EndedAt = DateTimeOffset.Now,
                Passed = true,
                Rank = ScoreRank.B,
                MaxCombo = 100,
                TotalScore = 200000,
                User = new APIUser { Username = "CPU Player" },
                Statistics = new Dictionary<HitResult, int>(),
            });

            res.UserScore = res.Scores[0];
            return res;
        }

        public void CleanupExpired()
        {
            var now = DateTimeOffset.Now;
            rooms.RemoveAll(r => r.EndDate != null && r.EndDate <= now);
        }

        private void ensureChannel(long channelId)
        {
            if (!channelMessages.ContainsKey(channelId))
                channelMessages[channelId] = new List<Message>();
        }

        private static Message cloneMessage(Message source)
        {
            return new Message(source.Id)
            {
                ChannelId = source.ChannelId,
                IsAction = source.IsAction,
                Timestamp = source.Timestamp,
                Content = source.Content,
                Sender = source.Sender,
                Uuid = source.Uuid,
            };
        }
    }
}
