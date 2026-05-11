// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// Custom player for BMS beatmaps that handles keysound triggering.
    /// Extends the standard Player to integrate BMS audio driving.
    /// </summary>
    public partial class BmsPlayer : SoloPlayer
    {
        private const string BMS_LOG_PREFIX = "[BMS]";
        private BmsKeysoundManager? keysoundManager;
        private int updateCount = 0;
        private int triggerCount = 0;
        private Bindable<double>? bmsKeysoundVolume;
        private bool isDisposed;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        /// <summary>
        /// Optional lamp store cached by <c>BmsSoloSongSelect</c>. When BMS gameplay is reached
        /// through a different host (tests, replay viewer, etc.) no provider is registered and
        /// the score is simply not surfaced to the carousel; gameplay itself is unaffected.
        /// </summary>
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

        /// <summary>
        /// Same failure policy as always having <see cref="osu.Game.Rulesets.Mods.ModNoFail"/> active:
        /// health may reach 0, but the session does not end in a fail state; the chart runs to completion
        /// and the score is settled normally (equivalent to <c>ModNoFail.PerformFail()</c> returning
        /// <c>false</c> from <c>Player</c>'s fail gate).
        /// </summary>
        /// <remarks>
        /// Always returning <c>false</c> makes <c>Player.onFail()</c> reject failure so
        /// <see cref="osu.Game.Rulesets.Scoring.HealthProcessor.TriggerFailure"/> cannot set
        /// <c>HasFailed = true</c>. Mods that implement <see cref="osu.Game.Rulesets.Mods.IApplicableFailOverride"/>
        /// (SuddenDeath, Perfect, …) therefore cannot terminate the run for BMS.
        /// </remarks>
        protected override bool CheckModsAllowFailure() => false;

        /// <summary>
        /// BMS gameplay runs through mania's DrawableRuleset under <see cref="BMSGameplayRoute.ManiaCompatibility"/>,
        /// so <c>BMSPlayerLoader</c> switches <c>Ruleset.Value</c> to mania at gameplay start. <see cref="Player"/>
        /// then propagates that into <c>Score.ScoreInfo.Ruleset</c>, which leaves the post-gameplay score attributed
        /// to mania even though the underlying <see cref="osu.Game.Beatmaps.BeatmapInfo"/> belongs to BMS.
        /// <para>
        /// That mismatch breaks <c>ResultsScreen</c> + <c>StatisticsPanel</c>: they re-resolve a fresh
        /// <c>BeatmapManagerWorkingBeatmap</c> from <c>BeatmapInfo</c> (which decodes the .bms file into a
        /// <see cref="BMSBeatmap"/> with <see cref="BMSHitObject"/>s) and call
        /// <c>GetPlayableBeatmap(score.Ruleset)</c>. With <c>score.Ruleset = mania</c> that resolves to
        /// <see cref="osu.Game.Rulesets.Mania.Beatmaps.ManiaBeatmapConverter"/> whose <c>CanConvert()</c>
        /// returns false for <see cref="BMSHitObject"/>, throwing <c>BeatmapInvalidForRulesetException</c>.
        /// </para>
        /// <para>
        /// Re-attributing the score to BMS here keeps in-game HUD/skin/judgement under mania (gameplay path is
        /// untouched), while results / statistics route through <see cref="BMSBeatmapConverter"/>
        /// (<c>CanConvert() == true</c> for BMS hit objects). The argument is the cloned <c>scoreCopy</c> that
        /// <c>Player.prepareAndImportScoreAsync</c> hands off to <c>ImportScore</c> and the results screen, so
        /// gameplay's live <c>Score</c> is unaffected.
        /// </para>
        /// </summary>
        protected override Task PrepareScoreForResultsAsync(Score score)
        {
            score.ScoreInfo.Ruleset = new BMSRuleset().RulesetInfo;

            // Surface the play to the lamp store *before* the base implementation suspends on its async
            // import work — this guarantees the carousel/song-select sees the new lamp by the time the
            // player returns to it. Exceptions are caught so a broken lamp pipeline never blocks the
            // results screen.
            try
            {
                reportLampForScore(score);
            }
            catch (Exception ex)
            {
                Logger.Log($"{BMS_LOG_PREFIX} Lamp report failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return base.PrepareScoreForResultsAsync(score);
        }

        /// <summary>
        /// Build a <see cref="BmsLampContext"/> from the finalized score + live <see cref="HealthProcessor"/>
        /// and hand it to the cached <see cref="BmsLampStore"/>, which folds it into the per-beatmap
        /// best-lamp record consumed by <c>BmsLampAccentColourProvider</c> in song-select.
        /// </summary>
        /// <remarks>
        /// Cleared judgement: since BMS gameplay is force-NoFail (<see cref="CheckModsAllowFailure"/>),
        /// the canonical osu <c>HasFailed</c> path never fires. We instead approximate beatoraja's NORMAL-gauge
        /// rule (game must end with the gauge above the empty mark) by checking
        /// <see cref="HealthProcessor"/>.<c>Health</c> &gt; 0 at score-prep time.
        /// </remarks>
        private void reportLampForScore(Score score)
        {
            if (lampStore == null)
                return;

            BeatmapInfo? beatmapInfo = score.ScoreInfo.BeatmapInfo;
            if (beatmapInfo == null)
                return;

            var stats = score.ScoreInfo.Statistics;
            int get(HitResult r) => stats != null && stats.TryGetValue(r, out int v) ? v : 0;

            int perfect = get(HitResult.Perfect);
            int great = get(HitResult.Great);
            int good = get(HitResult.Good) + get(HitResult.Ok);
            int bad = get(HitResult.Meh) + get(HitResult.Poor);
            int miss = get(HitResult.Miss);
            int total = perfect + great + good + bad + miss;

            // Beatoraja's "cleared" check is gauge-end >= empty threshold. We approximate that with
            // the live HealthProcessor's final value because BMS gauge accounting isn't wired through
            // osu's HealthProcessor in detail yet — this still correctly distinguishes "kept the gauge
            // alive" from "drained to zero", which is what controls Easy/Normal/Hard/FullCombo branching
            // inside BeatorajaLampScheme.ResolveLamp.
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

            var lamp = lampStore.ReportPlay(beatmapInfo, context);

            Logger.Log(
                $"{BMS_LOG_PREFIX} Lamp reported for beatmap {beatmapInfo.ID}: {lamp} " +
                $"(cleared={cleared}, pg={perfect}, gr={great}, gd={good}, bd={bad}, miss={miss}, total={total})",
                LoggingTarget.Runtime, LogLevel.Debug);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Beatmap.Value is BMSWorkingBeatmap bmsWorkingBeatmap)
            {
                keysoundManager = new BmsKeysoundManager(audioManager, bmsWorkingBeatmap.FolderPath);

                if (bmsWorkingBeatmap.Beatmap is BMSBeatmap bmsBeatmap)
                {
                    keysoundManager.PreloadKeysounds(bmsBeatmap.HitObjects);
                    keysoundManager.SetBackgroundSoundEvents(bmsBeatmap.BackgroundSoundEvents);
                }
            }
            else if (Beatmap.Value is ManiaConvertedWorkingBeatmap maniaConverted)
            {
                keysoundManager = maniaConverted.KeysoundManager;
            }
            else
            {
                Logger.Log($"{BMS_LOG_PREFIX} Unexpected working beatmap type: {Beatmap.Value?.GetType().Name}", LoggingTarget.Runtime, LogLevel.Error);
            }

            var bmsConfig = rulesetConfigCache.GetConfigFor(new BMSRuleset()) as BMSRulesetConfigManager;

            if (bmsConfig != null)
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Subscribe to hit events to trigger keysounds on player input
            if (DrawableRuleset != null)
            {
                DrawableRuleset.NewResult += onNewResult;
                Logger.Log($"{BMS_LOG_PREFIX} Subscribed to hit events", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        protected override void Update()
        {
            base.Update();

            var manager = keysoundManager;
            if (isDisposed || manager == null || manager.IsDisposed)
            {
                if (++updateCount == 1)
                    Logger.Log($"{BMS_LOG_PREFIX} Update: keysoundManager is null", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            if (!IsLoaded)
                return;

            if (GameplayClockContainer == null)
            {
                if (updateCount++ < 3)
                    Logger.Log($"{BMS_LOG_PREFIX} GameplayClockContainer is null, skip keysound update.", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            double currentTime = GameplayClockContainer.CurrentTime;

            // Log first few updates
            if (updateCount++ < 5)
                Logger.Log($"{BMS_LOG_PREFIX} Update #{updateCount}: chartClock={currentTime:F1}ms", LoggingTarget.Runtime, LogLevel.Debug);

            // Only update after the game has started (after intro)
            if (currentTime < 0)
                return;

            // Update keysound manager with current time - THIS PLAYS BACKGROUND SOUNDS
            manager.Update(currentTime);
        }

        private void onNewResult(JudgementResult result)
        {
            var manager = keysoundManager;
            if (isDisposed || manager == null || manager.IsDisposed)
                return;

            // Only trigger keysounds on successful hits (not miss)
            if (!result.IsHit)
                return;

            if (result.HitObject == null)
                return;

            // Get keysound samples
            IEnumerable<HitSampleInfo> samples = result.HitObject.Samples;

            if (result.HitObject is IBmsKeysoundProvider provider && provider.KeysoundSamples.Count > 0)
                samples = provider.KeysoundSamples;
            else if (result.HitObject.AuxiliarySamples.Count > 0)
                samples = result.HitObject.AuxiliarySamples;

            // Trigger the keysound
            foreach (var sample in samples)
            {
                if (sample is ConvertHitObjectParser.FileHitSampleInfo fileSample)
                {
                    triggerCount++;
                    if (triggerCount <= 10)
                        Logger.Log($"{BMS_LOG_PREFIX} Hit trigger #{triggerCount}: {fileSample.Filename} - {result.Type}", LoggingTarget.Runtime, LogLevel.Debug);
                    manager.TriggerKeysound(fileSample.Filename);
                    break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            isDisposed = true;

            if (DrawableRuleset != null)
                DrawableRuleset.NewResult -= onNewResult;

            if (bmsKeysoundVolume != null)
                bmsKeysoundVolume.ValueChanged -= onKeysoundVolumeChanged;

            keysoundManager?.Dispose();
            keysoundManager = null;
            base.Dispose(isDisposing);
        }
    }
}
