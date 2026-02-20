// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.LocalMultiplayer
{
    /// <summary>
    /// Lightweight direct bridge for local-only multiplayer clients.
    /// Allows one client to query/join a room hosted by another client without remote server infrastructure.
    /// </summary>
    public class LocalMultiplayerDirectServer : IDisposable
    {
        public const int DEFAULT_PORT = 5326;
        private const int protocol_version = 1;
        private const int max_payload_length = 16 * 1024;

        private readonly LocalMultiplayerServer localServer;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public LocalMultiplayerDirectServer(LocalMultiplayerServer localServer, int port = DEFAULT_PORT)
        {
            this.localServer = localServer;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Task.Run(() => acceptLoop(cancellation.Token));
        }

        private async Task acceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    _ = Task.Run(() => handleClient(client), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log($"[LocalDirect] accept failed: {ex}", LoggingTarget.Network, LogLevel.Debug);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }
        }

        private async Task handleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                stream.ReadTimeout = 3000;
                stream.WriteTimeout = 3000;

                using var writer = new StreamWriter(stream);
                writer.AutoFlush = true;

                try
                {
                    string line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line))
                        return;

                    if (line.Length > max_payload_length)
                    {
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(new Response
                        {
                            Success = false,
                            Error = "Payload too large."
                        })).ConfigureAwait(false);
                        return;
                    }

                    Request req = JsonConvert.DeserializeObject<Request>(line);
                    if (req == null)
                        return;

                    if (req.Version != protocol_version)
                    {
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(new Response
                        {
                            Success = false,
                            Error = $"Unsupported protocol version '{req.Version}'."
                        })).ConfigureAwait(false);
                        return;
                    }

                    Response response = processRequest(req);
                    await writer.WriteLineAsync(JsonConvert.SerializeObject(response)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[LocalDirect] handle failed: {ex}", LoggingTarget.Network, LogLevel.Debug);
                }
            }
        }

        private Response processRequest(Request req)
        {
            try
            {
                switch (req.Op)
                {
                    case "list_rooms":
                        return new Response
                        {
                            Success = true,
                            Rooms = localServer.GetRooms().ToArray()
                        };

                    case "get_room":
                    {
                        Room room = localServer.GetRoom(req.RoomId);
                        return room == null
                            ? new Response { Success = false, Error = "Room not found." }
                            : new Response { Success = true, Room = room };
                    }

                    case "join_room":
                    {
                        var requested = new Room { RoomID = req.RoomId };
                        var user = new APIUser
                        {
                            Id = req.UserId,
                            Username = string.IsNullOrEmpty(req.Username) ? "Guest" : req.Username
                        };

                        var (success, room, error) = localServer.JoinRoom(requested, user, req.Password);

                        return success
                            ? new Response { Success = true, Room = room }
                            : new Response { Success = false, Error = error ?? "Join failed." };
                    }

                    case "part_room":
                    {
                        var requested = new Room { RoomID = req.RoomId };
                        var user = new APIUser
                        {
                            Id = req.UserId,
                            Username = string.IsNullOrEmpty(req.Username) ? "Guest" : req.Username
                        };

                        localServer.PartRoom(requested, user);
                        return new Response { Success = true };
                    }

                    default:
                        return new Response { Success = false, Error = $"Unknown op '{req.Op}'" };
                }
            }
            catch (Exception ex)
            {
                return new Response { Success = false, Error = ex.Message };
            }
        }

        public void Dispose()
        {
            cancellation.Cancel();

            try
            {
                listener.Stop();
            }
            catch
            {
            }

            cancellation.Dispose();
        }

        private class Request
        {
            public int Version = protocol_version;
            public string Op = string.Empty;
            public long RoomId;
            public string Password = string.Empty;
            public int UserId;
            public string Username = string.Empty;
        }

        private class Response
        {
            public bool Success;
            public string Error = string.Empty;
            public Room Room = new Room();
            public Room[] Rooms = Array.Empty<Room>();
        }
    }

    public static class LocalMultiplayerDirectClient
    {
        private const int protocol_version = 1;

        public static bool TryJoinRoom(IPEndPoint endpoint, long roomId, string password, APIUser user, out Room room, out string error)
            => tryRequest(endpoint, new Request
            {
                Version = protocol_version,
                Op = "join_room",
                RoomId = roomId,
                Password = password,
                UserId = user?.Id ?? 0,
                Username = user?.Username
            }, out room, out _, out error);

        public static bool TryGetRoom(IPEndPoint endpoint, long roomId, out Room room, out string error)
            => tryRequest(endpoint, new Request { Version = protocol_version, Op = "get_room", RoomId = roomId }, out room, out _, out error);

        public static bool TryListRooms(IPEndPoint endpoint, out Room[] rooms, out string error)
        {
            bool success = tryRequest(endpoint, new Request { Version = protocol_version, Op = "list_rooms" }, out _, out rooms, out error);
            rooms ??= Array.Empty<Room>();
            return success;
        }

        public static bool TryPartRoom(IPEndPoint endpoint, long roomId, APIUser user, out string error)
            => tryRequest(endpoint, new Request
            {
                Version = protocol_version,
                Op = "part_room",
                RoomId = roomId,
                UserId = user?.Id ?? 0,
                Username = user?.Username
            }, out _, out _, out error);

        private static bool tryRequest(IPEndPoint endpoint, Request req, out Room room, out Room[] rooms, out string error)
        {
            room = null;
            rooms = null;
            error = null;

            try
            {
                if (endpoint == null)
                {
                    error = "Endpoint is null.";
                    return false;
                }

                using (var tcp = new TcpClient())
                {
                    tcp.ReceiveTimeout = 3000;
                    tcp.SendTimeout = 3000;

                    var connectTask = tcp.ConnectAsync(endpoint.Address, endpoint.Port);

                    if (!connectTask.Wait(3000))
                    {
                        error = "Connect timeout";
                        return false;
                    }

                    using (var stream = tcp.GetStream())
                    using (var reader = new StreamReader(stream))
                    {
                        stream.ReadTimeout = 3000;
                        stream.WriteTimeout = 3000;

                        using var writer = new StreamWriter(stream);
                        writer.AutoFlush = true;

                        writer.WriteLine(JsonConvert.SerializeObject(req));
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            error = "No response";
                            return false;
                        }

                        Response response = JsonConvert.DeserializeObject<Response>(line);

                        if (response == null)
                        {
                            error = "Invalid response";
                            return false;
                        }

                        if (!response.Success)
                        {
                            error = response.Error ?? "Remote request failed";
                            return false;
                        }

                        room = response.Room;
                        rooms = response.Rooms;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private class Request
        {
            public int Version;
            public string Op = string.Empty;
            public long RoomId;
            public string Password = string.Empty;
            public int UserId;
            public string Username = string.Empty;
        }

        private class Response
        {
            public bool Success;
            public string Error = string.Empty;
            public Room Room = new Room();
            public Room[] Rooms = Array.Empty<Room>();
        }
    }
}
