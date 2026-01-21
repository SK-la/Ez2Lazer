using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Skinning.Scripting
{
    /// <summary>
    /// Interface for communication between the game and skin scripts.
    /// </summary>
    public interface ISkinScriptHost
    {
        /// <summary>
        /// Gets the current beatmap.
        /// </summary>
        IBeatmap CurrentBeatmap { get; }

        /// <summary>
        /// Gets the audio manager for sound playback.
        /// </summary>
        IAudioManager AudioManager { get; }

        /// <summary>
        /// Gets the current skin.
        /// </summary>
        ISkin CurrentSkin { get; }

        /// <summary>
        /// Gets the current ruleset info.
        /// </summary>
        IRulesetInfo CurrentRuleset { get; }

        /// <summary>
        /// Creates a new drawable component of the specified type.
        /// </summary>
        /// <param name="componentType">The type of component to create.</param>
        /// <returns>The created component.</returns>
        Drawable CreateComponent(string componentType);

        /// <summary>
        /// Gets a texture from the current skin.
        /// </summary>
        /// <param name="name">The name of the texture.</param>
        /// <returns>The texture, or null if not found.</returns>
        Texture GetTexture(string name);

        /// <summary>
        /// Gets a sample from the current skin.
        /// </summary>
        /// <param name="name">The name of the sample.</param>
        /// <returns>The sample, or null if not found.</returns>
        ISample GetSample(string name);

        /// <summary>
        /// Subscribe to a game event.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        void SubscribeToEvent(string eventName);

        /// <summary>
        /// Log a message to the osu! log.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level.</param>
        void Log(string message, LogLevel level = LogLevel.Information);
    }

    /// <summary>
    /// Log levels for skin script messages.
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }
}
