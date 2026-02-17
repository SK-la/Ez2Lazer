// WebRtcManager: runtime-probing PoC for Microsoft.MixedReality.WebRTC with placeholder fallback.
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Online.Multiplayer.WebRtc
{
    public class WebRtcManager : IDisposable
    {
        private bool initialized;
        private object? peerConnection;
        private Type? peerType;

        public WebRtcManager()
        {
        }

        private Assembly? findMrWebRtcAssembly()
        {
            // try to find loaded assembly containing Microsoft.MixedReality.WebRTC
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name?.StartsWith("Microsoft.MixedReality.WebRTC") == true || a.GetTypes().Any(t => t.FullName?.StartsWith("Microsoft.MixedReality.WebRTC") == true));
            return asm;
        }

        public async Task InitializeAsync()
        {
            if (initialized)
                return;

            try
            {
                var asm = findMrWebRtcAssembly();
                if (asm == null)
                    throw new InvalidOperationException("MR.WebRTC assembly not loaded");

                peerType = asm.GetType("Microsoft.MixedReality.WebRTC.PeerConnection") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "PeerConnection");
                if (peerType == null)
                    throw new InvalidOperationException("PeerConnection type not found in MR.WebRTC assembly");

                peerConnection = Activator.CreateInstance(peerType);

                // Try to call InitializeAsync() with no args or with a config if available.
                var initMethod = peerType.GetMethod("InitializeAsync", BindingFlags.Instance | BindingFlags.Public);
                if (initMethod != null)
                {
                    var parameters = initMethod.GetParameters();
                    object? taskObj;
                    if (parameters.Length == 0)
                        taskObj = initMethod.Invoke(peerConnection, Array.Empty<object>());
                    else
                    {
                        // attempt to construct a PeerConnectionConfiguration with a STUN server if available
                        var configType = asm.GetType("Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "PeerConnectionConfiguration");
                        object? config = null;
                        if (configType != null)
                        {
                            config = Activator.CreateInstance(configType);
                            // try set IceServers property if exists
                            var iceProp = configType.GetProperty("IceServers");
                            if (iceProp != null)
                            {
                                try
                                {
                                    var iceList = iceProp.GetValue(config);
                                    // best-effort: leave default if cannot set
                                }
                                catch { }
                            }
                        }

                        taskObj = initMethod.Invoke(peerConnection, config == null ? null : new[] { config });
                    }

                    if (taskObj is Task t)
                        await t.ConfigureAwait(false);
                }

                initialized = true;
            }
            catch
            {
                // fallback to placeholder behaviour by marking initialized to true
                initialized = true;
                peerConnection = null;
                peerType = null;
            }
        }

        public async Task<string> CreateOfferAsync()
        {
            if (!initialized) throw new InvalidOperationException("Not initialized");

            // If peerConnection was created, try to call CreateOffer or CreateOfferAsync via reflection.
            if (peerConnection != null && peerType != null)
            {
                try
                {
                    // create a TaskCompletionSource to wait for the local SDP event if present
                    var tcs = new TaskCompletionSource<string>();

                    // attempt to subscribe to a local sdp ready event via reflection
                    var evt = peerType.GetEvent("LocalSdpReadytoSend") ?? peerType.GetEvent("LocalSdpReadyToSend");
                    if (evt != null)
                    {
                        // create handler that takes (string) or (object) and sets result
                        Action<object, string> handler = (_, sdp) => tcs.TrySetResult(sdp);
                        // build delegate of correct type
                        var handlerDelegate = Delegate.CreateDelegate(evt.EventHandlerType!, handler.Target, handler.Method);
                        evt.AddEventHandler(peerConnection, handlerDelegate);
                    }

                    // invoke CreateOffer or CreateOfferAsync
                    var method = peerType.GetMethod("CreateOffer") ?? peerType.GetMethod("CreateOfferAsync");
                    object? res = null;
                    if (method != null)
                    {
                        res = method.Invoke(peerConnection, null);
                        if (res is Task task)
                            await task.ConfigureAwait(false);
                    }

                    // wait for the sdp or timeout
                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000)).ConfigureAwait(false);
                    if (completed == tcs.Task)
                        return await tcs.Task.ConfigureAwait(false);
                }
                catch
                {
                    // fallback
                }
            }

            return $"offer-placeholder:{Guid.NewGuid()}";
        }

        public async Task<string> CreateAnswerAsync(string offer)
        {
            if (!initialized) throw new InvalidOperationException("Not initialized");

            if (peerConnection != null && peerType != null)
            {
                try
                {
                    // try to set remote description then create answer
                    var setRemote = peerType.GetMethod("SetRemoteDescriptionAsync") ?? peerType.GetMethod("SetRemoteDescription");
                    if (setRemote != null)
                    {
                        // build an SdpMessage if available
                        var asm = peerType.Assembly;
                        var sdpType = asm.GetType("Microsoft.MixedReality.WebRTC.SdpMessage") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "SdpMessage");
                        object? sdpObj = null;
                        if (sdpType != null)
                        {
                            sdpObj = Activator.CreateInstance(sdpType);
                            var contentProp = sdpType.GetProperty("Content");
                            var typeProp = sdpType.GetProperty("Type");
                            if (contentProp != null) contentProp.SetValue(sdpObj, offer);
                            if (typeProp != null)
                            {
                                // try set type to Offer
                                var enumType = typeProp.PropertyType;
                                var offerVal = Enum.GetValues(enumType).Cast<object?>().FirstOrDefault(v => v.ToString()?.ToLower().Contains("offer") == true);
                                if (offerVal != null) typeProp.SetValue(sdpObj, offerVal);
                            }
                        }

                        var setRes = setRemote.Invoke(peerConnection, sdpObj == null ? new object[] { offer } : new object[] { sdpObj });
                        if (setRes is Task setTask) setTask.GetAwaiter().GetResult();
                    }

                    // create answer similar to CreateOfferAsync
                    var tcs = new TaskCompletionSource<string>();
                    var evt = peerType.GetEvent("LocalSdpReadytoSend") ?? peerType.GetEvent("LocalSdpReadyToSend");
                    if (evt != null)
                    {
                        Action<object, string> handler = (_, sdp) => tcs.TrySetResult(sdp);
                        var handlerDelegate = Delegate.CreateDelegate(evt.EventHandlerType!, handler.Target, handler.Method);
                        evt.AddEventHandler(peerConnection, handlerDelegate);
                    }

                    var method = peerType.GetMethod("CreateAnswer") ?? peerType.GetMethod("CreateAnswerAsync");
                    if (method != null)
                    {
                        var res = method.Invoke(peerConnection, null);
                        if (res is Task t) await t.ConfigureAwait(false);
                    }

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000)).ConfigureAwait(false);
                    if (completed == tcs.Task)
                        return await tcs.Task.ConfigureAwait(false);
                }
                catch
                {
                }
            }

            return $"answer-placeholder:{Guid.NewGuid()}";
        }

        public Task SetRemoteAnswerAsync(string answer)
        {
            if (!initialized) throw new InvalidOperationException("Not initialized");

            if (peerConnection != null && peerType != null)
            {
                try
                {
                    var setRemote = peerType.GetMethod("SetRemoteDescriptionAsync") ?? peerType.GetMethod("SetRemoteDescription");
                    if (setRemote != null)
                    {
                        var asm = peerType.Assembly;
                        var sdpType = asm.GetType("Microsoft.MixedReality.WebRTC.SdpMessage") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "SdpMessage");
                        object? sdpObj = null;
                        if (sdpType != null)
                        {
                            sdpObj = Activator.CreateInstance(sdpType);
                            var contentProp = sdpType.GetProperty("Content");
                            var typeProp = sdpType.GetProperty("Type");
                            if (contentProp != null) contentProp.SetValue(sdpObj, answer);
                            if (typeProp != null)
                            {
                                var enumType = typeProp.PropertyType;
                                var answerVal = Enum.GetValues(enumType).Cast<object?>().FirstOrDefault(v => v.ToString()?.ToLower().Contains("answer") == true);
                                if (answerVal != null) typeProp.SetValue(sdpObj, answerVal);
                            }
                        }

                        var setRes = setRemote.Invoke(peerConnection, sdpObj == null ? new object[] { answer } : new object[] { sdpObj });
                        if (setRes is Task setTask) setTask.GetAwaiter().GetResult();
                    }
                }
                catch { }
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                if (peerConnection != null && peerType != null)
                {
                    var close = peerType.GetMethod("Close") ?? peerType.GetMethod("Dispose");
                    try { close?.Invoke(peerConnection, null); } catch { }
                    peerConnection = null;
                    peerType = null;
                }
            }
            catch { }

            initialized = false;
        }
    }
}
