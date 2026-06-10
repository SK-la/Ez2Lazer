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

        public Bindable<EzSkinEditorPreviewSource> Source { get; } = new Bindable<EzSkinEditorPreviewSource>(EzSkinEditorPreviewSource.Static);

        public Bindable<EzBeatmapPreviewMode> Mode { get; } = new Bindable<EzBeatmapPreviewMode>(EzBeatmapPreviewMode.Static);

        public Bindable<RulesetInfo?> Ruleset { get; } = new Bindable<RulesetInfo?>();

        public Bindable<int?> SuggestedKeyMode { get; } = new Bindable<int?>();

        public IWorkingBeatmap? PreviewBeatmap { get; private set; }

        public void SetStatic()
        {
            Source.Value = EzSkinEditorPreviewSource.Static;
            PreviewBeatmap = null;
            Ruleset.Value = null;
            SuggestedKeyMode.Value = null;
        }

        public void SetBeatmap(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, EzBeatmapPreviewMode mode)
        {
            PreviewBeatmap = workingBeatmap;
            Ruleset.Value = ruleset;
            Mode.Value = mode;
            Source.Value = EzSkinEditorPreviewSource.Beatmap;
            SuggestedKeyMode.Value = resolveKeyModeFromBeatmap(workingBeatmap);
        }

        public bool HasBeatmapLoaded => Source.Value == EzSkinEditorPreviewSource.Beatmap && PreviewBeatmap != null;

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
