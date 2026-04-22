// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Objects.Legacy;
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

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public BmsPlayer()
            : base(new PlayerConfiguration
            {
                AllowPause = false,
            })
        {
        }

        protected override bool PauseOnFocusLost => false;

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

            var bmsConfig = rulesetConfigCache.GetConfigFor(new BMSRuleset()) as BMSRulesetConfigManager;

            if (bmsConfig != null)
            {
                bmsKeysoundVolume = bmsConfig.GetBindable<double>(BMSRulesetSetting.KeysoundVolume);
                bmsKeysoundVolume.BindValueChanged(onKeysoundVolumeChanged, true);
            }
        }

        private void onKeysoundVolumeChanged(ValueChangedEvent<double> volume)
        {
            keysoundManager?.SetVolume(volume.NewValue);
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

            if (keysoundManager == null)
            {
                if (++updateCount == 1)
                    Logger.Log($"{BMS_LOG_PREFIX} Update: keysoundManager is null", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            if (!IsLoaded)
                return;

            double currentTime = GameplayClockContainer?.CurrentTime ?? Clock.CurrentTime;

            // Log first few updates
            if (updateCount++ < 5)
                Logger.Log($"{BMS_LOG_PREFIX} Update #{updateCount}: currentTime={currentTime:F1}ms", LoggingTarget.Runtime, LogLevel.Debug);

            // Only update after the game has started (after intro)
            if (currentTime < 0)
                return;

            // Update keysound manager with current time - THIS PLAYS BACKGROUND SOUNDS
            keysoundManager.Update(currentTime);
        }

        private void onNewResult(osu.Game.Rulesets.Judgements.JudgementResult result)
        {
            if (keysoundManager == null)
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
                    keysoundManager.TriggerKeysound(fileSample.Filename);
                    break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (DrawableRuleset != null)
                DrawableRuleset.NewResult -= onNewResult;

            if (bmsKeysoundVolume != null)
                bmsKeysoundVolume.ValueChanged -= onKeysoundVolumeChanged;

            keysoundManager?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}


