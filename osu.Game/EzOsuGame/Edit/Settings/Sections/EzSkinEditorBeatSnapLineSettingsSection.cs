// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorBeatSnapLineSettingsSection : EzSkinEditorSettingsGroup
    {
        [Resolved]
        private EzConfigStore configStore { get; set; } = null!;

        private ManiaEditorBeatLinePalette palette => configStore.BeatmapEditor.Colouring.BeatLines;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzSkinEditorBeatSnapLineSettingsSection()
            : base(EzEditorStrings.GROUP_BEAT_SNAP_LINES)
        {
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            palette.EnsureDefaults(colours);

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
            };

            foreach (int divisor in ManiaEditorBeatLinePalette.EDITOR_LINE_DIVISORS)
            {
                flow.Add(SettingsColourExtensions.CreateStyledSettingsColour(
                    FormattableString.Invariant($"1/{divisor}"),
                    palette.GetBindable(divisor)));
            }

            Add(flow);
        }
    }
}
