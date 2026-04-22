// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    /// <summary>
    /// Minimal <see cref="WorkingBeatmap"/> for offline analytics: pre-built mania beatmap, no keysounds, no background IO.
    /// </summary>
    internal sealed class BmsAnalyticsManiaWorkingBeatmap : WorkingBeatmap
    {
        private readonly AudioManager audio;
        private readonly ManiaBeatmap maniaBeatmap;
        private readonly double length;

        public BmsAnalyticsManiaWorkingBeatmap(BeatmapInfo beatmapInfo, ManiaBeatmap maniaBeatmap, AudioManager audioManager)
            : base(beatmapInfo, audioManager)
        {
            audio = audioManager;
            this.maniaBeatmap = maniaBeatmap;

            if (maniaBeatmap.HitObjects.Count > 0)
                length = maniaBeatmap.HitObjects[^1].StartTime + 2000;
        }

        protected override IBeatmap GetBeatmap() => maniaBeatmap;

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token) => maniaBeatmap;

        public override Texture? GetBackground() => null;

        protected override Track GetBeatmapTrack() => audio.Tracks.GetVirtual(Math.Max(length, 1000));

        protected override ISkin GetSkin() => null!;

        public override Stream? GetStream(string storagePath) => null;

        protected override Storyboard GetStoryboard() => new Storyboard { BeatmapInfo = BeatmapInfo, Beatmap = maniaBeatmap };
    }
}
