// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// A WorkingBeatmap that wraps a converted ManiaBeatmap from BMS.
    /// This allows BMS files to be played using osu!'s standard Mania Player flow.
    /// </summary>
    public class ManiaConvertedWorkingBeatmap : WorkingBeatmap
    {
        private readonly ManiaBeatmap maniaBeatmap;
        private readonly AudioManager audioManager;
        private double beatmapLength;

        public ManiaConvertedWorkingBeatmap(BMSWorkingBeatmap source, AudioManager audioManager)
            : base(source.BeatmapInfo, audioManager)
        {
            SourceBeatmap = source;
            this.audioManager = audioManager;

            // Force loading of the BMS beatmap to ensure BackgroundSoundEvents are available
            var bmsBeatmap = source.Beatmap as BMSBeatmap;
            if (bmsBeatmap == null)
            {
                Logger.Log($"[BMS] ERROR: Source beatmap is not BMSBeatmap (is {source.Beatmap?.GetType().Name ?? "null"})", LoggingTarget.Runtime, LogLevel.Error);
                bmsBeatmap = new BMSBeatmap();
            }
            else
            {
                Logger.Log($"[BMS] Source BMSBeatmap loaded, {bmsBeatmap.BackgroundSoundEvents.Count} background sound events", LoggingTarget.Runtime, LogLevel.Debug);
            }

            maniaBeatmap = convertToManiaBeatmap(source);

            // Initialize keysound manager
            KeysoundManager = new BmsKeysoundManager(audioManager, SourceBeatmap.FolderPath);
            Logger.Log("[BMS] ManiaConvertedWorkingBeatmap created, initialized BmsKeysoundManager", LoggingTarget.Runtime, LogLevel.Debug);
            Logger.Log($"[BMS] BMS folder: {SourceBeatmap.FolderPath}", LoggingTarget.Runtime, LogLevel.Debug);

            // List audio files in folder for debugging
            try
            {
                var audioFiles = Directory.GetFiles(SourceBeatmap.FolderPath, "*.*")
                    .Where(f => new[] { ".wav", ".ogg", ".mp3", ".flac" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(Path.GetFileName)
                    .ToList();
                Logger.Log($"[BMS] Found {audioFiles.Count} audio files in folder", LoggingTarget.Runtime, LogLevel.Debug);
                if (audioFiles.Count > 0 && audioFiles.Count <= 10)
                {
                    Logger.Log($"[BMS] Audio files: {string.Join(", ", audioFiles.Take(10))}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to list audio files: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Preload keysounds from hit objects
            Logger.Log($"[BMS] Preloading keysounds from {maniaBeatmap.HitObjects.Count} hit objects", LoggingTarget.Runtime, LogLevel.Debug);
            KeysoundManager.PreloadKeysounds(maniaBeatmap.HitObjects);

            // Set and preload background sound events
            Logger.Log($"[BMS] Setting {bmsBeatmap.BackgroundSoundEvents.Count} background sound events", LoggingTarget.Runtime, LogLevel.Debug);
            KeysoundManager.SetBackgroundSoundEvents(bmsBeatmap.BackgroundSoundEvents);

            // Preload background sound keysounds
            var backgroundKeysounds = bmsBeatmap.BackgroundSoundEvents.Select(e => e.Filename).Distinct().ToList();
            Logger.Log($"[BMS] 🔊 Starting to preload {backgroundKeysounds.Count} unique background sound files...", LoggingTarget.Runtime, LogLevel.Important);

            int loadedCount = 0;
            int failedCount = 0;
            foreach (var filename in backgroundKeysounds)
            {
                var sample = KeysoundManager.LoadKeysound(filename);
                if (sample != null)
                    loadedCount++;
                else
                    failedCount++;
            }

            Logger.Log($"[BMS] 🔊 Background sounds: {loadedCount} loaded, {failedCount} failed", LoggingTarget.Runtime, LogLevel.Important);


            // Calculate beatmap length
            if (maniaBeatmap.HitObjects.Count > 0)
            {
                beatmapLength = maniaBeatmap.HitObjects.Max(h => h.GetEndTime()) + 2000;
            }
        }

        /// <summary>
        /// Convert BMS beatmap to Mania beatmap format.
        /// </summary>
        private static ManiaBeatmap convertToManiaBeatmap(BMSWorkingBeatmap source)
        {
            var bmsBeatmap = source.Beatmap;

            // Collect all unique columns and remap to 0-indexed contiguous range
            var usedColumns = bmsBeatmap.HitObjects
                                        .OfType<BMSHitObject>()
                                        .Select(h => h.Column)
                                        .Distinct()
                                        .OrderBy(c => c)
                                        .ToList();

            // Create column remapping dictionary
            var columnRemap = new Dictionary<int, int>();

            for (int i = 0; i < usedColumns.Count; i++)
            {
                columnRemap[usedColumns[i]] = i;
            }

            int columnCount = usedColumns.Count;
            if (columnCount <= 0) columnCount = 7;

            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(columnCount))
            {
                // Copy metadata - create new instances to avoid sharing references
                BeatmapInfo = bmsBeatmap.BeatmapInfo,
                Difficulty = new BeatmapDifficulty(bmsBeatmap.Difficulty)
                {
                    CircleSize = columnCount
                },
                ControlPointInfo = bmsBeatmap.ControlPointInfo
            };

            // Convert hit objects with remapped columns
            foreach (var hitObject in bmsBeatmap.HitObjects)
            {
                if (hitObject is not BMSHitObject bmsHitObject)
                    continue;

                // Remap to contiguous column range (0 to columnCount-1)
                int column = columnRemap.GetValueOrDefault(bmsHitObject.Column, 0);

                ManiaHitObject maniaHitObject;

                if (hitObject is BMSHoldNote holdNote)
                {
                    maniaHitObject = new BmsManiaHoldNote
                    {
                        Column = column,
                        IsScratch = holdNote.IsScratch,
                        StartTime = holdNote.StartTime,
                        Duration = holdNote.Duration,
                        KeysoundSamples = holdNote.Samples.ToList(),
                        Samples = new List<HitSampleInfo>(),
                    };
                }
                else if (hitObject is BMSNote)
                {
                    maniaHitObject = new BmsManiaNote
                    {
                        Column = column,
                        IsScratch = bmsHitObject.IsScratch,
                        StartTime = bmsHitObject.StartTime,
                        KeysoundSamples = bmsHitObject.Samples.ToList(),
                        Samples = new List<HitSampleInfo>(),
                    };
                }
                else
                {
                    continue;
                }

                // Apply defaults to initialize HitWindows
                maniaHitObject.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty);
                maniaBeatmap.HitObjects.Add(maniaHitObject);
            }

            return maniaBeatmap;
        }

        protected override IBeatmap GetBeatmap() => maniaBeatmap;

        /// <summary>
        /// Override to return the already-converted ManiaBeatmap directly,
        /// bypassing the normal beatmap conversion process.
        /// </summary>
        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            // The beatmap is already converted to Mania format, so we just return it directly
            // after applying any necessary mod processing
            var rulesetInstance = ruleset.CreateInstance();

            // Apply difficulty mods
            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToDifficulty(maniaBeatmap.Difficulty);
            }

            // Apply conversion mods to the result
            foreach (var mod in mods.OfType<IApplicableAfterBeatmapConversion>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToBeatmap(maniaBeatmap);
            }

            // Process beatmap
            var processor = rulesetInstance.CreateBeatmapProcessor(maniaBeatmap);

            if (processor != null)
            {
                foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                    mod.ApplyToBeatmapProcessor(processor);

                processor.PreProcess();
            }

            // Compute default values for hitobjects
            foreach (var obj in maniaBeatmap.HitObjects)
            {
                token.ThrowIfCancellationRequested();
                obj.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty, token);
            }

            processor?.PostProcess();

            // Apply mods to hit objects
            foreach (var mod in mods.OfType<IApplicableToHitObject>())
            {
                foreach (var obj in maniaBeatmap.HitObjects)
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToHitObject(obj);
                }
            }

            return maniaBeatmap;
        }

        public override Texture? GetBackground() => SourceBeatmap.GetBackground();

        protected override Track GetBeatmapTrack()
        {
            // Return a virtual track for BMS - keysounds drive the audio
            // This virtual track is used as the main timing reference
            return audioManager.Tracks.GetVirtual(Math.Max(beatmapLength, 60000));
        }

        /// <summary>
        /// Gets the BMS keysound manager for gameplay integration.
        /// </summary>
        public BmsKeysoundManager? KeysoundManager { get; }

        protected override ISkin GetSkin() => new BMSSkin(SourceBeatmap.FolderPath, audioManager);

        public override Stream? GetStream(string storagePath) => SourceBeatmap.GetStream(storagePath);

        /// <summary>
        /// Gets the source BMSWorkingBeatmap for keysound access.
        /// </summary>
        public BMSWorkingBeatmap SourceBeatmap { get; }
    }
}




