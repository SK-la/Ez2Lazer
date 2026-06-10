// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Embedded endless autoplay preview without HUD or storyboard (playfield only).
    /// </summary>
    public partial class EzSkinEditorEmbeddedPlayer : CompositeDrawable
    {
        public GameplayClockContainer GameplayClock { get; private set; } = null!;

        public DrawableRuleset DrawableRuleset { get; private set; } = null!;

        public double BeatmapMinTime { get; private set; }

        public double BeatmapMaxTime { get; private set; }

        private readonly WorkingBeatmap workingBeatmap;
        private readonly RulesetInfo rulesetInfo;
        private readonly ISkin editorSkin;

        private IBeatmap playableBeatmap = null!;
        private GameplayState gameplayState = null!;
        private ScoreProcessor scoreProcessor = null!;
        private HealthProcessor healthProcessor = null!;
        private BreakTracker breakTracker = null!;

        private DependencyContainer dependencies = null!;

        public EzSkinEditorEmbeddedPlayer(WorkingBeatmap workingBeatmap, RulesetInfo rulesetInfo, ISkin editorSkin)
        {
            this.workingBeatmap = workingBeatmap;
            this.rulesetInfo = rulesetInfo;
            this.editorSkin = editorSkin;

            RelativeSizeAxes = Axes.Both;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var ruleset = rulesetInfo.CreateInstance();
            var autoplayMod = ruleset.GetAutoplayMod();

            if (autoplayMod == null)
                return;

            var mods = new Mod[] { autoplayMod };
            playableBeatmap = workingBeatmap.GetPlayableBeatmap(rulesetInfo, mods);

            if (playableBeatmap.HitObjects.Count == 0)
                return;

            DrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, mods);
            DrawableRuleset.FrameStablePlayback = true;
            DrawableRuleset.Playfield.DisplayJudgements.Value = true;

            dependencies.CacheAs(DrawableRuleset);

            if (DrawableRuleset is IDrawableScrollingRuleset scrollingRuleset)
                dependencies.CacheAs(scrollingRuleset.ScrollingInfo);

            scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playableBeatmap);

            healthProcessor = ruleset.CreateHealthProcessor(playableBeatmap.HitObjects[0].StartTime);
            healthProcessor.ApplyBeatmap(playableBeatmap);

            dependencies.CacheAs(scoreProcessor);
            dependencies.CacheAs(healthProcessor);

            BeatmapMinTime = 0;
            BeatmapMaxTime = Math.Max(workingBeatmap.BeatmapInfo.Length, playableBeatmap.GetLastObjectTime() + 1000);

            var score = autoplayMod.CreateScoreFromReplayData(playableBeatmap, mods);

            if (workingBeatmap.BeatmapInfo is BeatmapInfo beatmapInfo)
            {
                score.ScoreInfo.BeatmapInfo = beatmapInfo;
                score.ScoreInfo.BeatmapHash = beatmapInfo.Hash;
            }

            score.ScoreInfo.Ruleset = rulesetInfo;
            score.ScoreInfo.Mods = mods;

            gameplayState = new GameplayState(
                playableBeatmap,
                ruleset,
                mods,
                score,
                scoreProcessor,
                healthProcessor,
                new Storyboard(),
                new Bindable<LocalUserPlayingState>());

            dependencies.CacheAs(gameplayState);

            GameplayClock = new MasterGameplayClockContainer(workingBeatmap, DrawableRuleset.GameplayStartTime);

            breakTracker = new BreakTracker(DrawableRuleset.GameplayStartTime, scoreProcessor)
            {
                Breaks = workingBeatmap.Beatmap.Breaks,
            };

            var skinProvider = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, editorSkin);
            config.BindWith(OsuSetting.BeatmapSkins, skinProvider.BeatmapSkins);
            config.BindWith(OsuSetting.BeatmapColours, skinProvider.BeatmapColours);
            config.BindWith(OsuSetting.BeatmapHitsounds, skinProvider.BeatmapHitsounds);

            GameplayClock.Add(skinProvider);

            skinProvider.Add(new ScalingContainer(ScalingMode.Gameplay)
            {
                Children = new Drawable[]
                {
                    DrawableRuleset.With(rulesetDrawable =>
                    {
                        rulesetDrawable.FrameStableComponents.Children = new Drawable[]
                        {
                            scoreProcessor,
                            healthProcessor,
                            new ComboEffects(scoreProcessor),
                            breakTracker,
                        };
                    }),
                },
            });

            InternalChild = GameplayClock;

            DrawableRuleset.NewResult += r =>
            {
                healthProcessor.ApplyResult(r);
                scoreProcessor.ApplyResult(r);
                gameplayState.ApplyResult(r);
            };

            DrawableRuleset.RevertResult += r =>
            {
                healthProcessor.RevertResult(r);
                scoreProcessor.RevertResult(r);
            };

            ((IBindable<bool>)DrawableRuleset.IsPaused).BindTo(GameplayClock.IsPaused);

            scoreProcessor.HasCompleted.BindValueChanged(completed =>
            {
                if (completed.NewValue)
                    loopToStart();
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EzSkinEditorRulesetPreviewBootstrap.ApplyAutoplayReplay(DrawableRuleset, playableBeatmap);
            GameplayClock.Reset(DrawableRuleset.GameplayStartTime, startClock: false);
        }

        public void Seek(double time)
        {
            double clamped = Math.Clamp(time, BeatmapMinTime, BeatmapMaxTime);
            GameplayClock.Seek(clamped);
        }

        public void SetPlaying(bool playing)
        {
            if (playing)
                GameplayClock.Start();
            else
                GameplayClock.Stop();
        }

        public void Restart()
        {
            gameplayState.HasPassed = false;
            Seek(BeatmapMinTime);
            SetPlaying(true);
        }

        protected override void Update()
        {
            base.Update();

            if (!GameplayClock.IsRunning)
                return;

            if (gameplayState.HasPassed || GameplayClock.CurrentTime >= BeatmapMaxTime)
                loopToStart();
        }

        private void loopToStart()
        {
            gameplayState.HasPassed = false;
            GameplayClock.Seek(BeatmapMinTime);
        }
    }
}
