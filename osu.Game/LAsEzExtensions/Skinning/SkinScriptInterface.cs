using System;
using MoonSharp.Interpreter;

namespace osu.Game.LAsEzExtensions.Skinning
{
    [MoonSharpUserData]
    public class SkinScriptInterface
    {
        private readonly ISkinScriptHost host;
        private readonly Action<string>? onSubscribe;

        public SkinScriptInterface(ISkinScriptHost host, Action<string>? onSubscribe = null)
        {
            this.host = host;
            this.onSubscribe = onSubscribe;
        }

        public string GetBeatmapTitle() => host.CurrentBeatmap?.Metadata.Title ?? string.Empty;

        public string GetBeatmapArtist() => host.CurrentBeatmap?.Metadata.Artist ?? string.Empty;

        public string GetRulesetName() => host.CurrentRuleset?.Name ?? string.Empty;

        public object CreateComponent(string componentType) => host.CreateComponent(componentType);

        public object? GetTexture(string name) => host.GetTexture(name);

        public object? GetSample(string name) => host.GetSample(name);

        public void PlaySample(string name)
        {
            host.GetSample(name)?.Play();
        }

        public void SubscribeToEvent(string eventName)
        {
            onSubscribe?.Invoke(eventName);
            host.SubscribeToEvent(eventName);
        }

        public double GetCurrentTime() => host.GetCurrentTime();

        public void Log(string message, string level = "info")
        {
            SkinScriptLogLevel logLevel = level.ToLowerInvariant() switch
            {
                "debug" => SkinScriptLogLevel.Debug,
                "warning" => SkinScriptLogLevel.Warning,
                "error" => SkinScriptLogLevel.Error,
                _ => SkinScriptLogLevel.Information,
            };

            host.Log(message, logLevel);
        }
    }
}
