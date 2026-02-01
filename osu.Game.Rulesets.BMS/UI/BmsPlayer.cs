// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Objects;
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

        [BackgroundDependencyLoader]
        private void load()
        {
            // Extract the keysound manager from the working beatmap if available
            if (Beatmap.Value is ManiaConvertedWorkingBeatmap maniaConverted)
            {
                keysoundManager = maniaConverted.KeysoundManager;
                Logger.Log($"{BMS_LOG_PREFIX} Player loaded, keysound manager initialized", LoggingTarget.Runtime, LogLevel.Debug);
            }
            else
            {
                Logger.Log($"{BMS_LOG_PREFIX} Warning: Beatmap is not ManiaConvertedWorkingBeatmap", LoggingTarget.Runtime, LogLevel.Error);
            }
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

            if (result.HitObject is not ManiaHitObject hitObject)
                return;

            // Get keysound samples
            IEnumerable<HitSampleInfo> samples = hitObject.Samples;

            if (hitObject is IBmsKeysoundProvider provider && provider.KeysoundSamples.Count > 0)
                samples = provider.KeysoundSamples;
            else if (hitObject.AuxiliarySamples.Count > 0)
                samples = hitObject.AuxiliarySamples;

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

            keysoundManager?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}


