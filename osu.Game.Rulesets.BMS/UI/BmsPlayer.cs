// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// Custom player for BMS beatmaps. Note keysounds are played via Mania drawable + <see cref="BMSSkin"/>;
    /// this player drives BGM timeline updates and volume only.
    /// </summary>
    public partial class BmsPlayer : SoloPlayer
    {
        private const string bms_log_prefix = "[BMS]";
        private BmsKeysoundManager? keysoundManager;
        private bool disposeKeysoundManagerOnExit;
        private Bindable<double>? bmsKeysoundVolume;
        private bool isDisposed;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private BmsLampStore? lampStore { get; set; }

        public BmsPlayer()
            : base(new PlayerConfiguration
            {
                AllowPause = false,
            })
        {
        }

        protected override bool PauseOnFocusLost => false;

        protected override bool CheckModsAllowFailure() => false;

        protected override Task PrepareScoreForResultsAsync(Score score)
        {
            score.ScoreInfo.Ruleset = new BMSRuleset().RulesetInfo;

            try
            {
                reportLampForScore(score);
            }
            catch (Exception ex)
            {
                Logger.Log($"{bms_log_prefix} Lamp report failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return base.PrepareScoreForResultsAsync(score);
        }

        private void reportLampForScore(Score score)
        {
            if (lampStore == null)
                return;

            BeatmapInfo? beatmapInfo = score.ScoreInfo.BeatmapInfo;
            if (beatmapInfo == null)
                return;

            var stats = score.ScoreInfo.Statistics;
            int get(HitResult r) => stats.GetValueOrDefault(r, 0);

            int perfect = get(HitResult.Perfect);
            int great = get(HitResult.Great);
            int good = get(HitResult.Good) + get(HitResult.Ok);
            int bad = get(HitResult.Meh) + get(HitResult.Poor);
            int miss = get(HitResult.Miss);
            int total = perfect + great + good + bad + miss;

            bool cleared = HealthProcessor != null && HealthProcessor.Health.Value > 0;

            var context = new BmsLampContext(
                HasPlayed: true,
                Cleared: cleared,
                Gauge: BmsGaugeType.Normal,
                MissCount: miss,
                GreatCount: great,
                GoodCount: good,
                BadCount: bad,
                PerfectGreatCount: perfect,
                TotalNotes: total,
                UsedHighestJudgementWindow: false);

            lampStore.ReportPlay(beatmapInfo, context);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            switch (Beatmap.Value)
            {
                case ManiaConvertedWorkingBeatmap maniaConverted:
                    keysoundManager = maniaConverted.KeysoundManager;
                    break;

                case BMSWorkingBeatmap bmsWorkingBeatmap:
                    keysoundManager = bmsWorkingBeatmap.KeysoundManager;
                    break;

                default:
                    keysoundManager = BmsBeatmapAudioResolver.TryPrepare(Beatmap.Value, audioManager, resolveAutoPreloadKeysounds());
                    disposeKeysoundManagerOnExit = keysoundManager != null;
                    break;
            }

            if (keysoundManager != null)
                BmsRuntimeAudioContext.RegisterKeysoundManager(keysoundManager);

            if (rulesetConfigCache.GetConfigFor(new BMSRuleset()) is BMSRulesetConfigManager bmsConfig)
            {
                bmsKeysoundVolume = bmsConfig.GetBindable<double>(BMSRulesetSetting.KeysoundVolume);
                bmsKeysoundVolume.BindValueChanged(onKeysoundVolumeChanged, true);
            }
        }

        private void onKeysoundVolumeChanged(ValueChangedEvent<double> volume)
        {
            var manager = keysoundManager;
            if (isDisposed || manager == null || manager.IsDisposed)
                return;

            manager.SetVolume(volume.NewValue);
        }

        private bool resolveAutoPreloadKeysounds()
        {
            try
            {
                if (rulesetConfigCache.GetConfigFor(new BMSRuleset()) is BMSRulesetConfigManager bmsConfig)
                    return bmsConfig.Get<bool>(BMSRulesetSetting.AutoPreloadKeysounds);
            }
            catch (Exception ex)
            {
                Logger.Log($"{bms_log_prefix} Failed to read AutoPreloadKeysounds: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return true;
        }

        protected override void Update()
        {
            base.Update();

            var manager = keysoundManager;

            if (isDisposed || manager == null || manager.IsDisposed || !IsLoaded || GameplayClockContainer == null)
                return;

            double currentTime = GameplayClockContainer.CurrentTime;

            if (currentTime < 0)
                return;

            manager.Update(currentTime);
        }

        protected override void Dispose(bool isDisposing)
        {
            isDisposed = true;

            if (bmsKeysoundVolume != null)
                bmsKeysoundVolume.ValueChanged -= onKeysoundVolumeChanged;

            BmsRuntimeAudioContext.Clear();

            if (disposeKeysoundManagerOnExit)
                keysoundManager?.Dispose();

            keysoundManager = null;
            base.Dispose(isDisposing);
        }
    }
}
