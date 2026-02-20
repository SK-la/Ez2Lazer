// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace osu.Game.Online.Discovery
{
    /// <summary>
    /// Simple LAN discovery using UDP multicast for local multiplayer room announcements.
    /// Not a full mDNS implementation - lightweight PoC for LAN discovery.
    /// </summary>
    public class LocalMultiplayerDiscovery : IDisposable
    {
        private const string multicast_address = "239.0.0.222";
        private const int multicast_port = 5325;

        private readonly UdpClient listener;
        private readonly UdpClient broadcaster;
        private readonly IPEndPoint groupEndpoint;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public Action<DiscoveredRoom>? RoomReceived;

        public LocalMultiplayerDiscovery()
        {
            groupEndpoint = new IPEndPoint(IPAddress.Parse(multicast_address), multicast_port);

            listener = new UdpClient(AddressFamily.InterNetwork);
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.ExclusiveAddressUse = false;
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, multicast_port));
            listener.JoinMulticastGroup(IPAddress.Parse(multicast_address));

            broadcaster = new UdpClient();
            broadcaster.MulticastLoopback = false;

            Task.Run(() => receiveLoop(cts.Token));
        }

        public void BroadcastRoom(DiscoveredRoom room)
        {
            try
            {
                string json = JsonConvert.SerializeObject(room);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                broadcaster.Send(bytes, bytes.Length, groupEndpoint);
            }
            catch (Exception e)
            {
                Logger.Log($"[LocalDiscovery] Broadcast failed: {e}", LoggingTarget.Network, LogLevel.Debug);
            }
        }

        private async Task receiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await listener.ReceiveAsync(token).ConfigureAwait(false);
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    DiscoveredRoom room = JsonConvert.DeserializeObject<DiscoveredRoom>(json);

                    if (room != null)
                    {
                        // attach endpoint info
                        room.AdvertiserEndpoint = result.RemoteEndPoint;
                        RoomReceived?.Invoke(room);
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    Logger.Log($"[LocalDiscovery] Receive failed: {e}", LoggingTarget.Network, LogLevel.Debug);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                cts.Cancel();
                listener.DropMulticastGroup(IPAddress.Parse(multicast_address));
            }
            catch { }

            listener.Close();
            broadcaster.Close();
            cts.Dispose();
        }

        public class DiscoveredRoom
        {
            public string Name { get; set; } = string.Empty;
            public int RoomID { get; set; }
            public string HostName { get; set; } = string.Empty;
            public bool IsP2P { get; set; }
            public int ControlPort { get; set; } = osu.Game.Online.LocalMultiplayer.LocalMultiplayerDirectServer.DEFAULT_PORT;
            public IPEndPoint? AdvertiserEndpoint { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }
    }
}
