// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Online.Chat;
using osu.Game.Online.Discovery;
using osu.Game.Online.LocalMultiplayer;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Beatmaps;

namespace osu.Game.Online.API
{
    public partial class APIAccess : CompositeComponent, IAPIProvider
    {
        // Local multiplayer server used in local-only mode.
        private LocalMultiplayerServer localServer;
        private LocalMultiplayerDiscovery localDiscovery;
        private LocalMultiplayerDirectServer localDirectServer;
        private readonly Dictionary<long, RemoteRoomReference> remoteDiscoveredRooms = new Dictionary<long, RemoteRoomReference>();

        private class RemoteRoomReference
        {
            public long RemoteRoomId;
            public IPEndPoint Endpoint;
            public DateTimeOffset LastSeen;
        }

        private static readonly TimeSpan remote_room_reference_ttl = TimeSpan.FromMinutes(2);

        private readonly OsuGameBase game;
        private readonly OsuConfigManager config;

        private readonly string versionHash;

        private readonly OAuth authentication;

        private readonly Queue<APIRequest> queue = new Queue<APIRequest>();

        public EndpointConfiguration Endpoints { get; }

        /// <summary>
        /// The API response version.
        /// See: https://osu.ppy.sh/docs/index.html#api-versions
        /// </summary>
        public int APIVersion { get; }

        public Exception LastLoginError { get; private set; }

        public string ProvidedUsername { get; private set; }

        public SessionVerificationMethod? SessionVerificationMethod { get; private set; }

        public string SecondFactorCode { get; private set; }

        private string password;

        public IBindable<APIUser> LocalUser => localUserState.User;

        public ILocalUserState LocalUserState => localUserState;
        private readonly LocalUserState localUserState;

        public INotificationsClient NotificationsClient { get; }

        // Expose the in-memory local server for consumers which need to drive local multiplayer behaviour.
        public LocalMultiplayerServer LocalMultiplayerServer => localServer ??= new LocalMultiplayerServer();

        public Language Language => game.CurrentLanguage.Value;

        protected bool HasLogin => authentication.Token.Value != null || (!string.IsNullOrEmpty(ProvidedUsername) && !string.IsNullOrEmpty(password));

        private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private readonly Logger log;

        public APIAccess(OsuGameBase game, OsuConfigManager config, EndpointConfiguration endpoints, string versionHash)
        {
            this.game = game;
            this.config = config;
            this.versionHash = versionHash;

            if (game.IsDeployedBuild)
                APIVersion = game.AssemblyVersion.Major * 10000 + game.AssemblyVersion.Minor;
            else
            {
                var now = DateTimeOffset.Now;
                APIVersion = now.Year * 10000 + now.Month * 100 + now.Day;
            }

            Endpoints = endpoints;
            NotificationsClient = setUpNotificationsClient();

            authentication = new OAuth(endpoints.APIClientID, endpoints.APIClientSecret, Endpoints.APIUrl);

            log = Logger.GetLogger(LoggingTarget.Network);
            log.Add($@"API endpoint root: {Endpoints.APIUrl}");
            log.Add($@"API request version: {APIVersion}");

            ProvidedUsername = config.Get<string>(OsuSetting.Username);

            authentication.TokenString = config.Get<string>(OsuSetting.Token);
            authentication.Token.ValueChanged += onTokenChanged;

            AddInternal(localUserState = new LocalUserState(this, config));

            if (HasLogin)
            {
                // Early call to ensure the local user / "logged in" state is correct immediately.
                localUserState.SetPlaceholderLocalUser(ProvidedUsername);

                // This is required so that Queue() requests during startup sequence don't fail due to "not logged in".
                state.Value = APIState.Connecting;
            }

            // If the experimental P2P flag is enabled, force this API into local-only mode.
            if (isP2PForced())
                LoginLocal(string.IsNullOrEmpty(ProvidedUsername) ? "Local" : ProvidedUsername);

            var thread = new Thread(run)
            {
                Name = "APIAccess",
                IsBackground = true
            };

            thread.Start();
        }

        private WebSocketNotificationsClientConnector setUpNotificationsClient()
        {
            var connector = new WebSocketNotificationsClientConnector(this);

            connector.MessageReceived += msg =>
            {
                switch (msg.Event)
                {
                    case @"verified":
                        if (state.Value == APIState.RequiresSecondFactorAuth)
                            state.Value = APIState.Online;
                        break;

                    case @"logout":
                        // 在本地-only 模式下忽略来自服务器的 logout 事件，避免远程推送导致本地实验性登录被撤销。
                        if (state.Value == APIState.Online && !IsLocalOnly)
                            Logout();

                        break;
                }
            };

            return connector;
        }

        private void onTokenChanged(ValueChangedEvent<OAuthToken> e) => config.SetValue(OsuSetting.Token, config.Get<bool>(OsuSetting.SavePassword) ? authentication.TokenString : string.Empty);

