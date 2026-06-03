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
    public class ManiaConvertedWorkingBeatmap : WorkingBeatmap
    {
        /// <summary>
        /// Gameplay, previews, and Ez analysis may call <see cref="GetPlayableBeatmap"/> concurrently on the same
        /// instance; in-place ApplyDefaults mutates shared lists → lock for thread safety.
        /// </summary>
        private readonly object playableMutationLock = new object();

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
                ControlPointInfo = bmsBeatmap.ControlPointInfo
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

        protected override IBeatmap GetBeatmap()
        {
            lock (playableMutationLock)
            {
                return maniaBeatmap;
            }
        }

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            lock (playableMutationLock)
            {
                var rulesetInstance = ruleset.CreateInstance();

                foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToDifficulty(maniaBeatmap.Difficulty);
                }

                foreach (var mod in mods.OfType<IApplicableAfterBeatmapConversion>())
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToBeatmap(maniaBeatmap);
                }

                var processor = rulesetInstance.CreateBeatmapProcessor(maniaBeatmap);

                if (processor != null)
                {
                    foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                        mod.ApplyToBeatmapProcessor(processor);

                    processor.PreProcess();
                }

                foreach (var obj in maniaBeatmap.HitObjects)
                {
                    token.ThrowIfCancellationRequested();
                    obj.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty, token);
                }

                processor?.PostProcess();

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
