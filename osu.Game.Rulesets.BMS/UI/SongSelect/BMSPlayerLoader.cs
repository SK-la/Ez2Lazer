// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Loader screen that prepares and starts BMS gameplay.
    /// Uses BMS ruleset gameplay pipeline directly.
    /// </summary>
    public partial class BMSPlayerLoader : OsuScreen
    {
        public override bool DisallowExternalBeatmapRulesetChanges => true;

        protected override bool InitialBackButtonVisibility => false;

        private readonly BMSWorkingBeatmap workingBeatmap;
        private readonly BMSGameplayRoute? gameplayRouteOverride;
        private LoadingSpinner loadingSpinner = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText titleText = null!;
        private ScheduledDelegate? scheduledLoadBeatmap;
        private ScheduledDelegate? scheduledPushPlayer;
        private ScheduledDelegate? scheduledExit;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        /// <summary>
        /// Construct a player loader with an explicit route override (mainly used by tests or special entries).
        /// </summary>
        public BMSPlayerLoader(BMSWorkingBeatmap workingBeatmap, BMSGameplayRoute? gameplayRouteOverride = null)
        {
            this.workingBeatmap = workingBeatmap;
            this.gameplayRouteOverride = gameplayRouteOverride;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(0, 20),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.Music,
                            Size = new Vector2(64),
                            Colour = colours.Yellow,
                        },
                        titleText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = BmsStrings.LOADER_LOADING,
                            Font = OsuFont.GetFont(size: 32, weight: FontWeight.Bold),
                        },
                        statusText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = BmsStrings.LOADER_PARSING_BMS_FILE,
                            Font = OsuFont.GetFont(size: 18),
                            Colour = colours.Yellow,
                        },
                        loadingSpinner = new LoadingSpinner
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(50),
                        },
                    },
                },
            };

            loadingSpinner.Show();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Start loading after a brief delay
            scheduledLoadBeatmap = Scheduler.AddDelayed(loadBeatmap, 100);
        }

        private void loadBeatmap()
        {
            if (!this.IsCurrentScreen())
                return;

            try
            {
                statusText.Text = BmsStrings.LOADER_PARSING_BEATMAP;

                // Force load the beatmap
                var beatmap = workingBeatmap.Beatmap;

                if (beatmap == null || beatmap.HitObjects.Count == 0)
                {
                    statusText.Text = BmsStrings.LOADER_LOAD_FAILED;
                    loadingSpinner.Hide();
                    scheduleExit(2000);
                    return;
                }

                // Update title display
                var metadata = workingBeatmap.BeatmapInfo.Metadata;
                titleText.Text = string.IsNullOrEmpty(metadata.Title) ? "BMS" : metadata.Title;
                statusText.Text = BmsStrings.Loader_LoadComplete(beatmap.HitObjects.Count);

                bool preload = resolveAutoPreloadFromConfig();
                workingBeatmap.PrepareAudio(preload);

                // Small delay then push to player
                scheduledPushPlayer = Scheduler.AddDelayed(() =>
                {
                    if (!this.IsCurrentScreen())
                        return;

                    loadingSpinner.Hide();
                    pushPlayer();
                }, 500);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load BMS beatmap for play");
                statusText.Text = BmsStrings.Loader_LoadError(ex.Message);
                loadingSpinner.Hide();
                scheduleExit(3000);
            }
        }

        private void pushPlayer()
        {
            if (!this.IsCurrentScreen())
                return;

            try
            {
                BMSGameplayRoute route = gameplayRouteOverride ?? resolveRouteFromConfig();

                if (route == BMSGameplayRoute.BmsNative)
                {
                    Beatmap.Value = workingBeatmap;
                    Ruleset.Value = new BMSNativeRuleset().RulesetInfo;
                }
                else
                {
                    bool preload = resolveAutoPreloadFromConfig();
                    var maniaWorkingBeatmap = new ManiaConvertedWorkingBeatmap(workingBeatmap, audioManager, preload);
                    Beatmap.Value = maniaWorkingBeatmap;
                    Ruleset.Value = new ManiaRuleset().RulesetInfo;
                }

                var playerLoader = new PlayerLoader(() => new BmsPlayer());
                this.Push(playerLoader);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to push BMS player");
                statusText.Text = BmsStrings.Loader_LaunchFailed(ex.Message);
                scheduleExit(3000);
            }
        }

        private bool resolveAutoPreloadFromConfig()
        {
            try
            {
                if (rulesetConfigCache.GetConfigFor(new BMSRuleset()) is BMSRulesetConfigManager bmsConfig)
                    return bmsConfig.Get<bool>(BMSRulesetSetting.AutoPreloadKeysounds);
            }
            catch (Exception ex)
            {
                Logger.Log($"BMSPlayerLoader: failed to read AutoPreloadKeysounds: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return true;
        }

        private BMSGameplayRoute resolveRouteFromConfig()
        {
            try
            {
                if (rulesetConfigCache.GetConfigFor(new BMSRuleset()) is BMSRulesetConfigManager bmsConfig)
                    return bmsConfig.Get<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute);
            }
            catch (Exception ex)
            {
                Logger.Log($"BMSPlayerLoader: failed to read GameplayRoute config: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return BMSGameplayRoute.ManiaCompatibility;
        }

        private void scheduleExit(double delay)
        {
            scheduledExit?.Cancel();
            scheduledExit = Scheduler.AddDelayed(() =>
            {
                if (this.IsCurrentScreen())
                    this.Exit();
            }, delay);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(300);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            scheduledLoadBeatmap?.Cancel();
            scheduledPushPlayer?.Cancel();
            scheduledExit?.Cancel();
            this.FadeOut(200);
            return base.OnExiting(e);
        }
    }
}
