// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// A multiplayer room.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class MultiplayerRoom
    {
        /// <summary>
        /// The ID of the room, used for database persistence.
        /// </summary>
        [Key(0)]
        public readonly long RoomID;

        /// <summary>
        /// The current state of the room (ie. whether it is in progress or otherwise).
        /// </summary>
        [Key(1)]
        public MultiplayerRoomState State { get; set; }

        /// <summary>
        /// All currently enforced game settings for this room.
        /// </summary>
        [Key(2)]
        public MultiplayerRoomSettings Settings { get; set; } = new MultiplayerRoomSettings();

        /// <summary>
        /// All users currently in this room.
        /// </summary>
        [Key(3)]
        public IList<MultiplayerRoomUser> Users { get; set; } = new List<MultiplayerRoomUser>();

        /// <summary>
        /// The host of this room, in control of changing room settings.
        /// </summary>
        [Key(4)]
        public MultiplayerRoomUser? Host { get; set; }

        [Key(5)]
        public MatchRoomState? MatchState { get; set; }

        [Key(6)]
        public IList<MultiplayerPlaylistItem> Playlist { get; set; } = new List<MultiplayerPlaylistItem>();

        /// <summary>
        /// The currently running countdowns.
        /// </summary>
        [Key(7)]
        public IList<MultiplayerCountdown> ActiveCountdowns { get; set; } = new List<MultiplayerCountdown>();

        /// <summary>
        /// The ID of the chat channel for the room.
        /// </summary>
        [Key(8)]
        public int ChannelID { get; set; }

        /// <summary>
        /// Whether this room is using experimental P2P networking (host-to-peer).
        /// </summary>
        [Key(9)]
        public bool IsP2P { get; set; }

        /// <summary>
        /// Optional host signalling payload (e.g. SDP or other rendezvous data) stored on the room for short-term exchange.
        /// </summary>
        [Key(10)]
        public string? HostSignalling { get; set; }

        /// <summary>
        /// Optional per-peer signalling payloads uploaded by joiners keyed by user id.
        /// </summary>
        [Key(11)]
        public IDictionary<int, string> PeerSignalling { get; set; } = new Dictionary<int, string>();

        [JsonConstructor]
        [SerializationConstructor]
        public MultiplayerRoom(long roomId)
        {
            RoomID = roomId;
        }

        public MultiplayerRoom(Room room)
        {
            RoomID = room.RoomID ?? 0;
            ChannelID = room.ChannelId;
            Settings = new MultiplayerRoomSettings(room);
            Host = room.Host != null ? new MultiplayerRoomUser(room.Host.OnlineID) : null;
            Playlist = room.Playlist.Select(p => new MultiplayerPlaylistItem(p)).ToArray();

            // Map optional P2P metadata from lounge room representation.
            IsP2P = room.IsP2P;
            HostSignalling = room.HostSignalling;
            PeerSignalling = room.PeerSignalling != null ? new Dictionary<int, string>(room.PeerSignalling) : new Dictionary<int, string>();
        }

        /// <summary>
        /// Retrieves the active <see cref="MultiplayerPlaylistItem"/> as determined by the room's current settings.
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public MultiplayerPlaylistItem CurrentPlaylistItem => Playlist.Single(item => item.ID == Settings.PlaylistItemId);

        /// <summary>
        /// Determines whether a user is able to add playlist items to this room.
        /// </summary>
        /// <param name="user">The user to check.</param>
        public bool CanAddPlaylistItems(MultiplayerRoomUser user) => user.Equals(Host) || Settings.QueueMode != QueueMode.HostOnly;

        public override string ToString() => $"RoomID:{RoomID} Host:{Host?.UserID} Users:{Users.Count} State:{State} Settings: [{Settings}]";
    }
}
