using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.LAsEzExtensions.Skinning
{
    [Cached]
    public partial class SkinScriptManager : Component, ISkinScriptHost
    {
        private const string script_storage_directory = "skin-scripts";

        private readonly List<SkinScript> activeScripts = new List<SkinScript>();

        private Bindable<bool> scriptingEnabled = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = new SkinScriptingConfig(storage);
            scriptingEnabled = config.GetBindable<bool>(SkinScriptingSetting.ScriptingEnabled);
        }

        public override bool HandleNonPositionalInput => true;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skinManager.CurrentSkin.BindValueChanged(_ => reloadScripts(), true);
            scriptingEnabled.BindValueChanged(_ => reloadScripts());
        }

        private void reloadScripts()
        {
            foreach (var script in activeScripts)
                script.Dispose();

            activeScripts.Clear();

            if (!scriptingEnabled.Value)
                return;

            string externalScriptPath = storage.GetStorageForDirectory(script_storage_directory).GetFullPath($"{skinManager.CurrentSkinInfo.Value.ID}.lua");

            if (File.Exists(externalScriptPath))
            {
                try
                {
                    using var reader = new StreamReader(externalScriptPath);
                    string scriptContent = reader.ReadToEnd();

                    Log($"Loaded script file: {Path.GetFileName(externalScriptPath)}", SkinScriptLogLevel.Information);

                    var script = new SkinScript(scriptContent, Path.GetFileName(externalScriptPath), this);

                    if (!script.IsActivated)
                    {
                        Log($"Script activation failed: {Path.GetFileName(externalScriptPath)} | Reason: {script.ActivationError ?? "Unknown error"}", SkinScriptLogLevel.Error);
                        script.Dispose();
                        return;
                    }

                    activeScripts.Add(script);
                    Log($"Script activated: {Path.GetFileName(externalScriptPath)} | Description: {script.Description}", SkinScriptLogLevel.Information);
                    return;
                }
                catch (Exception ex)
                {
                    Log($"Script read exception: {Path.GetFileName(externalScriptPath)} | {ex.Message}", SkinScriptLogLevel.Error);
                    return;
                }
            }

            if (skinManager.CurrentSkin.Value is not Skin skin)
                return;

            foreach (string file in skin.GetScriptFiles())
            {
                try
                {
                    using Stream? stream = skin.GetFileStream(file);

                    if (stream == null)
                    {
                        Log($"Script read failed: {file} (stream is null)", SkinScriptLogLevel.Error);
                        continue;
                    }

                    using var reader = new StreamReader(stream);
                    string scriptContent = reader.ReadToEnd();

                    Log($"Loaded script file: {file}", SkinScriptLogLevel.Information);

                    var script = new SkinScript(scriptContent, file, this);

                    if (!script.IsActivated)
                    {
                        Log($"Script activation failed: {file} | Reason: {script.ActivationError ?? "Unknown error"}", SkinScriptLogLevel.Error);
                        script.Dispose();
                        continue;
                    }

                    activeScripts.Add(script);

                    Log($"Script activated: {file} | Description: {script.Description}", SkinScriptLogLevel.Information);
                }
                catch (Exception ex)
                {
                    Log($"Script load failed: {file} | {ex.Message}", SkinScriptLogLevel.Error);
                }
            }
        }

        public void NotifyComponentLoaded(Drawable component)
        {
            foreach (var script in activeScripts)
                script.NotifyComponentLoaded(component);
        }

        public void NotifyJudgement(JudgementResult result)
        {
            foreach (var script in activeScripts)
                script.NotifyJudgement(result);

            var hitEventData = new Dictionary<string, object?>
            {
                ["Type"] = result.Type.ToString(),
                ["IsHit"] = result.IsHit,
                ["TimeOffset"] = result.TimeOffset,
                ["ColumnIndex"] = getColumnIndex(result.HitObject),
            };

            notifyGameEvent("HitEvent", hitEventData);

            if (string.Equals(CurrentRuleset?.Name, "mania", StringComparison.OrdinalIgnoreCase))
            {
                int? column = getColumnIndex(result.HitObject);
                if (column != null)
                    notifyGameEvent("ManiaColumnHit", new Dictionary<string, object?> { ["ColumnIndex"] = column.Value });

                string hitObjectType = result.HitObject.GetType().Name;

                if (hitObjectType.Contains("Hold", StringComparison.OrdinalIgnoreCase) && hitObjectType.Contains("Head", StringComparison.OrdinalIgnoreCase) && column != null)
                    notifyGameEvent("ManiaHoldActivated", new Dictionary<string, object?> { ["ColumnIndex"] = column.Value });

                if (hitObjectType.Contains("Hold", StringComparison.OrdinalIgnoreCase) && hitObjectType.Contains("Tail", StringComparison.OrdinalIgnoreCase) && column != null)
                    notifyGameEvent("ManiaHoldReleased", new Dictionary<string, object?> { ["ColumnIndex"] = column.Value });
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (var script in activeScripts)
                script.Update();
        }

        public IBeatmap? CurrentBeatmap => beatmap.Value?.Beatmap;

        public IRulesetInfo? CurrentRuleset => ruleset.Value;

        public ISkin? CurrentSkin => skinManager.CurrentSkin.Value;

        public Drawable CreateComponent(string componentType)
        {
            switch (componentType.ToLowerInvariant())
            {
                case "container":
                    return new Container();

                case "sprite":
                    return new Sprite();

                case "box":
                    return new Box();

                case "spritetext":
                    return new OsuSpriteText();

                default:
                    Log($"Unknown component type requested: {componentType}. Returning Container fallback.", SkinScriptLogLevel.Warning);
                    return new Container();
            }
        }

        public Texture? GetTexture(string name) => skinManager.CurrentSkin.Value?.GetTexture(name);

        public ISample? GetSample(string name) => skinManager.GetSample(new SampleInfo(name));

        public void SubscribeToEvent(string eventName)
        {
            Log($"Subscribed event: {eventName}", SkinScriptLogLevel.Debug);
        }

        public double GetCurrentTime() => Time.Current;

        public void Log(string message, SkinScriptLogLevel level = SkinScriptLogLevel.Information)
        {
            if (!message.StartsWith("[SkinScript]", StringComparison.Ordinal))
                message = $"[SkinScript] {message}";

            switch (level)
            {
                case SkinScriptLogLevel.Debug:
                    Logger.Log(message, level: LogLevel.Debug);
                    break;

                case SkinScriptLogLevel.Warning:
                    Logger.Log(message, level: LogLevel.Important);
                    break;

                case SkinScriptLogLevel.Error:
                    Logger.Log(message, level: LogLevel.Important);
                    break;

                default:
                    Logger.Log(message);
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            foreach (var script in activeScripts)
                script.Dispose();

            activeScripts.Clear();

            base.Dispose(isDisposing);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            notifyInputEvent(new Dictionary<string, object?>
            {
                ["Key"] = e.Key.ToString(),
                ["State"] = "Down",
            });

            return false;
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            notifyInputEvent(new Dictionary<string, object?>
            {
                ["Key"] = e.Key.ToString(),
                ["State"] = "Up",
            });
        }

        private void notifyGameEvent(string eventName, IReadOnlyDictionary<string, object?> data)
        {
            foreach (var script in activeScripts)
                script.NotifyGameEvent(eventName, data);
        }

        private void notifyInputEvent(IReadOnlyDictionary<string, object?> data)
        {
            foreach (var script in activeScripts)
            {
                if (script.IsSubscribedToEvent("InputEvent"))
                    script.NotifyInputEvent(data);
            }
        }

        private static int? getColumnIndex(object hitObject)
        {
            var property = hitObject.GetType().GetProperty("Column");
            if (property == null)
                return null;

            object? raw = property.GetValue(hitObject);
            if (raw == null)
                return null;

            return raw switch
            {
                int i => i,
                IConvertible convertible => Convert.ToInt32(convertible),
                _ => null,
            };
        }
    }
}
