// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Shared dependencies passed into scene strategies when building content and sidebar groups.
    /// </summary>
    public sealed class EzSkinEditorSceneContext
    {
        public ISkinEditorVirtualProvider? Provider { get; init; }

        public ISkin EditorSkin { get; init; } = null!;

        public EzSkinIniSession? SkinIniSession { get; init; }

        public Action? RequestSceneRefresh { get; init; }

        public Action? CommitSkinIni { get; init; }

        public EzSkinEditorPreviewSource PreviewSource { get; init; } = EzSkinEditorPreviewSource.Static;

        public IWorkingBeatmap? PreviewBeatmap { get; init; }

        public RulesetInfo? PreviewRuleset { get; init; }

        public EzBeatmapPreviewMode PreviewMode { get; init; } = EzBeatmapPreviewMode.Static;
    }
}
