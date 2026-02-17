// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Online.Multiplayer.MatchTypes.Matchmaking;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;
using osu.Game.Online.LocalMultiplayer;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// A lightweight local multiplayer client backed by the in-memory <see cref="LocalMultiplayerServer"/>.
    /// This provides the same behaviour as the test client but wired to the runtime local server so UI
    /// and gameplay code can operate against a realistic `MultiplayerClient` when running in local-only mode.
    /// </summary>
    public partial class LocalMultiplayerClient : MultiplayerClient
    {
        public override IBindable<bool> IsConnected => isConnected;
        private readonly BindableBool isConnected = new BindableBool(true);

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private LocalMultiplayerServer? localServer;

        // Server-side authoritative representation used by this client.
        private Room? serverApiRoom;
        private MultiplayerRoom? serverRoom;

        private long lastPlaylistItemId;
        private int lastCountdownId;

        private readonly Dictionary<int, long> matchmakingUserPicks = new Dictionary<int, long>();

        [BackgroundDependencyLoader]
        private void load()
        {
            if (api is APIAccess access)
                localServer = access.LocalMultiplayerServer;

            localServer ??= new LocalMultiplayerServer();
        }

        protected override async Task<MultiplayerRoom> CreateRoomInternal(MultiplayerRoom room)
        {
            if (localServer == null)
                throw new InvalidOperationException("Local server not available");

            // Mirror TestMultiplayerClient behaviour: create an API Room and add server-side room.
            var apiRoom = new Room(room)
            {
                Type = room.Settings.MatchType == MatchType.Playlists
                    ? MatchType.HeadToHead
                    : room.Settings.MatchType
            };

            var created = localServer.CreateRoom(apiRoom, api.LocalUser.Value);

            return await JoinRoomInternal(created.RoomID.GetValueOrDefault(), room.Settings.Password).ConfigureAwait(false);
        }

        protected override Task<MultiplayerRoom> JoinRoomInternal(long roomId, string? password = null)
        {
            if (localServer == null)
                throw new InvalidOperationException("Local server not available");

            var apiRoom = localServer.GetRoom(roomId);
            if (apiRoom == null)
                throw new InvalidOperationException("Room not found.");

            var (success, joinedRoom, error) = localServer.JoinRoom(apiRoom, api.LocalUser.Value, password);
            if (!success || joinedRoom == null)
                throw new InvalidOperationException(error ?? "Failed to join room.");

            // Construct the multiplayer room returned from server data.
            var m = new MultiplayerRoom(joinedRoom.RoomID ?? 0)
            {
                Settings = new MultiplayerRoomSettings(joinedRoom),
                Playlist = joinedRoom.Playlist.Select(p => new MultiplayerPlaylistItem(p)).ToList(),
                Host = joinedRoom.Host != null ? new MultiplayerRoomUser(joinedRoom.Host.OnlineID) { User = joinedRoom.Host } : null
            };

            // basic population of users (local only will only have the host initially)
            var localUser = new MultiplayerRoomUser(api.LocalUser.Value.Id) { User = api.LocalUser.Value };
            m.Users.Add(localUser);

            // store server-side references
            serverApiRoom = joinedRoom;
            serverRoom = m;

            // set up playlist item id and countdown ids
            lastPlaylistItemId = joinedRoom.Playlist?.Max(pi => pi.ID) ?? 0;

            return Task.FromResult(m);
        }

        protected override Task LeaveRoomInternal()
        {
            // inform server
            if (localServer != null && serverApiRoom != null)
                localServer.PartRoom(serverApiRoom, api.LocalUser.Value);

            serverApiRoom = null;
            serverRoom = null;
            return Task.CompletedTask;
        }

        public override Task DisconnectInternal()
        {
            isConnected.Value = false;
            return Task.CompletedTask;
        }

        public override Task InvitePlayer(int userId) => Task.CompletedTask;

        public override Task TransferHost(int userId)
        {
            MultiplayerRoom room = serverRoom;

            if (room == null)
                return Task.CompletedTask;

            room.Host = room.Users.Single(u => u.UserID == userId);
            return ((IMultiplayerClient)this).HostChanged(userId);
        }

        public override Task KickUser(int userId)
        {
            if (serverRoom == null)
                return Task.CompletedTask;

            var user = serverRoom.Users.Single(u => u.UserID == userId);
            serverRoom.Users.Remove(user);
            return ((IMultiplayerClient)this).UserKicked(user);
        }

        public override Task ChangeSettings(MultiplayerRoomSettings settings)
        {
            if (serverRoom == null)
                return Task.CompletedTask;

            settings = clone(settings);
            settings.PlaylistItemId = serverRoom.Settings.PlaylistItemId;
            serverRoom.Settings = settings;

            ((IMultiplayerClient)this).SettingsChanged(settings);

            return Task.CompletedTask;
        }

        public override Task ChangeState(MultiplayerUserState newState)
        {
            if (serverRoom == null)
                return Task.CompletedTask;

            var local = serverRoom.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);
            if (local == null) return Task.CompletedTask;

            local.State = newState;
            ((IMultiplayerClient)this).UserStateChanged(local.UserID, local.State);
            return Task.CompletedTask;
        }

        public override Task ChangeBeatmapAvailability(BeatmapAvailability newBeatmapAvailability)
        {
            MultiplayerRoom room = serverRoom;

            if (room == null)
                return Task.CompletedTask;

            var local = room.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);

            if (local == null)
                return Task.CompletedTask;

            local.BeatmapAvailability = newBeatmapAvailability;
            ((IMultiplayerClient)this).UserBeatmapAvailabilityChanged(local.UserID, newBeatmapAvailability);
            return Task.CompletedTask;
        }

        public override Task ChangeUserStyle(int? beatmapId, int? rulesetId)
        {
            MultiplayerRoom room = serverRoom;

            if (room == null)
                return Task.CompletedTask;

            var local = room.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);

            if (local == null)
                return Task.CompletedTask;

            local.BeatmapId = beatmapId;
            local.RulesetId = rulesetId;
            ((IMultiplayerClient)this).UserStyleChanged(local.UserID, beatmapId, rulesetId);
            return Task.CompletedTask;
        }

        public override Task ChangeUserMods(IEnumerable<APIMod> newMods)
        {
            MultiplayerRoom room = serverRoom;

            if (room == null)
                return Task.CompletedTask;

            var local = room.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);

            if (local == null)
                return Task.CompletedTask;

            local.Mods = newMods.ToArray();
            ((IMultiplayerClient)this).UserModsChanged(local.UserID, local.Mods);
            return Task.CompletedTask;
        }

        public override Task SendMatchRequest(MatchUserRequest request)
        {
            // handle only a few requests for now (countdown start/stop)
            switch (request)
            {
                case StartMatchCountdownRequest startCountdown:
                    return StartCountdown(new MatchStartCountdown { TimeRemaining = startCountdown.Duration });

                case StopCountdownRequest stopCountdown:
                    if (serverRoom == null)
                        return Task.CompletedTask;

                    var countdown = serverRoom.ActiveCountdowns.FirstOrDefault(c => c.ID == stopCountdown.ID);
                    return countdown == null ? Task.CompletedTask : StopCountdown(countdown);
            }

            return Task.CompletedTask;
        }

        public override Task StartMatch()
        {
            if (serverRoom == null) return Task.CompletedTask;

            serverRoom.State = MultiplayerRoomState.WaitingForLoad;
            foreach (var user in serverRoom.Users.Where(u => u.State == MultiplayerUserState.Ready))
                user.State = MultiplayerUserState.WaitingForLoad;

            return ((IMultiplayerClient)this).LoadRequested();
        }

        public override Task AbortGameplay()
        {
            var local = serverRoom?.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);
            if (local != null) local.State = MultiplayerUserState.Idle;
            return Task.CompletedTask;
        }

        public override Task AbortMatch()
        {
            var local = serverRoom?.Users.SingleOrDefault(u => u.User?.Id == api.LocalUser.Value.Id);
            if (local != null) local.State = MultiplayerUserState.Idle;
            return ((IMultiplayerClient)this).GameplayAborted(GameplayAbortReason.HostAbortedTheMatch);
        }

        public override async Task AddPlaylistItem(MultiplayerPlaylistItem item)
        {
            if (serverRoom == null) throw new InvalidOperationException("Not in a room");

            item.ID = ++lastPlaylistItemId;
            item.OwnerID = api.LocalUser.Value.OnlineID;
            serverRoom.Playlist.Add(item);
            await ((IMultiplayerClient)this).PlaylistItemAdded(item).ConfigureAwait(false);
        }

        public override Task EditPlaylistItem(MultiplayerPlaylistItem item)
        {
            if (serverRoom == null) throw new InvalidOperationException("Not in a room");

            var existing = serverRoom.Playlist.SingleOrDefault(i => i.ID == item.ID);
            if (existing == null) throw new InvalidOperationException("Item does not exist");

            serverRoom.Playlist[serverRoom.Playlist.IndexOf(existing)] = item;
            return ((IMultiplayerClient)this).PlaylistItemChanged(item);
        }

        public override Task RemovePlaylistItem(long playlistItemId)
        {
            if (serverRoom == null) throw new InvalidOperationException("Not in a room");

            var item = serverRoom.Playlist.First(i => i.ID == playlistItemId);
            serverRoom.Playlist.Remove(item);
            return ((IMultiplayerClient)this).PlaylistItemRemoved(playlistItemId);
        }

        public override Task VoteToSkipIntro()
        {
            // simple immediate vote
            return ((IMultiplayerClient)this).UserVotedToSkipIntro(api.LocalUser.Value.OnlineID, true);
        }

        public async Task StartCountdown(MultiplayerCountdown countdown)
        {
            if (serverRoom == null) return;

            countdown.ID = ++lastCountdownId;
            serverRoom.ActiveCountdowns.Add(countdown);
            await ((IMultiplayerClient)this).MatchEvent(new CountdownStartedEvent(countdown)).ConfigureAwait(false);
        }

        public async Task StopCountdown(MultiplayerCountdown countdown)
        {
            if (serverRoom == null) return;

            serverRoom.ActiveCountdowns.Remove(serverRoom.ActiveCountdowns.First(c => c.ID == countdown.ID));
            await ((IMultiplayerClient)this).MatchEvent(new CountdownStoppedEvent(countdown.ID)).ConfigureAwait(false);
        }

        public override Task<MatchmakingPool[]> GetMatchmakingPoolsOfType(MatchmakingPoolType type)
        {
            return Task.FromResult<MatchmakingPool[]>(
            [
                new MatchmakingPool { Id = 0, RulesetId = 0 },
                new MatchmakingPool { Id = 1, RulesetId = 1 },
                new MatchmakingPool { Id = 2, RulesetId = 2 },
                new MatchmakingPool { Id = 3, RulesetId = 3, Variant = 4 },
                new MatchmakingPool { Id = 4, RulesetId = 3, Variant = 7 },
            ]);
        }

        public override Task MatchmakingJoinLobby() => Task.CompletedTask;

        public override Task MatchmakingLeaveLobby() => Task.CompletedTask;

        public override async Task MatchmakingJoinQueue(int poolId)
        {
            await ((IMatchmakingClient)this).MatchmakingQueueJoined().ConfigureAwait(false);
            await ((IMatchmakingClient)this).MatchmakingQueueStatusChanged(new MatchmakingQueueStatus.Searching()).ConfigureAwait(false);
        }

        public override Task MatchmakingLeaveQueue() => ((IMatchmakingClient)this).MatchmakingQueueLeft();

        public override Task MatchmakingAcceptInvitation() => Task.CompletedTask;

        public override Task MatchmakingDeclineInvitation() => Task.CompletedTask;

        public override Task MatchmakingToggleSelection(long playlistItemId)
            => MatchmakingToggleUserSelection(api.LocalUser.Value.OnlineID, playlistItemId);

        public override Task MatchmakingSkipToNextStage() => Task.CompletedTask;

        public async Task MatchmakingToggleUserSelection(int userId, long playlistItemId)
        {
            if (matchmakingUserPicks.TryGetValue(userId, out long existingId))
            {
                if (existingId == playlistItemId)
                    return;

                await ((IMatchmakingClient)this).MatchmakingItemDeselected(clone(userId), clone(existingId)).ConfigureAwait(false);
            }

            matchmakingUserPicks[userId] = playlistItemId;

            await ((IMatchmakingClient)this).MatchmakingItemSelected(clone(userId), clone(playlistItemId)).ConfigureAwait(false);
        }

        private T clone<T>(T incoming)
        {
            byte[] serialized = MessagePackSerializer.Serialize(typeof(T), incoming, SignalRUnionWorkaroundResolver.OPTIONS);
            return MessagePackSerializer.Deserialize<T>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);
        }
    }
}
