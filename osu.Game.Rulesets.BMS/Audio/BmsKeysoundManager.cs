// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Audio
{
    /// <summary>
    /// Manages BMS chart audio: <see cref="Prepare"/> loads all samples before gameplay;
    /// runtime only plays from the in-memory cache (no disk IO).
    /// </summary>
    public class BmsKeysoundManager
    {
        private const string bms_log_prefix = "[BMS]";
        private const int max_background_triggers_per_update = 32;
        private const double stale_background_event_threshold = 250;

        private readonly AudioManager audioManager;
        private readonly ISampleStore? sampleStore;
        private readonly string bmsFolder;
        private readonly Dictionary<string, ISample> keysoundCache = new Dictionary<string, ISample>(StringComparer.OrdinalIgnoreCase);
        private BmsFolderSampleIndex? folderIndex;
        private double currentOffset;
        private double gameplayTime;
        private double sampleVolume = 1;
        private List<BmsBackgroundSoundEvent> backgroundEvents = new List<BmsBackgroundSoundEvent>();
        private int nextBackgroundIndex;
        private double lastBackgroundUpdateTime = double.MinValue;
        private int missingSampleLogCount;
        public bool IsDisposed { get; private set; }

        public BmsKeysoundManager(AudioManager audioManager, string bmsFolder)
        {
            this.audioManager = audioManager;
            this.bmsFolder = bmsFolder;

            var storage = new NativeStorage(bmsFolder);
            var resourceStore = new StorageBackedResourceStore(storage);
            sampleStore = audioManager.GetSampleStore(resourceStore);
        }

        public bool IsPrepared { get; private set; }

        /// <summary>
        /// One-time chart entry: build folder index, decode all referenced samples into memory.
        /// </summary>
        public void Prepare(IEnumerable<HitObject> hitObjects, IReadOnlyList<BmsBackgroundSoundEvent>? backgroundEvents = null)
        {
            if (IsDisposed || IsPrepared)
                return;

            folderIndex = BmsFolderSampleIndex.TryBuild(bmsFolder);

            var keysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            collectSampleFilenames(hitObjects, keysoundFiles);

            if (backgroundEvents != null)
            {
                foreach (var evt in backgroundEvents)
                {
                    if (!string.IsNullOrEmpty(evt.Filename))
                        keysoundFiles.Add(evt.Filename);
                }
            }

            int loaded = 0;
            int missing = 0;

            foreach (string filename in keysoundFiles)
            {
                if (loadIntoCache(filename) != null)
                    loaded++;
                else
                    missing++;
            }

            if (backgroundEvents != null && backgroundEvents.Count > 0)
                SetBackgroundSoundEvents(backgroundEvents);

            IsPrepared = true;

            Logger.Log($"{bms_log_prefix} Prepare complete: {loaded} loaded, {missing} missing, folder={bmsFolder}", LoggingTarget.Runtime, LogLevel.Debug);
        }

        /// <summary>
        /// Legacy name; forwards to <see cref="Prepare"/>.
        /// </summary>
        public void PreloadKeysounds(IEnumerable<HitObject> hitObjects) => Prepare(hitObjects);

        public void SetBackgroundSoundEvents(IReadOnlyList<BmsBackgroundSoundEvent> events)
        {
            if (IsDisposed)
                return;

            backgroundEvents = events
                               .OrderBy(e => e.Time)
                               .ToList();
            nextBackgroundIndex = 0;
            lastBackgroundUpdateTime = double.MinValue;
        }

        /// <summary>
        /// Returns a prepared sample for skin / drawable <see cref="osu.Game.Skinning.ISkin.GetSample"/> (no IO).
        /// </summary>
        public ISample? GetPreparedSample(string filename)
        {
            if (IsDisposed || string.IsNullOrEmpty(filename))
                return null;

            string cacheKey = filename.ToLowerInvariant();
            return keysoundCache.GetValueOrDefault(cacheKey);
        }

        /// <summary>
        /// Play a previously prepared sample. Does not load from disk.
        /// </summary>
        public void TriggerKeysound(string filename)
        {
            if (IsDisposed || string.IsNullOrEmpty(filename))
                return;

            var sample = GetPreparedSample(filename);

            if (sample == null)
            {
                if (missingSampleLogCount++ < 5)
                    Logger.Log($"{bms_log_prefix} Keysound not prepared: {filename}", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            try
            {
                var channel = sample.Play();
                if (channel != null)
                    channel.Volume.Value = sampleVolume;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to play keysound {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        public void Update(double currentGameplayTime)
        {
            if (IsDisposed)
                return;

            gameplayTime = currentGameplayTime;

            if (backgroundEvents.Count == 0)
                return;

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

                if (currentGameplayTime - scheduledTime > stale_background_event_threshold)
                {
                    nextBackgroundIndex++;
                    continue;
                }

                TriggerKeysound(evt.Filename);
                eventsTriggered++;
                nextBackgroundIndex++;

                if (eventsTriggered >= max_background_triggers_per_update)
                    break;
            }

            lastBackgroundUpdateTime = currentGameplayTime;
        }

        public void SetOffset(double offsetMs)
        {
            if (IsDisposed)
                return;

            currentOffset = offsetMs;
        }

        public void SetVolume(double volume)
        {
            if (IsDisposed)
                return;

            sampleVolume = Math.Clamp(volume, 0, 1);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            foreach (var sample in keysoundCache.Values)
                (sample as IDisposable)?.Dispose();

            keysoundCache.Clear();
            backgroundEvents.Clear();
        }

        private ISample? loadIntoCache(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            string cacheKey = filename.ToLowerInvariant();

            if (keysoundCache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (folderIndex == null || sampleStore == null)
                return null;

            string? relative = folderIndex.TryResolveRelativePath(filename);

            if (relative == null)
                return null;

            try
            {
                var sample = sampleStore.Get(relative);
                keysoundCache[cacheKey] = sample;
                return sample;
            }
            catch (Exception ex)
            {
                Logger.Log($"{bms_log_prefix} Load failed: {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }

        private static void collectSampleFilenames(IEnumerable<HitObject> hitObjects, HashSet<string> keysoundFiles)
        {
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
                        keysoundFiles.Add(fileSample.Filename);
                }

                if (hitObject.NestedHitObjects.Count > 0)
                    collectSampleFilenames(hitObject.NestedHitObjects, keysoundFiles);
            }
        }
    }
}
