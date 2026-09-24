// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Wraps a BMS chart as a pre-converted Mania beatmap, so gameplay uses full mania pipelines.
    /// </summary>
    /// <remarks>
    /// <see cref="GetBeatmap"/> exposes the display copy built in the constructor, and
    /// <see cref="GetPlayableBeatmap"/> derives a fresh working copy from the BMS source on every call.
    /// The two never share mutable state: conversion mods, difficulty mods, processors and
    /// <see cref="HitObject.ApplyDefaults"/> all rewrite the beatmap in place, so applying them to the
    /// display copy would stack their effect across calls (mirroring twice restores the original, a
    /// reshuffle reshuffles again) and leak it into every other consumer of this working beatmap.
    /// </remarks>
    public class ManiaConvertedWorkingBeatmap : WorkingBeatmap
    {
        private readonly ManiaBeatmap maniaBeatmap;
        private readonly AudioManager audioManager;
        private readonly double beatmapLength;

        /// <param name="source">Parsed BMS working beatmap.</param>
        /// <param name="audioManager">Used only for a virtual timeline track (no sample IO when <paramref name="preloadKeysounds"/> is false).</param>
        /// <param name="preloadKeysounds">
        /// When <see langword="false"/>, skips keysound sample IO (for offline analytics scans).
        /// Gameplay and previews should keep the default <see langword="true"/>.
        /// </param>
        public ManiaConvertedWorkingBeatmap(BMSWorkingBeatmap source, AudioManager audioManager, bool preloadKeysounds = true)
            : base(source.BeatmapInfo, audioManager)
        {
            SourceBeatmap = source;
            this.audioManager = audioManager;

            maniaBeatmap = ConvertToManiaBeatmap(source.Beatmap);

            if (preloadKeysounds)
            {
                KeysoundManager = source.KeysoundManager ?? new BmsKeysoundManager(audioManager, SourceBeatmap.FolderPath);

                if (!KeysoundManager.IsPrepared)
                {
                    if (source.Beatmap is BMSBeatmap bmsBeatmap)
                        KeysoundManager.Prepare(maniaBeatmap.HitObjects, bmsBeatmap.BackgroundSoundEvents);
                    else
                        KeysoundManager.Prepare(maniaBeatmap.HitObjects);
                }

                BmsRuntimeAudioContext.RegisterKeysoundManager(KeysoundManager);
            }

            if (maniaBeatmap.HitObjects.Count > 0)
                beatmapLength = maniaBeatmap.HitObjects.Max(h => h.GetEndTime()) + 2000;
        }

        /// <remarks>
        /// Idempotent: an input that is already a <see cref="ManiaBeatmap"/> is returned unchanged, so a caller that
        /// intends to mutate the result must derive its own copy first.
        /// </remarks>
        public static ManiaBeatmap ConvertToManiaBeatmap(IBeatmap bmsBeatmap)
        {
            if (bmsBeatmap is ManiaBeatmap existing)
                return existing;

            var usedColumns = bmsBeatmap.HitObjects
                                        .OfType<BMSHitObject>()
                                        .Select(h => h.Column)
                                        .Distinct()
                                        .OrderBy(c => c)
                                        .ToList();

            var columnRemap = new Dictionary<int, int>();
            for (int i = 0; i < usedColumns.Count; i++)
                columnRemap[usedColumns[i]] = i;

            int columnCount = usedColumns.Count > 0 ? usedColumns.Count : 1;

            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(columnCount))
            {
                BeatmapInfo = bmsBeatmap.BeatmapInfo,
                Difficulty = new BeatmapDifficulty(bmsBeatmap.Difficulty)
                {
                    CircleSize = columnCount
                },
                // 产物自己持有一份 control points：mod、processor 与 ApplyDefaults 都会就地改写它，
                // 共享源的实例等于把 mania 侧的改写写回 BMS 源谱面。
                ControlPointInfo = bmsBeatmap.ControlPointInfo.DeepClone()
            };

            foreach (var hitObject in bmsBeatmap.HitObjects.OfType<BMSHitObject>())
            {
                int column = columnRemap.GetValueOrDefault(hitObject.Column, 0);

                var samples = hitObject.Samples.ToList();

                ManiaHitObject maniaHitObject = hitObject switch
                {
                    BMSHoldNote holdNote => new BmsManiaHoldNote
                    {
                        Column = column,
                        StartTime = holdNote.StartTime,
                        Duration = holdNote.Duration,
                        Samples = samples,
                        KeysoundSamples = samples,
                        IsScratch = holdNote.IsScratch,
                    },
                    _ => new BmsManiaNote
                    {
                        Column = column,
                        StartTime = hitObject.StartTime,
                        Samples = samples,
                        KeysoundSamples = samples,
                        IsScratch = hitObject.IsScratch,
                    }
                };

                maniaHitObject.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty);
                maniaBeatmap.HitObjects.Add(maniaHitObject);
            }

            return maniaBeatmap;
        }

        protected override IBeatmap GetBeatmap() => maniaBeatmap;

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            => CreatePlayableFromSource(SourceBeatmap.Beatmap, ruleset, mods, token);

        /// <summary>
        /// Derives a fresh playable mania beatmap from a BMS source chart, applying the mods and defaults pipeline.
        /// </summary>
        /// <remarks>
        /// Every call builds a brand-new copy. Conversion mods, difficulty mods, processors and
        /// <see cref="HitObject.ApplyDefaults"/> all rewrite the beatmap in place, so reusing one instance across
        /// calls would stack their effect (mirroring twice restores the original, a reshuffle reshuffles again)
        /// and leak it into every other consumer — including the display copy returned by <see cref="GetBeatmap"/>.
        /// <para>
        /// <paramref name="source"/> is expected to be a BMS chart. An already-mania source is handed back
        /// unchanged by <see cref="ConvertToManiaBeatmap"/> and would therefore be rewritten in place, so such a
        /// caller must derive its own copy first.
        /// </para>
        /// </remarks>
        internal static ManiaBeatmap CreatePlayableFromSource(IBeatmap source, IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            // ConvertToManiaBeatmap 已经为产物深拷贝了 control points，这里不再重复拷贝。
            ManiaBeatmap playable = ConvertToManiaBeatmap(source);

            var rulesetInstance = ruleset.CreateInstance();

            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToDifficulty(playable.Difficulty);
            }

            foreach (var mod in mods.OfType<IApplicableAfterBeatmapConversion>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToBeatmap(playable);
            }

            var processor = rulesetInstance.CreateBeatmapProcessor(playable);

            if (processor != null)
            {
                foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                    mod.ApplyToBeatmapProcessor(processor);

                processor.PreProcess();
            }

            foreach (var obj in playable.HitObjects)
            {
                token.ThrowIfCancellationRequested();
                obj.ApplyDefaults(playable.ControlPointInfo, playable.Difficulty, token);
            }

            processor?.PostProcess();

            foreach (var mod in mods.OfType<IApplicableToHitObject>())
            {
                foreach (var obj in playable.HitObjects)
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToHitObject(obj);
                }
            }

            return playable;
        }

        public override Texture? GetBackground() => SourceBeatmap.GetBackground();

        protected override Storyboard GetStoryboard() => SourceBeatmap.Storyboard;

        protected override Track GetBeatmapTrack() => audioManager.Tracks.GetVirtual(Math.Max(beatmapLength, 60000));

        protected override ISkin GetSkin()
        {
            if (KeysoundManager == null)
                return null!;

            return new BMSSkin(KeysoundManager);
        }

        public override Stream? GetStream(string storagePath) => SourceBeatmap.GetStream(storagePath);

        public BmsKeysoundManager? KeysoundManager { get; }

        public BMSWorkingBeatmap SourceBeatmap { get; }
    }
}
