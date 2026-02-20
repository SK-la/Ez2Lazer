using osu.Framework.Audio.Sample;
using osu.Framework.Timing;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.LAsEzExtensions.Skinning
{
    public interface ISkinScriptHost
    {
        IBeatmap? CurrentBeatmap { get; }

        IRulesetInfo? CurrentRuleset { get; }

        ISkin? CurrentSkin { get; }

        Drawable CreateComponent(string componentType);

        Texture? GetTexture(string name);

        ISample? GetSample(string name);

        void SubscribeToEvent(string eventName);

        double GetCurrentTime();

        void Log(string message, SkinScriptLogLevel level = SkinScriptLogLevel.Information);
    }

    public enum SkinScriptLogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
    }
}
