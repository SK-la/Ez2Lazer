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
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
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
        private readonly BMSWorkingBeatmap sourceWorkingBeatmap;
        private readonly ManiaBeatmap maniaBeatmap;
        private readonly AudioManager audioManager;

        public ManiaConvertedWorkingBeatmap(BMSWorkingBeatmap source, AudioManager audioManager)
            : base(source.BeatmapInfo, audioManager)
        {
            this.sourceWorkingBeatmap = source;
            this.audioManager = audioManager;
            this.maniaBeatmap = ConvertToManiaBeatmap(source);
        }

        /// <summary>
        /// Convert BMS beatmap to Mania beatmap format.
        /// </summary>
        private static ManiaBeatmap ConvertToManiaBeatmap(BMSWorkingBeatmap source)
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

            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(columnCount));

            // Copy metadata - create new instances to avoid sharing references
            maniaBeatmap.BeatmapInfo = bmsBeatmap.BeatmapInfo;
            maniaBeatmap.Difficulty = new BeatmapDifficulty(bmsBeatmap.Difficulty);
            maniaBeatmap.Difficulty.CircleSize = columnCount;
            maniaBeatmap.ControlPointInfo = bmsBeatmap.ControlPointInfo;

            // Convert hit objects with remapped columns
            foreach (var hitObject in bmsBeatmap.HitObjects)
            {
                if (hitObject is not BMSHitObject bmsHitObject)
                    continue;

                // Remap to contiguous column range (0 to columnCount-1)
                if (!columnRemap.TryGetValue(bmsHitObject.Column, out int column))
                    column = 0;

                ManiaHitObject maniaHitObject;

                if (hitObject is BMSHoldNote holdNote)
                {
                    maniaHitObject = new HoldNote
                    {
                        Column = column,
                        StartTime = holdNote.StartTime,
                        Duration = holdNote.Duration,
                        Samples = holdNote.Samples,
                    };
                }
                else if (hitObject is BMSNote)
                {
                    maniaHitObject = new Note
                    {
                        Column = column,
                        StartTime = bmsHitObject.StartTime,
                        Samples = bmsHitObject.Samples,
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
            var processor = rulesetInstance?.CreateBeatmapProcessor(maniaBeatmap);

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
                obj.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty);
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

        public override Texture? GetBackground() => sourceWorkingBeatmap.GetBackground();

        protected override Track GetBeatmapTrack()
        {
            // Use the source's track (virtual track with appropriate length)
            sourceWorkingBeatmap.LoadTrack();
            return sourceWorkingBeatmap.Track;
        }

        protected override ISkin GetSkin() => new BMSSkin(sourceWorkingBeatmap.FolderPath, audioManager);

        public override Stream? GetStream(string storagePath) => sourceWorkingBeatmap.GetStream(storagePath);

        /// <summary>
        /// Gets the source BMSWorkingBeatmap for keysound access.
        /// </summary>
        public BMSWorkingBeatmap SourceBeatmap => sourceWorkingBeatmap;
    }
}
