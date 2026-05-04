// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 一个轻量级的 replay 分析器，直接使用 DrawableRuleset 进行后台判定计算。
    /// 通过注入空皮肤源来避免实例化真实皮肤资源。
    /// </summary>
    internal sealed partial class ReplayAnalysisRunner : CompositeDrawable
    {
        private readonly Score score;
        private readonly Action<List<HitEvent>?> onComplete;

        private DrawableRuleset? drawableRuleset;
        private ScoreProcessor? scoreProcessor;
        private bool isFinished;

        public ReplayAnalysisRunner(Score score, Action<List<HitEvent>?> onComplete)
        {
            this.score = score;
            this.onComplete = onComplete;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            AlwaysPresent = false;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmaps)
        {
            try
            {
                RulesetInfo rulesetInfo = score.ScoreInfo.Ruleset;

                var ruleset = rulesetInfo.CreateInstance();

                var workingBeatmap = beatmaps.GetWorkingBeatmap(score.ScoreInfo.BeatmapInfo);

                if (workingBeatmap is DummyWorkingBeatmap)
                {
                    finish(null);
                    return;
                }

                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(rulesetInfo, score.ScoreInfo.Mods);

                if (playableBeatmap.HitObjects.Count == 0)
                {
                    finish(null);
                    return;
                }

                drawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, score.ScoreInfo.Mods);
                drawableRuleset.NewResult += onDrawableNewResult;
                drawableRuleset.RevertResult += onDrawableRevertResult;

                scoreProcessor = ruleset.CreateScoreProcessor();
                scoreProcessor.Mods.Value = score.ScoreInfo.Mods;
                scoreProcessor.ApplyBeatmap(playableBeatmap);

                foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                    mod.ApplyToScoreProcessor(scoreProcessor);

                scoreProcessor.HasCompleted.BindValueChanged(completed =>
                {
                    if (completed.NewValue && !isFinished)
                        finish(scoreProcessor.HitEvents.ToList());
                });

                var clock = new GameplayClockContainer(workingBeatmap.LoadTrack(), applyOffsets: true, requireDecoupling: true);
                clock.Add(drawableRuleset);
                AddInternal(clock);

                bool gameplayStarted = false;

                void startGameplay()
                {
                    if (gameplayStarted)
                        return;

                    gameplayStarted = true;

                    drawableRuleset.SetReplayScore(score);
                    drawableRuleset.Audio.AddAdjustment(AdjustableProperty.Volume, new BindableDouble(0));
                    clock.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, new BindableDouble(getPlaybackRate(score)));
                    clock.Reset(drawableRuleset.GameplayStartTime, startClock: true);
                }

                if (drawableRuleset.LoadState >= LoadState.Ready)
                    startGameplay();
                else
                    drawableRuleset.OnLoadComplete += _ => startGameplay();
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Error(ex, "[EzScore] ReplayAnalysisRunner failed");
                }
                catch
                {
                }

                finish(null);
            }
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<ISkinSource>(NullSkinSource.INSTANCE);
            return dependencies;
        }

        private void onDrawableNewResult(JudgementResult result)
        {
            scoreProcessor?.ApplyResult(result);
        }

        private void onDrawableRevertResult(JudgementResult result)
        {
            scoreProcessor?.RevertResult(result);
        }

        private void finish(List<HitEvent>? hitEvents)
        {
            if (isFinished)
                return;

            isFinished = true;

            if (drawableRuleset != null)
            {
                drawableRuleset.NewResult -= onDrawableNewResult;
                drawableRuleset.RevertResult -= onDrawableRevertResult;
            }

            drawableRuleset?.RemoveAndDisposeImmediately();
            Schedule(() => onComplete(hitEvents));
        }

        public void Cancel()
        {
            if (isFinished)
                return;

            isFinished = true;

            if (drawableRuleset != null)
            {
                drawableRuleset.NewResult -= onDrawableNewResult;
                drawableRuleset.RevertResult -= onDrawableRevertResult;
            }

            drawableRuleset?.RemoveAndDisposeImmediately();
        }

        private static double getPlaybackRate(Score score) => score.ScoreInfo.Ruleset.ShortName == "fruits" ? 5 : 10;

        private sealed class NullSkinSource : ISkinSource
        {
            public static readonly NullSkinSource INSTANCE = new NullSkinSource();

            private NullSkinSource()
            {
            }

            public event Action SourceChanged
            {
                add { }
                remove { }
            }

            public ISkin? FindProvider(Func<ISkin, bool> lookupFunction) => null;

            public IEnumerable<ISkin> AllSources => Array.Empty<ISkin>();

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull => null;
        }
    }
}