        void IAPIProvider.Schedule(Action action) => base.Schedule(action);

        public string AccessToken => authentication.RequestAccessToken();

        public Guid SessionIdentifier { get; } = Guid.NewGuid();

        /// <summary>
        /// Number of consecutive requests which failed due to network issues.
        /// </summary>
        private int failureCount;

        /// <summary>
        /// The main API thread loop, which will continue to run until the game is shut down.
        /// </summary>
        private void run()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (state.Value == APIState.Failing)
                {
                    // To recover from a failing state, falling through and running the full reconnection process seems safest for now.
                    // This could probably be replaced with a ping-style request if we want to avoid the reconnection overheads.
                    log.Add($@"{nameof(APIAccess)} is in a failing state, waiting a bit before we try again...");
                    Thread.Sleep(5000);
                }

                // Ensure that we have valid credentials.
                // If not, setting the offline state will allow the game to prompt the user to provide new credentials.
                if (!HasLogin)
                {
                    state.Value = APIState.Offline;
                    Thread.Sleep(50);
                    continue;
                }

                Debug.Assert(HasLogin);

                // Ensure that we are in an online state. If not, attempt to connect.
                if (state.Value != APIState.Online)
                {
                    attemptConnect();

                    if (state.Value != APIState.Online)
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                }

                // 如果无法获取有效的 access token，则通常会强制登出。
                // 对于本地-only 模式不需要 access token，因此在该模式下应避免登出操作。
                if (authentication.RequestAccessToken() == null)
                {
                    if (!IsLocalOnly)
                    {
                        Logout();
                        continue;
                    }
                }

                processQueuedRequests();
                // perform local housekeeping for local-only mode
                if (IsLocalOnly)
                    cleanupLocalRooms();

