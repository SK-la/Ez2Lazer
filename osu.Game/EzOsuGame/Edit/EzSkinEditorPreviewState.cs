// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Local preview state for Ez skin editor. Does not mutate global <see cref="OsuGame.Beatmap"/> / <see cref="OsuGame.Ruleset"/>.
    /// </summary>
    public sealed class EzSkinEditorPreviewState
    {
        public Bindable<EzSkinEditorPreviewSource> Source { get; } = new Bindable<EzSkinEditorPreviewSource>(EzSkinEditorPreviewSource.Static);

        public Bindable<EzBeatmapPreviewMode> Mode { get; } = new Bindable<EzBeatmapPreviewMode>(EzBeatmapPreviewMode.Static);

        public Bindable<RulesetInfo?> Ruleset { get; } = new Bindable<RulesetInfo?>();

        public IWorkingBeatmap? PreviewBeatmap { get; private set; }

        public void SetStatic()
        {
            Source.Value = EzSkinEditorPreviewSource.Static;
            PreviewBeatmap = null;
            Ruleset.Value = null;
        }

        public void SetBeatmap(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, EzBeatmapPreviewMode mode)
        {
            PreviewBeatmap = workingBeatmap;
            Ruleset.Value = ruleset;
            Mode.Value = mode;
            Source.Value = EzSkinEditorPreviewSource.Beatmap;
        }
    }
}
