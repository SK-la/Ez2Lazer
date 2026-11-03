// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Local preview state for Ez skin editor. Does not mutate global <see cref="OsuGameBase.Beatmap"/> / <see cref="OsuGameBase.Ruleset"/>.
    /// </summary>
    public sealed class EzSkinEditorPreviewState
    {
        private static readonly int[] available_key_modes = { 0, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        public Bindable<EzBeatmapPreviewMode> Mode { get; } = new Bindable<EzBeatmapPreviewMode>();

        public Bindable<RulesetInfo?> Ruleset { get; } = new Bindable<RulesetInfo?>();

        public Bindable<int?> SuggestedKeyMode { get; } = new Bindable<int?>();

        /// <summary>
        /// When enabled, playfield scenes use the built-in looping chart (mania virtual LN preview).
        /// Other rulesets are not implemented yet.
        /// </summary>
        public BindableBool LoopBeatmap { get; } = new BindableBool();

        public Bindable<IWorkingBeatmap?> PreviewBeatmap { get; } = new Bindable<IWorkingBeatmap?>();

        public void SetBeatmap(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, EzBeatmapPreviewMode mode)
        {
            PreviewBeatmap.Value = workingBeatmap;
            Ruleset.Value = ruleset;
            Mode.Value = mode;
            SuggestedKeyMode.Value = resolveKeyModeFromBeatmap(workingBeatmap);
        }

        public bool HasBeatmapLoaded => PreviewBeatmap.Value != null;

        public void PauseBeatmapPlayback() => Mode.Value = EzBeatmapPreviewMode.Static;

        public void ResumeBeatmapPlayback() => Mode.Value = EzBeatmapPreviewMode.Dynamic;

        private static int? resolveKeyModeFromBeatmap(IWorkingBeatmap workingBeatmap)
        {
            if (workingBeatmap.BeatmapInfo.Ruleset.OnlineID != 3)
                return null;

            int keyCount = (int)workingBeatmap.Beatmap.Difficulty.CircleSize;

            foreach (int mode in available_key_modes)
            {
                if (mode == keyCount)
                    return mode;
            }

            return null;
        }
    }
}
