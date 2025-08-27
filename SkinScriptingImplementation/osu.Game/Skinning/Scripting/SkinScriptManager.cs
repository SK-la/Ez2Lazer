using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Skinning.Scripting
{
    /// <summary>
    /// Manages skin scripts for the current skin.
    /// </summary>
    [Cached]
    public class SkinScriptManager : Component, ISkinScriptHost
    {
        private readonly List<SkinScript> activeScripts = new List<SkinScript>();

        [Resolved]
        private AudioManager audioManager { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private Storage storage { get; set; }

        private SkinScriptingConfig scriptingConfig;

        private Bindable<bool> scriptingEnabled;
        private BindableList<string> allowedScripts;
        private BindableList<string> blockedScripts;        /// <summary>
        /// Gets the current beatmap.
        /// </summary>
        public IBeatmap CurrentBeatmap => beatmap.Value?.Beatmap;

        /// <summary>
        /// Gets the audio manager for sound playback.
        /// </summary>
        public IAudioManager AudioManager => audioManager;

        /// <summary>
        /// Gets the current skin.
        /// </summary>
        public ISkin CurrentSkin => skinManager.CurrentSkin.Value;

        /// <summary>
        /// Gets the current ruleset info.
        /// </summary>
        public IRulesetInfo CurrentRuleset => ruleset.Value;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Initialize scripting configuration
            scriptingConfig = new SkinScriptingConfig(storage);
            scriptingEnabled = scriptingConfig.GetBindable<bool>(SkinScriptingSettings.ScriptingEnabled);
            allowedScripts = scriptingConfig.GetBindable<List<string>>(SkinScriptingSettings.AllowedScripts).GetBoundCopy();
            blockedScripts = scriptingConfig.GetBindable<List<string>>(SkinScriptingSettings.BlockedScripts).GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Subscribe to skin changes
            skinManager.CurrentSkinInfo.BindValueChanged(skinChanged);

            // Subscribe to scripting configuration changes
            scriptingEnabled.BindValueChanged(_ => updateScriptStates(), true);
            allowedScripts.BindCollectionChanged((_, __) => updateScriptStates(), true);
            blockedScripts.BindCollectionChanged((_, __) => updateScriptStates(), true);
        }

        private void updateScriptStates()
        {
            if (!scriptingEnabled.Value)
            {
                // Disable all scripts when scripting is disabled
                foreach (var script in activeScripts)
                    script.IsEnabled = false;

                return;
            }

            foreach (var script in activeScripts)
            {
                string scriptName = script.ScriptName;

                if (blockedScripts.Contains(scriptName))
                    script.IsEnabled = false;
                else if (allowedScripts.Count > 0)
                    script.IsEnabled = allowedScripts.Contains(scriptName);
                else
                    script.IsEnabled = true;
            }
        }

        private void skinChanged(ValueChangedEvent<SkinInfo> skin)
        {
            // Clear existing scripts
            foreach (var script in activeScripts)
                script.Dispose();

            activeScripts.Clear();

            if (scriptingEnabled.Value)
            {
                // Load scripts from the new skin
                loadScriptsFromSkin(skinManager.CurrentSkin.Value);
            }
        }        private void loadScriptsFromSkin(ISkin skin)
        {
            if (skin is Skin skinWithFiles)
            {
                // Look for Lua script files
                foreach (var file in skinWithFiles.Files.Where(f => Path.GetExtension(f.Filename).Equals(".lua", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using (Stream stream = skinWithFiles.GetStream(file.Filename))
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string scriptContent = reader.ReadToEnd();
                            SkinScript script = new SkinScript(scriptContent, file.Filename, this);

                            // 设置脚本的启用状态
                            string scriptName = file.Filename;
                            if (blockedScripts.Contains(scriptName))
                                script.IsEnabled = false;
                            else if (allowedScripts.Count > 0)
                                script.IsEnabled = allowedScripts.Contains(scriptName);
                            else
                                script.IsEnabled = true;

                            activeScripts.Add(script);

                            Log($"Loaded skin script: {file.Filename}", LogLevel.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to load skin script {file.Filename}: {ex.Message}", LogLevel.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Notifies scripts that a component has been loaded.
        /// </summary>
        /// <param name="component">The loaded component.</param>
        public void NotifyComponentLoaded(Drawable component)
        {
            foreach (var script in activeScripts)
                script.NotifyComponentLoaded(component);
        }

        /// <summary>
        /// Notifies scripts of a game event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="data">The event data.</param>
        public void NotifyGameEvent(string eventName, object data)
        {
            foreach (var script in activeScripts)
                script.NotifyGameEvent(eventName, data);
        }

        /// <summary>
        /// Notifies scripts of a judgement result.
        /// </summary>
        /// <param name="result">The judgement result.</param>
        public void NotifyJudgement(JudgementResult result)
        {
            foreach (var script in activeScripts)
                script.NotifyJudgement(result);
        }

        /// <summary>
        /// Notifies scripts of an input event.
        /// </summary>
        /// <param name="inputEvent">The input event.</param>
        public void NotifyInputEvent(InputEvent inputEvent)
        {
            foreach (var script in activeScripts)
                script.NotifyInputEvent(inputEvent);
        }

        /// <summary>
        /// Updates all scripts.
        /// </summary>
        protected override void Update()
        {
            base.Update();

            foreach (var script in activeScripts)
                script.Update();
        }

        #region ISkinScriptHost Implementation

        /// <summary>
        /// Creates a new drawable component of the specified type.
        /// </summary>
        /// <param name="componentType">The type of component to create.</param>
        /// <returns>The created component.</returns>
        public Drawable CreateComponent(string componentType)
        {
            // This would need to be expanded with actual component types
            switch (componentType)
            {
                case "Container":
                    return new Container();
                // Add more component types as needed
                default:
                    Log($"Unknown component type: {componentType}", LogLevel.Warning);
                    return new Container();
            }
        }

        /// <summary>
        /// Gets a texture from the current skin.
        /// </summary>
        /// <param name="name">The name of the texture.</param>
        /// <returns>The texture, or null if not found.</returns>
        public Texture GetTexture(string name)
        {
            return skinManager.CurrentSkin.Value.GetTexture(name);
        }

        /// <summary>
        /// Gets a sample from the current skin.
        /// </summary>
        /// <param name="name">The name of the sample.</param>
        /// <returns>The sample, or null if not found.</returns>
        public ISample GetSample(string name)
        {
            return skinManager.CurrentSkin.Value.GetSample(name);
        }

        /// <summary>
        /// Subscribe to a game event.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        public void SubscribeToEvent(string eventName)
        {
            // Implementation would depend on available events
            Log($"Script subscribed to event: {eventName}", LogLevel.Debug);
        }

        /// <summary>
        /// Log a message to the osu! log.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level.</param>
        public void Log(string message, LogLevel level = LogLevel.Information)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Logger.Log(message, level: LogLevel.Debug);
                    break;
                case LogLevel.Information:
                    Logger.Log(message);
                    break;
                case LogLevel.Warning:
                    Logger.Log(message, level: Framework.Logging.LogLevel.Important);
                    break;
                case LogLevel.Error:
                    Logger.Error(message);
                    break;
            }
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            foreach (var script in activeScripts)
                script.Dispose();

            activeScripts.Clear();

            base.Dispose(isDisposing);
        }
    }
}
