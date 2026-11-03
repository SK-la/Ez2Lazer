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
    /// Manages BMS chart audio: <see cref="Prepare"/> decodes all referenced samples on a background thread.
    /// Lookups hit the in-memory cache; a sample that preloading has not resolved yet is decoded on demand as a
    /// fallback so the chart is never left silent.
    /// </summary>
    public class BmsKeysoundManager
    {
        private const string bms_log_prefix = "[BMS]";
        private const int max_background_triggers_per_update = 32;
        private const double stale_background_event_threshold = 250;

        private readonly AudioManager audioManager;
        private readonly ISampleStore? sampleStore;
        private readonly string bmsFolder;

        /// <summary>
        /// Guards <see cref="keysoundCache"/>, <see cref="folderIndex"/> and the preload bookkeeping: the preload
        /// task and the update thread both resolve samples.
        /// </summary>
        private readonly object syncRoot = new object();

        private readonly Dictionary<string, ISample> keysoundCache = new Dictionary<string, ISample>(StringComparer.OrdinalIgnoreCase);
        private BmsFolderSampleIndex? folderIndex;
        private CancellationTokenSource? preloadCancellation;
        private Task? preloadTask;
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
        /// The in-flight background decode started by <see cref="Prepare"/>, or <see langword="null"/> when nothing
        /// is loading. Callers that must not start audio against a cold cache (player loader, song-select preview)
        /// wait for this instead of blocking the update thread.
        /// </summary>
        public Task? PreloadTask
        {
            get
            {
                lock (syncRoot)
                    return preloadTask;
            }
        }

        /// <summary>
        /// One-time chart entry: collects the referenced samples and decodes them on a background thread.
        /// </summary>
        /// <remarks>
        /// Returns immediately. Decoding a chart's whole sample set takes long enough to visibly freeze the game
        /// after song select, so the work is handed to the audio store's background path.
        /// </remarks>
        public void Prepare(IEnumerable<HitObject> hitObjects, IReadOnlyList<BmsBackgroundSoundEvent>? backgroundEvents = null)
        {
            if (IsDisposed)
                return;

            lock (syncRoot)
            {
                if (preloadTask is { IsCompleted: false })
                    return;

                if (IsPrepared)
                    return;

                IsPrepared = true;
            }

            // Only on the first prepare: installing the timeline resets background playback position, which a
            // repeated call (e.g. constructing the mania-converted beatmap after the loader already prepared)
            // must not do.
            if (backgroundEvents != null && backgroundEvents.Count > 0)
                SetBackgroundSoundEvents(backgroundEvents);

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

            if (keysoundFiles.Count == 0)
                return;

            startPreload(keysoundFiles);
        }

        /// <summary>
        /// Cancels an in-flight background decode, e.g. when a song-select preview is torn down before it finishes.
        /// A later <see cref="Prepare"/> restarts it, so cancelling never leaves the chart permanently unloaded.
        /// </summary>
        public void CancelPreload()
        {
            lock (syncRoot)
            {
                if (preloadTask is not { IsCompleted: false })
                    return;

                preloadCancellation?.Cancel();

                // Drop the cancelled task rather than waiting for it to notice: a restart must be possible
                // immediately, without a window where the chart looks loaded but has nothing cached.
                preloadCancellation = null;
                preloadTask = null;
                IsPrepared = false;
            }
        }

        private void startPreload(HashSet<string> keysoundFiles)
        {
            lock (syncRoot)
            {
                preloadCancellation = new CancellationTokenSource();
                CancellationToken token = preloadCancellation.Token;
                preloadTask = Task.Run(() => preload(keysoundFiles, token), token);
            }
        }

        private void preload(HashSet<string> keysoundFiles, CancellationToken token)
        {
            // The folder scan is disk work as well, and doing it here keeps it off the update thread.
            lock (syncRoot)
                folderIndex ??= BmsFolderSampleIndex.TryBuild(bmsFolder);

            int loaded = 0;
            int missing = 0;

            foreach (string filename in keysoundFiles)
            {
                if (token.IsCancellationRequested || IsDisposed)
                    return;

                if (loadIntoCache(filename) != null)
                    loaded++;
                else
                    missing++;
            }

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
        /// Returns a prepared sample for skin / drawable <see cref="osu.Game.Skinning.ISkin.GetSample"/>.
        /// Resolves on demand when preloading is disabled, cancelled, or has not reached the sample yet, so
        /// previews and note keysounds never go silent.
        /// </summary>
        public ISample? GetPreparedSample(string filename)
        {
            if (IsDisposed || string.IsNullOrEmpty(filename))
                return null;

            string cacheKey = filename.ToLowerInvariant();

            lock (syncRoot)
            {
                if (keysoundCache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            return loadIntoCache(filename);
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

            lock (syncRoot)
            {
                preloadCancellation?.Cancel();

                foreach (var sample in keysoundCache.Values)
                    (sample as IDisposable)?.Dispose();

                keysoundCache.Clear();
            }

            backgroundEvents.Clear();
        }

        private ISample? loadIntoCache(string filename)
        {
            if (string.IsNullOrEmpty(filename) || sampleStore == null)
                return null;

            string cacheKey = filename.ToLowerInvariant();
            string? relative;

            lock (syncRoot)
            {
                if (keysoundCache.TryGetValue(cacheKey, out var cached))
                    return cached;

                relative = (folderIndex ??= BmsFolderSampleIndex.TryBuild(bmsFolder))?.TryResolveRelativePath(filename);
            }

            if (relative == null)
            {
                // Remember the miss so a repeated lookup of a missing file does not re-probe the folder.
                lock (syncRoot)
                    keysoundCache[cacheKey] = null!;

                return null;
            }

            try
            {
                var sample = sampleStore.Get(relative);

                lock (syncRoot)
                    keysoundCache[cacheKey] = sample!;

                return sample;
            }
            catch (Exception ex)
            {
                Logger.Log($"{bms_log_prefix} Load failed: {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);

                lock (syncRoot)
                    keysoundCache[cacheKey] = null!;

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
