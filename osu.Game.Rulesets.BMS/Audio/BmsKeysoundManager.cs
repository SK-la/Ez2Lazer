// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.IO;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Audio
{
    /// <summary>
    /// Manages keysound playback for BMS beatmaps.
    /// BMS uses keysounds as the primary audio source with optional BGM.
    /// </summary>
    public class BmsKeysoundManager
    {
        private const string BMS_LOG_PREFIX = "[BMS]";

        private readonly AudioManager audioManager;
        private readonly ISampleStore sampleStore;
        private readonly string bmsFolder;
        private readonly Dictionary<string, ISample> keysoundCache = new Dictionary<string, ISample>();
        private readonly Dictionary<string, double> keysoundPlayTimes = new Dictionary<string, double>(); // filename -> scheduled play time
        private double currentOffset;
        private double gameplayTime;
        private double sampleVolume = 1;
        private List<BmsBackgroundSoundEvent> backgroundEvents = new List<BmsBackgroundSoundEvent>();
        private int nextBackgroundIndex;
        private double lastBackgroundUpdateTime = double.MinValue;

        public BmsKeysoundManager(AudioManager audioManager, string bmsFolder)
        {
            this.audioManager = audioManager;
            this.bmsFolder = bmsFolder;

            // Create a sample store for the BMS folder
            var storage = new NativeStorage(bmsFolder);
            var resourceStore = new StorageBackedResourceStore(storage);
            sampleStore = audioManager.GetSampleStore(resourceStore);

            Logger.Log($"{BMS_LOG_PREFIX} Keysound manager created - AudioManager: {audioManager != null}, Folder: {bmsFolder}", LoggingTarget.Runtime, LogLevel.Important);
            Logger.Log($"{BMS_LOG_PREFIX} Audio volumes - Samples: {audioManager.Samples.Volume.Value:F2}, Tracks: {audioManager.Tracks.Volume.Value:F2}", LoggingTarget.Runtime, LogLevel.Important);
            Logger.Log($"{BMS_LOG_PREFIX} Created SampleStore for BMS folder: {sampleStore != null}", LoggingTarget.Runtime, LogLevel.Important);
        }

        /// <summary>
        /// Preload all keysounds from the beatmap's hit objects.
        /// </summary>
        public void PreloadKeysounds(IEnumerable<HitObject> hitObjects)
        {
            var keysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hitObject in hitObjects)
            {
                IEnumerable<HitSampleInfo> samples = hitObject.Samples;

                if (hitObject is IBmsKeysoundProvider provider && provider.KeysoundSamples.Count > 0)
                    samples = provider.KeysoundSamples;
                else if (hitObject.AuxiliarySamples.Count > 0)
                    samples = hitObject.AuxiliarySamples;

                foreach (var sample in samples)
                {
                    if (sample is ConvertHitObjectParser.FileHitSampleInfo fileSample)
                    {
                        keysoundFiles.Add(fileSample.Filename);
                    }
                }
            }

            Logger.Log($"{BMS_LOG_PREFIX} Preloading {keysoundFiles.Count} keysounds", LoggingTarget.Runtime, LogLevel.Debug);

            foreach (string filename in keysoundFiles)
            {
                try
                {
                    LoadKeysound(filename);
                }
                catch (Exception ex)
                {
                    Logger.Log($"{BMS_LOG_PREFIX} Failed to preload keysound {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }
        }

        public void SetBackgroundSoundEvents(IReadOnlyList<BmsBackgroundSoundEvent> events)
        {
            backgroundEvents = events
                               .OrderBy(e => e.Time)
                               .ToList();
            nextBackgroundIndex = 0;
            lastBackgroundUpdateTime = double.MinValue;

            Logger.Log($"{BMS_LOG_PREFIX} Background sound events set: {backgroundEvents.Count}", LoggingTarget.Runtime, LogLevel.Debug);
        }

        /// <summary>
        /// Load a keysound file into cache.
        /// </summary>
        public ISample? LoadKeysound(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Normalize filename - use lowercase for cache key to handle case-insensitive filesystems
            string cacheKey = filename.ToLowerInvariant();

            if (keysoundCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Try to load the sample - sampleStore will handle file extensions automatically
            string? foundFilename = null;

            // Try the filename as-is first
            if (File.Exists(Path.Combine(bmsFolder, filename)))
            {
                foundFilename = filename;
            }
            else
            {
                // Get base name without extension and try different extensions
                string baseName = Path.GetFileNameWithoutExtension(filename);
                string[] extensions = new[] { "", ".wav", ".ogg", ".mp3", ".flac" };

                foreach (string ext in extensions)
                {
                    // Try different case variations
                    string[] testNames = new[] { baseName + ext, baseName.ToLowerInvariant() + ext, baseName.ToUpperInvariant() + ext };

                    foreach (string testName in testNames)
                    {
                        if (File.Exists(Path.Combine(bmsFolder, testName)))
                        {
                            foundFilename = testName;
                            break;
                        }
                    }

                    if (foundFilename != null)
                        break;
                }
            }

            if (foundFilename == null)
            {
                // Only log first few failures to avoid spam
                if (keysoundCache.Count < 100)
                    Logger.Log($"[BMS] ❌ Not found: {filename}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }

            try
            {
                var sample = sampleStore.Get(foundFilename);
                keysoundCache[cacheKey] = sample;

                // Log first few successful loads with more detail
                if (keysoundCache.Count <= 5)
                {
                    Logger.Log($"{BMS_LOG_PREFIX} ✓ Loaded #{keysoundCache.Count}: {filename} -> {foundFilename}", LoggingTarget.Runtime, LogLevel.Important);
                    Logger.Log($"{BMS_LOG_PREFIX}    Sample info: {sample != null}, Has volume: {sample?.Volume != null}, Volume value: {sample?.Volume?.Value ?? -1:F2}", LoggingTarget.Runtime, LogLevel.Important);
                }
                else if (keysoundCache.Count == 10)
                {
                    Logger.Log($"{BMS_LOG_PREFIX} ✓ Loaded 10 samples, suppressing further load logs...", LoggingTarget.Runtime, LogLevel.Debug);
                }

                return sample;
            }
            catch (Exception ex)
            {
                Logger.Log($"{BMS_LOG_PREFIX} ❌ Load exception: {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }

        /// <summary>
        /// Trigger a keysound to play immediately with offset applied.
        /// </summary>
        public void TriggerKeysound(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return;

            var sample = LoadKeysound(filename);

            if (sample != null)
            {
                try
                {
                    var channel = sample.Play();

                    if (channel != null)
                        channel.Volume.Value = sampleVolume;

                    // Log detailed playback info for first few triggers
                    if (nextBackgroundIndex < 5 || gameplayTime < 10000)
                    {
                        Logger.Log($"{BMS_LOG_PREFIX} ▶ Playing: {filename} at {gameplayTime:F0}ms - Channel: {channel != null}, Volume: {sample.Volume?.Value ?? -1}", LoggingTarget.Runtime, LogLevel.Important);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BMS] Failed to play keysound {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            }
            else
            {
                Logger.Log($"{BMS_LOG_PREFIX} ⚠ Keysound not loaded: {filename}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        /// <summary>
        /// Update the current gameplay time. Call this every frame from the Player.
        /// </summary>
        public void Update(double currentGameplayTime)
        {
            gameplayTime = currentGameplayTime;

            if (backgroundEvents.Count == 0)
            {
                // Only log this once
                if (nextBackgroundIndex == 0)
                {
                    Logger.Log($"{BMS_LOG_PREFIX} Update called but no background events loaded", LoggingTarget.Runtime, LogLevel.Debug);
                    nextBackgroundIndex = -1; // Mark as logged
                }
                return;
            }

            // Log first update with background events
            if (nextBackgroundIndex == 0)
                Logger.Log($"{BMS_LOG_PREFIX} First Update with {backgroundEvents.Count} background events, currentTime={currentGameplayTime:F1}ms", LoggingTarget.Runtime, LogLevel.Debug);

            if (currentGameplayTime < lastBackgroundUpdateTime)
            {
                nextBackgroundIndex = backgroundEvents.FindIndex(e => e.Time + currentOffset >= currentGameplayTime);
                if (nextBackgroundIndex < 0)
                    nextBackgroundIndex = backgroundEvents.Count;
            }

            int eventsTriggered = 0;
            while (nextBackgroundIndex < backgroundEvents.Count)
            {
                var evt = backgroundEvents[nextBackgroundIndex];
                double scheduledTime = evt.Time + currentOffset;

                if (currentGameplayTime < scheduledTime)
                    break;

                TriggerKeysound(evt.Filename);
                eventsTriggered++;
                nextBackgroundIndex++;
            }

            if (eventsTriggered > 0)
                Logger.Log($"{BMS_LOG_PREFIX} Triggered {eventsTriggered} background sound events at time={currentGameplayTime:F1}ms", LoggingTarget.Runtime, LogLevel.Debug);

            lastBackgroundUpdateTime = currentGameplayTime;
        }

        /// <summary>
        /// Set the offset to apply to all keysound timings.
        /// BMS offset works inversely: positive offset delays the audio playback.
        /// </summary>
        public void SetOffset(double offsetMs)
        {
            currentOffset = offsetMs;
        }

        public void SetVolume(double volume)
        {
            sampleVolume = Math.Clamp(volume, 0, 1);
        }

        /// <summary>
        /// Dispose all cached keysounds.
        /// </summary>
        public void Dispose()
        {
            foreach (var sample in keysoundCache.Values)
            {
                (sample as IDisposable)?.Dispose();
            }

            keysoundCache.Clear();
        }
    }
}
