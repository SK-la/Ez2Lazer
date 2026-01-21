using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Scripting
{
    /// <summary>
    /// Provides an interface for Lua scripts to interact with the game.
    /// </summary>
    [MoonSharpUserData]
    public class SkinScriptInterface
    {
        private readonly ISkinScriptHost host;
        private readonly Dictionary<string, object> eventHandlers = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SkinScriptInterface"/> class.
        /// </summary>
        /// <param name="host">The host interface for the script.</param>
        public SkinScriptInterface(ISkinScriptHost host)
        {
            this.host = host;
        }

        /// <summary>
        /// Gets the beatmap's title.
        /// </summary>
        /// <returns>The beatmap's title.</returns>
        [MoonSharpVisible(true)]
        public string GetBeatmapTitle()
        {
            return host.CurrentBeatmap?.Metadata?.Title ?? "Unknown";
        }

        /// <summary>
        /// Gets the beatmap's artist.
        /// </summary>
        /// <returns>The beatmap's artist.</returns>
        [MoonSharpVisible(true)]
        public string GetBeatmapArtist()
        {
            return host.CurrentBeatmap?.Metadata?.Artist ?? "Unknown";
        }

        /// <summary>
        /// Gets the current ruleset's name.
        /// </summary>
        /// <returns>The ruleset's name.</returns>
        [MoonSharpVisible(true)]
        public string GetRulesetName()
        {
            return host.CurrentRuleset?.Name ?? "Unknown";
        }

        /// <summary>
        /// Creates a new component of the specified type.
        /// </summary>
        /// <param name="componentType">The component type name.</param>
        /// <returns>The created component.</returns>
        [MoonSharpVisible(true)]
        public object CreateComponent(string componentType)
        {
            return host.CreateComponent(componentType);
        }

        /// <summary>
        /// Gets a texture from the current skin.
        /// </summary>
        /// <param name="name">The name of the texture.</param>
        /// <returns>The texture, or null if not found.</returns>
        [MoonSharpVisible(true)]
        public object GetTexture(string name)
        {
            return host.GetTexture(name);
        }

        /// <summary>
        /// Gets a sample from the current skin.
        /// </summary>
        /// <param name="name">The name of the sample.</param>
        /// <returns>The sample, or null if not found.</returns>
        [MoonSharpVisible(true)]
        public object GetSample(string name)
        {
            return host.GetSample(name);
        }

        /// <summary>
        /// Plays a sample.
        /// </summary>
        /// <param name="name">The name of the sample.</param>
        [MoonSharpVisible(true)]
        public void PlaySample(string name)
        {
            var sample = host.GetSample(name);
            sample?.Play();
        }

        /// <summary>
        /// Subscribes to a game event.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        [MoonSharpVisible(true)]
        public void SubscribeToEvent(string eventName)
        {
            host.SubscribeToEvent(eventName);
        }

        /// <summary>
        /// Logs a message to the osu! log.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level (debug, info, warning, error).</param>
        [MoonSharpVisible(true)]
        public void Log(string message, string level = "info")
        {
            LogLevel logLevel = LogLevel.Information;

            switch (level.ToLower())
            {
                case "debug":
                    logLevel = LogLevel.Debug;
                    break;
                case "warning":
                    logLevel = LogLevel.Warning;
                    break;
                case "error":
                    logLevel = LogLevel.Error;
                    break;
            }

            host.Log(message, logLevel);
        }
    }
}