                Thread.Sleep(50);
            }
        }

        private void cleanupLocalRooms()
        {
            if (!IsLocalOnly)
                return;

            localServer?.CleanupExpired();
        }

        /// <summary>
        /// Dequeue from the queue and run each request synchronously until the queue is empty.
        /// </summary>
        private void processQueuedRequests()
        {
            while (true)
            {
                APIRequest req;

                lock (queue)
                {
                    if (queue.Count == 0) return;

                    req = queue.Dequeue();
                }

                handleRequest(req);
            }
        }

        /// <summary>
        /// From a non-connected state, perform a full connection flow, obtaining OAuth tokens and populating the local user and friends.
        /// </summary>
        /// <remarks>
        /// This method takes control of <see cref="state"/> and transitions from <see cref="APIState.Connecting"/> to either
        /// - <see cref="APIState.RequiresSecondFactorAuth"/> (pending 2fa)
        /// - <see cref="APIState.Online"/>  (successful connection)
        /// - <see cref="APIState.Failing"/> (failed connection but retrying)
        /// - <see cref="APIState.Offline"/> (failed and can't retry, clear credentials and require user interaction)
        /// </remarks>
        /// <returns>Whether the connection attempt was successful.</returns>
        private void attemptConnect()
        {
            if (LocalUser.IsDefault)
                Scheduler.Add(localUserState.SetPlaceholderLocalUser, ProvidedUsername, false);

            // save the username at this point, if the user requested for it to be.
            config.SetValue(OsuSetting.Username, config.Get<bool>(OsuSetting.SaveUsername) ? ProvidedUsername : string.Empty);

            if (!authentication.HasValidAccessToken && HasLogin)
            {
                state.Value = APIState.Connecting;
                LastLoginError = null;

                try
                {
                    authentication.AuthenticateWithLogin(ProvidedUsername, password);
                }
                catch (WebRequestFlushedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    //todo: this fails even on network-related issues. we should probably handle those differently.
                    LastLoginError = e;
                    log.Add($@"Login failed for username {ProvidedUsername} ({LastLoginError.Message})!");

                    Logout();
                    return;
                }
            }

            switch (state.Value)
            {
                case APIState.RequiresSecondFactorAuth:
                {
                    if (string.IsNullOrEmpty(SecondFactorCode))
                        return;

                    state.Value = APIState.Connecting;
                    LastLoginError = null;

                    var verificationRequest = new VerifySessionRequest(SecondFactorCode);

                    verificationRequest.Success += () => state.Value = APIState.Online;
                    verificationRequest.Failure += ex =>
                    {
                        state.Value = APIState.RequiresSecondFactorAuth;

                        if (verificationRequest.RequiredVerificationMethod != null)
                        {
                            SessionVerificationMethod = verificationRequest.RequiredVerificationMethod;
                            LastLoginError = new APIException($"Must use {SessionVerificationMethod.GetDescription().ToLowerInvariant()} to complete verification.", ex);
                        }
                        else
                        {
                            LastLoginError = ex;
                        }

                        SecondFactorCode = null;
                    };

                    if (!handleRequest(verificationRequest))
                    {
                        state.Value = APIState.Failing;
                        return;
                    }

                    if (state.Value != APIState.Online)
                        return;

                    break;
                }

                default:
                {
                    var userReq = new GetMeRequest();

                    userReq.Failure += ex =>
                    {
                        if (ex is APIException)
                        {
                            LastLoginError = ex;
                            log.Add($@"Login failed for username {ProvidedUsername} on user retrieval ({LastLoginError.Message})!");
                            Logout();
                        }
                        else if (ex is WebException webException && webException.Message == @"Unauthorized")
                        {
                            log.Add(@"Login no longer valid");
                            Logout();
                        }
                        else if (ex is not WebRequestFlushedException)
                        {
                            state.Value = APIState.Failing;
                        }
                    };

                    userReq.Success += me =>
                    {
                        Debug.Assert(ThreadSafety.IsUpdateThread);

                        localUserState.SetLocalUser(me);
                        SessionVerificationMethod = me.SessionVerificationMethod;
                        state.Value = SessionVerificationMethod == null ? APIState.Online : APIState.RequiresSecondFactorAuth;
                        failureCount = 0;
                    };

                    if (!handleRequest(userReq))
                    {
                        state.Value = APIState.Failing;
                        return;
                    }

                    break;
                }
            }

            // The Success callback event is fired on the main thread, so we should wait for that to run before proceeding.
            // Without this, we will end up circulating this Connecting loop multiple times and queueing up many web requests
            // before actually going online.
            while (State.Value == APIState.Connecting && !cancellationToken.IsCancellationRequested)
                Thread.Sleep(500);
        }

        public void Perform(APIRequest request)
        {
            try
            {
                if (IsLocalOnly)
                {
                    request.AttachAPI(this);
                    // Handle known request types locally to provide a full offline multiplayer experience.
                    if (handleLocalRequest(request))
                        return;
                    // If not handled locally, fail deterministically.
                    request.Fail(new WebException("User not logged in"));
                    return;
                }

                request.AttachAPI(this);
                request.Perform();
            }
            catch (Exception e)
            {
                // todo: fix exception handling
                request.Fail(e);
            }
        }

        /// <summary>
        /// Handle requests locally when in local-only mode. Returns true if handled.
        /// </summary>
        private bool handleLocalRequest(APIRequest request)
        {
            if (localServer == null)
                return false;

            switch (request)
            {
                case GetRoomsRequest getRooms:
                    pruneExpiredRemoteReferences();
                    getRooms.TriggerSuccess(localServer.GetRooms().ToList());
                    return true;

                case ListChannelsRequest listChannelsReq:
                    listChannelsReq.TriggerSuccess(localServer.GetChannels().ToList());
                    return true;

                case GetMessagesRequest getMessagesReq:
                    getMessagesReq.TriggerSuccess(localServer.GetChannelMessages(getMessagesReq.Channel.Id).ToList());
                    return true;

                case PostMessageRequest postMessageReq:
                    postMessageReq.TriggerSuccess(localServer.PostChannelMessage(postMessageReq.Message, LocalUser.Value));
                    return true;

                case CreateRoomRequest createReq:
                {
                    var created = localServer.CreateRoom(createReq.Room, LocalUser.Value);
                    createReq.TriggerSuccess(created);

                    try
                    {
                        localDiscovery?.BroadcastRoom(new LocalMultiplayerDiscovery.DiscoveredRoom
                        {
                            Name = createReq.Room.Name,
                            RoomID = created.RoomID.HasValue ? (int)created.RoomID.Value : 0,
                            HostName = LocalUser.Value?.Username ?? ProvidedUsername,
                            IsP2P = createReq.Room.IsP2P,
                            ControlPort = LocalMultiplayerDirectServer.DEFAULT_PORT,
                            Timestamp = DateTimeOffset.Now,
                        });
                    }
                    catch { }

                    return true;
                }

                case GetRoomRequest getRoomReq:
                {
                    var r = localServer.GetRoom(getRoomReq.RoomId);

                    if (r == null && tryGetRemoteRoom(getRoomReq.RoomId, out Room remoteRoom, out _))
                    {
                        localServer.UpsertRoom(remoteRoom);
                        r = remoteRoom;
                    }

                    if (r == null)
                    {
                        getRoomReq.TriggerFailure(new InvalidOperationException("Room not found."));
                        return true;
                    }

                    getRoomReq.TriggerSuccess(r);
                    return true;
                }

                case GetUsersRequest getUsersReq:
                {
                    APIUser local = LocalUser.Value;

                    getUsersReq.TriggerSuccess(new GetUsersResponse
                    {
                        Users = getUsersReq.UserIds
                                           .Distinct()
                                           .Select(id => local != null && local.Id == id
                                               ? local
                                               : new APIUser
                                               {
                                                   Id = id,
                                                   Username = $"User {id}",
                                               })
                                           .ToList()
                    });

                    return true;
                }

                case LookupUsersRequest lookupUsersReq:
                {
                    APIUser local = LocalUser.Value;

                    lookupUsersReq.TriggerSuccess(new GetUsersResponse
                    {
                        Users = lookupUsersReq.UserIds
                                              .Distinct()
                                              .Select(id => local != null && local.Id == id
                                                  ? local
                                                  : new APIUser
                                                  {
                                                      Id = id,
                                                      Username = $"User {id}",
                                                  })
                                              .ToList()
                    });

                    return true;
                }

                case GetBeatmapsRequest getBeatmapsReq:
                {
                    List<APIBeatmap> beatmaps = getBeatmapsReq.BeatmapIds
                                                              .Distinct()
                                                              .Select(id => tryGetKnownBeatmapInfo(id, out IBeatmapInfo info)
                                                                  ? createApiBeatmap(info)
                                                                  : createFallbackApiBeatmap(id))
                                                              .ToList();

                    getBeatmapsReq.TriggerSuccess(new GetBeatmapsResponse
                    {
                        Beatmaps = beatmaps
                    });
                    return true;
                }

                case GetBeatmapSetRequest getBeatmapSetReq:
                {
                    IBeatmapInfo sourceBeatmap;

                    if (getBeatmapSetReq.Type == BeatmapSetLookupType.BeatmapId)
                    {
                        if (!tryGetKnownBeatmapInfo(getBeatmapSetReq.ID, out sourceBeatmap))
                            sourceBeatmap = null;
                    }
                    else
                    {
                        sourceBeatmap = localServer.GetRooms()
                                                   .SelectMany(r => r.Playlist)
                                                   .Select(p => p.Beatmap)
                                                   .FirstOrDefault(b => b.BeatmapSet?.OnlineID == getBeatmapSetReq.ID);
                    }

                    getBeatmapSetReq.TriggerSuccess(sourceBeatmap != null
                        ? createApiBeatmapSet(sourceBeatmap)
                        : createFallbackApiBeatmapSet(getBeatmapSetReq.ID));

                    return true;
                }

                case JoinRoomRequest joinReq:
                {
                    string remoteJoinError = null;

                    if (joinReq.Room.RoomID != null
                        && tryGetRemoteRoomReference(joinReq.Room.RoomID.Value, out RemoteRoomReference remoteRef)
                        && LocalMultiplayerDirectClient.TryJoinRoom(remoteRef.Endpoint, remoteRef.RemoteRoomId, joinReq.Password, LocalUser.Value, out Room remoteJoined, out remoteJoinError))
                    {
                        // keep synthetic room id locally while preserving remote id in ChannelId for follow-up calls.
                        remoteJoined.ChannelId = (int)remoteRef.RemoteRoomId;
                        remoteJoined.RoomID = joinReq.Room.RoomID;

                        localServer.UpsertRoom(remoteJoined);
                        joinReq.TriggerSuccess(remoteJoined);
                        return true;
                    }

                    var (success, room, error) = localServer.JoinRoom(joinReq.Room, LocalUser.Value, joinReq.Password);

                    if (!success)
                    {
                        joinReq.TriggerFailure(new InvalidOperationException(error ?? remoteJoinError ?? "Join failed."));
                        return true;
                    }

                    joinReq.TriggerSuccess(room!);
                    return true;
                }

                case PartRoomRequest partReq:
                {
                    Room partRoom = partReqRoom(partReq);

                    if (partRoom.RoomID != null
                        && tryGetRemoteRoomReference(partRoom.RoomID.Value, out RemoteRoomReference remoteRef))
                    {
                        LocalMultiplayerDirectClient.TryPartRoom(remoteRef.Endpoint, remoteRef.RemoteRoomId, LocalUser.Value, out _);
                    }

                    localServer.PartRoom(partRoom, LocalUser.Value);
                    partReq.TriggerSuccess();
                    return true;
                }

                case CreateRoomScoreRequest createScoreReq:
                {
                    long roomId = getPrivateField<long>(createScoreReq, "roomId");
                    long playlistItemId = getPrivateField<long>(createScoreReq, "playlistItemId");
                    createScoreReq.TriggerSuccess(localServer.CreateRoomScore(roomId, playlistItemId));
                    return true;
                }

                case GetRoomLeaderboardRequest leaderboardReq:
                {
                    long roomId = getPrivateField<long>(leaderboardReq, "roomId");
                    leaderboardReq.TriggerSuccess(localServer.GetLeaderboard(roomId));
                    return true;
                }

                case IndexPlaylistScoresRequest indexReq:
                    indexReq.TriggerSuccess(localServer.IndexPlaylistScores(indexReq.RoomId, indexReq.PlaylistItemId));
                    return true;

                case SubmitRoomScoreRequest submitReq:
                {
                    long scoreId = getPrivateField<long>(submitReq, "ScoreId");
                    long roomId = getPrivateField<long>(submitReq, "roomId");
                    long playlistItemId = getPrivateField<long>(submitReq, "playlistItemId");
                    var score = localServer.SubmitRoomScore(submitReq.Score, scoreId, roomId, playlistItemId, LocalUser.Value);
                    submitReq.TriggerSuccess(score);
                    return true;
                }

                case ChatAckRequest ackReq:
                    ackReq.TriggerSuccess(new ChatAckResponse());
                    return true;

                default:
                    return false;
            }
        }

        // helper to extract room reference for PartRoomRequest; PartRoomRequest stores room private field, so use reflection as fallback.
        private Room partReqRoom(PartRoomRequest partReq)
        {
            try
            {
                var field = typeof(PartRoomRequest).GetField("room", BindingFlags.NonPublic | BindingFlags.Instance);

                if (field?.GetValue(partReq) is Room room)
                    return room;
            }
            catch
            {
            }

            return new Room();
        }

        private T getPrivateField<T>(object obj, string name)
        {
            var field = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Field '{name}' not found on type {obj.GetType()}");

            return (T)field.GetValue(obj)!;
        }

        private bool tryGetKnownBeatmapInfo(int beatmapId, out IBeatmapInfo beatmapInfo)
        {
            beatmapInfo = localServer.GetRooms()
                                     .SelectMany(r => r.Playlist)
                                     .Select(p => p.Beatmap)
                                     .FirstOrDefault(b => b.OnlineID == beatmapId);

            return beatmapInfo != null;
        }

        private APIBeatmap createApiBeatmap(IBeatmapInfo source)
        {
            int setId = source.BeatmapSet?.OnlineID ?? source.OnlineID;

            APIBeatmapSet set = createApiBeatmapSet(source);

            return new APIBeatmap
            {
                OnlineID = source.OnlineID,
                OnlineBeatmapSetID = setId,
                Status = source is BeatmapInfo beatmapInfo ? beatmapInfo.Status : BeatmapOnlineStatus.Ranked,
                Checksum = source.MD5Hash,
                AuthorID = source.Metadata.Author.OnlineID,
                RulesetID = source.Ruleset.OnlineID,
                StarRating = source.StarRating,
                DifficultyName = source.DifficultyName,
                BeatmapSet = set,
            };
        }

        private APIBeatmapSet createApiBeatmapSet(IBeatmapInfo source)
        {
            int setId = source.BeatmapSet?.OnlineID ?? source.OnlineID;

            BeatmapSetOnlineCovers covers = createCovers(setId);

            return new APIBeatmapSet
            {
                OnlineID = setId,
                Status = BeatmapOnlineStatus.Ranked,
                Covers = covers,
                Title = source.Metadata.Title,
                TitleUnicode = source.Metadata.TitleUnicode,
                Artist = source.Metadata.Artist,
                ArtistUnicode = source.Metadata.ArtistUnicode,
                Author = new APIUser
                {
                    Id = source.Metadata.Author.OnlineID,
                    Username = source.Metadata.Author.Username
                },
                Beatmaps = Array.Empty<APIBeatmap>(),
            };
        }

        private APIBeatmap createFallbackApiBeatmap(int beatmapId)
        {
            APIBeatmapSet set = createFallbackApiBeatmapSet(beatmapId);

            return new APIBeatmap
            {
                OnlineID = beatmapId,
                OnlineBeatmapSetID = set.OnlineID,
                Status = BeatmapOnlineStatus.Ranked,
                BeatmapSet = set,
            };
        }

        private APIBeatmapSet createFallbackApiBeatmapSet(int setId) => new APIBeatmapSet
        {
            OnlineID = setId,
            Status = BeatmapOnlineStatus.Ranked,
            Covers = createCovers(setId),
            Beatmaps = Array.Empty<APIBeatmap>(),
        };

        private BeatmapSetOnlineCovers createCovers(int setId)
        {
            if (setId <= 0)
                return default;

            string baseUrl = $"https://assets.ppy.sh/beatmaps/{setId}/covers";

            return new BeatmapSetOnlineCovers
            {
                Cover = $"{baseUrl}/cover.jpg",
                CoverLowRes = $"{baseUrl}/cover.jpg",
                Card = $"{baseUrl}/card.jpg",
                CardLowRes = $"{baseUrl}/card.jpg",
                List = $"{baseUrl}/list.jpg",
                ListLowRes = $"{baseUrl}/list.jpg",
            };
        }

        public Task PerformAsync(APIRequest request) =>
            Task.Factory.StartNew(() => Perform(request), TaskCreationOptions.LongRunning);

        public void Login(string username, string password)
        {
            Debug.Assert(State.Value == APIState.Offline);

            if (isP2PForced())
            {
                LoginLocal(string.IsNullOrEmpty(username) ? "Local" : username);
                return;
            }

            ProvidedUsername = username;
            this.password = password;
            IsLocalOnly = false;
        }

        public void LoginLocal(string username)
        {
            NotificationsClient.Disconnect();

            ProvidedUsername = username;
            password = "__local__";
            IsLocalOnly = true;

            Schedule(() => localUserState.SetPlaceholderLocalUser(ProvidedUsername, true));
            LastLoginError = null;
            state.Value = APIState.Online;

            // start local server and LAN discovery in local-only mode immediately so
            // requests can be handled synchronously after calling LoginLocal.
            try
            {
                localServer ??= new LocalMultiplayerServer();
                localDirectServer ??= new LocalMultiplayerDirectServer(localServer);

                if (localDiscovery == null)
                {
                    localDiscovery = new LocalMultiplayerDiscovery();
                    localDiscovery.RoomReceived += onRemoteRoomDiscovered;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start local server/discovery: {ex.Message}", LoggingTarget.Network, LogLevel.Debug);
            }
        }

        private void onRemoteRoomDiscovered(LocalMultiplayerDiscovery.DiscoveredRoom discovered)
        {
            // schedule onto API thread to mutate localRooms safely
            Schedule(() =>
            {
                try
                {
                    long syntheticId = -generateRoomKey(discovered.AdvertiserEndpoint, discovered.RoomID);

                    int controlPort = discovered.ControlPort > 0 ? discovered.ControlPort : LocalMultiplayerDirectServer.DEFAULT_PORT;
                    IPEndPoint endpoint = discovered.AdvertiserEndpoint == null
                        ? null
                        : new IPEndPoint(discovered.AdvertiserEndpoint.Address, controlPort);

                    if (endpoint != null)
                    {
                        remoteDiscoveredRooms[syntheticId] = new RemoteRoomReference
                        {
                            RemoteRoomId = discovered.RoomID,
                            Endpoint = endpoint,
                            LastSeen = DateTimeOffset.UtcNow,
                        };
                    }

                    // create a synthetic room representation for discovered remote host
                    var room = new Room
                    {
                        RoomID = syntheticId,
                        Name = discovered.Name,
                        Host = new APIUser { Username = discovered.HostName },
                        IsP2P = discovered.IsP2P,
                        StartDate = discovered.Timestamp,
                        EndDate = discovered.Timestamp.Add(remote_room_reference_ttl),
                        ParticipantCount = 1,
                    };

                    // upsert into local server by negative synthetic key
                    localServer?.UpsertRoom(room);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error injecting discovered LAN room: {ex}", LoggingTarget.Network, LogLevel.Debug);
                }
            });
        }

        private int generateRoomKey(IPEndPoint ep, int remoteRoomId)
        {
            if (ep == null) return (int)DateTimeOffset.Now.ToUnixTimeSeconds();

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + ep.Address.GetHashCode();
                hash = hash * 23 + ep.Port;
                hash = hash * 23 + remoteRoomId;
                return Math.Abs(hash);
            }
        }

        public void AuthenticateSecondFactor(string code)
        {
            Debug.Assert(State.Value == APIState.RequiresSecondFactorAuth);

            SecondFactorCode = code;
        }

        public IHubClientConnector GetHubConnector(string clientName, string endpoint) =>
            new HubClientConnector(clientName, endpoint, this, versionHash);

        public IChatClient GetChatClient() => new WebSocketChatClient(this);

        public RegistrationRequest.RegistrationRequestErrors CreateAccount(string email, string username, string password)
        {
            Debug.Assert(State.Value == APIState.Offline);

            var req = new RegistrationRequest
            {
                Url = $@"{Endpoints.APIUrl}/users",
                Method = HttpMethod.Post,
                Username = username,
                Email = email,
                Password = password
            };

            try
            {
                req.Perform();
            }
            catch (Exception e)
            {
                try
                {
                    return JObject.Parse(req.GetResponseString().AsNonNull()).SelectToken(@"form_error", true).AsNonNull().ToObject<RegistrationRequest.RegistrationRequestErrors>();
                }
                catch
                {
                    try
                    {
                        // attempt to parse a non-form error message
                        var response = JObject.Parse(req.GetResponseString().AsNonNull());

                        string redirect = (string)response.SelectToken(@"url", false);
                        string message = (string)response.SelectToken(@"error", false);

                        if (!string.IsNullOrEmpty(redirect) || !string.IsNullOrEmpty(message))
                        {
                            return new RegistrationRequest.RegistrationRequestErrors
                            {
                                Redirect = redirect,
                                Message = message,
                            };
                        }

                        // if we couldn't deserialize the error message let's throw the original exception outwards.
                        e.Rethrow();
                    }
                    catch
                    {
                        // if we couldn't deserialize the error message let's throw the original exception outwards.
                        e.Rethrow();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Handle a single API request.
        /// Ensures all exceptions are caught and dealt with correctly.
        /// </summary>
        /// <param name="req">The request.</param>
        /// <returns>true if the request succeeded.</returns>
        private bool handleRequest(APIRequest req)
        {
            try
            {
                req.AttachAPI(this);
                req.Perform();

                if (req.CompletionState != APIRequestCompletionState.Completed)
                    return false;

                // Reset failure count if this request succeeded.
                failureCount = 0;
                return true;
            }
            catch (HttpRequestException re)
            {
                log.Add($"{nameof(HttpRequestException)} while performing request {req}: {re.Message}");
                handleFailure();
                return false;
            }
            catch (SocketException se)
            {
                log.Add($"{nameof(SocketException)} while performing request {req}: {se.Message}");
                handleFailure();
                return false;
            }
            catch (WebException we)
            {
                log.Add($"{nameof(WebException)} while performing request {req}: {we.Message}");
                handleWebException(we);
                return false;
            }
            catch (WebRequestFlushedException wrf)
            {
                log.Add(wrf.Message);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while handling an API request.");
                return false;
            }
        }

        private readonly Bindable<APIState> state = new Bindable<APIState>();

        /// <summary>
        /// The current connectivity state of the API.
        /// </summary>
        public IBindable<APIState> State => state;

        private void handleWebException(WebException we)
        {
            HttpStatusCode statusCode = (we.Response as HttpWebResponse)?.StatusCode
                                        ?? (we.Status == WebExceptionStatus.UnknownError ? HttpStatusCode.NotAcceptable : HttpStatusCode.RequestTimeout);

            // special cases for un-typed but useful message responses.
            switch (we.Message)
            {
                case "Unauthorized":
                case "Forbidden":
                    statusCode = HttpStatusCode.Unauthorized;
                    break;
            }

            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                    Logout();
                    break;

                case HttpStatusCode.RequestTimeout:
                    handleFailure();
                    break;
            }
        }

        private void handleFailure()
        {
            failureCount++;
            log.Add($@"API failure count is now {failureCount}");

            if (failureCount >= 3)
            {
                state.Value = APIState.Failing;
                flushQueue();
            }
        }

        public bool IsLoggedIn => State.Value > APIState.Offline;

        public bool IsLocalOnly { get; private set; }

        public void Queue(APIRequest request)
        {
            lock (queue)
            {
                // P2P experiments run only through local/direct path (no online API dependency).
                if (isP2PForced())
                    ensureLocalModeStarted();

                // If a user attempts to create an experimental P2P room while we don't have
                // a valid authenticated online session, switch to local-only mode so the
                // request can be handled locally instead of being sent unauthenticated.
                if (request is CreateRoomRequest cr && cr.Room.IsP2P)
                {
                    // Force local-only mode for experimental P2P room creation so that
                    // no server-side authentication or checks are performed.
                    LoginLocal(string.IsNullOrEmpty(ProvidedUsername) ? "Local" : ProvidedUsername);
                }

                request.AttachAPI(this);

                // If we're in local-only mode and the request is allowed to be handled locally,
                // perform it immediately rather than enqueueing so the caller (UI) receives
                // a prompt response and doesn't remain stuck waiting for network activity.
                if (IsLocalOnly && request.AllowLocal)
                {
                    // Perform synchronously on the API thread loop semantics.
                    Perform(request);
                    return;
                }

                if (IsLocalOnly && !request.AllowLocal)
                {
                    // Avoid silently dropping requests in local-only mode which can lead to callers
                    // waiting indefinitely for completion (e.g. lounge polling). Fail the
                    // request immediately so callers receive a deterministic failure callback.
                    request.Fail(new WebException(@"User not logged in"));
                    return;
                }

                if (state.Value == APIState.Offline)
                {
                    if (request.AllowLocal)
                    {
                        // If the request may be handled locally, ensure local-only mode is started
                        // and execute the request immediately on the API thread.
                        ensureLocalModeStarted();
                        PerformAsync(request);
                        return;
                    }

                    request.Fail(new WebException(@"User not logged in"));
                    return;
                }

                queue.Enqueue(request);
            }
        }

        private static bool isP2PForced()
            => GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.ExperimentalP2P);

        private void ensureLocalModeStarted()
        {
            if (IsLocalOnly)
                return;

            // emulate the effect of LoginLocal without requiring explicit user action
            NotificationsClient.Disconnect();

            if (string.IsNullOrEmpty(ProvidedUsername))
                ProvidedUsername = config.Get<string>(OsuSetting.Username) ?? "Local";

            password = "__local__";
            IsLocalOnly = true;

            // set placeholder local user immediately on main thread
            Schedule(() => localUserState.SetPlaceholderLocalUser(ProvidedUsername, true));
            LastLoginError = null;
            state.Value = APIState.Online;

            // start local server and discovery
            Schedule(() =>
            {
                try
                {
                    localServer ??= new LocalMultiplayerServer();
                    localDirectServer ??= new LocalMultiplayerDirectServer(localServer);

                    if (localDiscovery == null)
                    {
                        localDiscovery = new LocalMultiplayerDiscovery();
                        localDiscovery.RoomReceived += onRemoteRoomDiscovered;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to start local server/discovery: {ex.Message}", LoggingTarget.Network, LogLevel.Debug);
                }
            });
        }

        private void flushQueue(bool failOldRequests = true)
        {
            lock (queue)
            {
                var oldQueueRequests = queue.ToArray();

                queue.Clear();

                if (failOldRequests)
                {
                    foreach (var req in oldQueueRequests)
                        req.Fail(new WebRequestFlushedException(state.Value));
                }
            }
        }

        private bool tryGetRemoteRoomReference(long localSyntheticRoomId, out RemoteRoomReference remote)
        {
            pruneExpiredRemoteReferences();

            if (!remoteDiscoveredRooms.TryGetValue(localSyntheticRoomId, out remote))
                return false;

            remote.LastSeen = DateTimeOffset.UtcNow;
            return true;
        }

        private bool tryGetRemoteRoom(long localSyntheticRoomId, out Room room, out string error)
        {
            room = null;
            error = null;

            if (!tryGetRemoteRoomReference(localSyntheticRoomId, out RemoteRoomReference remote))
                return false;

            if (!LocalMultiplayerDirectClient.TryGetRoom(remote.Endpoint, remote.RemoteRoomId, out Room fetched, out error) || fetched == null)
                return false;

            fetched.ChannelId = (int)remote.RemoteRoomId;
            fetched.RoomID = localSyntheticRoomId;
            room = fetched;
            return true;
        }

        public bool TryDiscoverRemoteHost(string addressOrEndpoint, out string error, out int discoveredCount)
        {
            error = null;
            discoveredCount = 0;

            if (string.IsNullOrWhiteSpace(addressOrEndpoint))
            {
                error = "Address is empty.";
                return false;
            }

            ensureLocalModeStarted();

            string address = addressOrEndpoint.Trim();
            int port = LocalMultiplayerDirectServer.DEFAULT_PORT;

            int idx = address.LastIndexOf(':');

            if (idx > 0 && idx < address.Length - 1 && int.TryParse(address[(idx + 1)..], out int parsedPort))
            {
                port = parsedPort;
                address = address[..idx];
            }

            if (!IPAddress.TryParse(address, out IPAddress ip))
            {
                error = "Invalid IP address format. Expected x.x.x.x[:port].";
                return false;
            }

            var endpoint = new IPEndPoint(ip, port);

            if (!LocalMultiplayerDirectClient.TryListRooms(endpoint, out Room[] rooms, out error))
                return false;

            foreach (Room remoteRoom in rooms ?? Array.Empty<Room>())
            {
                if (remoteRoom?.RoomID == null)
                    continue;

                long syntheticId = -generateRoomKey(endpoint, (int)remoteRoom.RoomID.Value);

                remoteDiscoveredRooms[syntheticId] = new RemoteRoomReference
                {
                    RemoteRoomId = remoteRoom.RoomID.Value,
                    Endpoint = endpoint,
                    LastSeen = DateTimeOffset.UtcNow,
                };

                remoteRoom.ChannelId = (int)remoteRoom.RoomID.Value;
                remoteRoom.RoomID = syntheticId;

                localServer.UpsertRoom(remoteRoom);
                discoveredCount++;
            }

            return true;
        }

        private void pruneExpiredRemoteReferences()
        {
            if (remoteDiscoveredRooms.Count == 0)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (long roomId in remoteDiscoveredRooms.Where(kv => now - kv.Value.LastSeen > remote_room_reference_ttl).Select(kv => kv.Key).ToArray())
                remoteDiscoveredRooms.Remove(roomId);

            localServer?.CleanupExpired();
        }

        public void Logout()
        {
            password = null;
            SecondFactorCode = null;
            authentication.Clear();

            remoteDiscoveredRooms.Clear();

            if (localDiscovery != null)
            {
                localDiscovery.Dispose();
                localDiscovery = null;
            }

            if (localDirectServer != null)
            {
                localDirectServer.Dispose();
                localDirectServer = null;
            }

            localUserState.ClearLocalUser();
            IsLocalOnly = false;

            state.Value = APIState.Offline;
            flushQueue();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            try
            {
                localDiscovery?.Dispose();
            }
            catch { }

            try
            {
                localDirectServer?.Dispose();
            }
            catch { }

            flushQueue();
            cancellationToken.Cancel();
        }

        internal class WebRequestFlushedException : Exception
        {
            public WebRequestFlushedException(APIState state)
                : base($@"Request failed from flush operation (state {state})")
            {
            }
        }
    }

    internal class GuestUser : APIUser
    {
        public GuestUser()
        {
            Username = @"Guest";
            Id = SYSTEM_USER_ID;
        }
    }

    public enum APIState
    {
        /// <summary>
        /// We cannot login (not enough credentials).
        /// </summary>
        Offline,

        /// <summary>
        /// We are having connectivity issues.
        /// </summary>
        Failing,

        /// <summary>
        /// Waiting on second factor authentication.
        /// </summary>
        RequiresSecondFactorAuth,

        /// <summary>
        /// We are in the process of (re-)connecting.
        /// </summary>
        Connecting,

        /// <summary>
        /// We are online.
        /// </summary>
        Online
    }
}
