// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// All EzStylePro dedicated settings for Appearance → Skin-specific.
    /// </summary>
    public partial class EzStyleProSkinScopedSettingsControls : FillFlowContainer
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzStyleProSkinScopedSettingsControls()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var nameOfJudgementLine = ezSkinConfig.GetBindable<string>(Ez2Setting.JudgementLineName);
            var judgementLines = new List<string> { string.Empty };
            judgementLines.AddRange(EzResourceDiscovery.ListJudgementLineCandidates(storage));

            if (!string.IsNullOrEmpty(nameOfJudgementLine.Value) && !judgementLines.Contains(nameOfJudgementLine.Value))
                judgementLines.Add(nameOfJudgementLine.Value);

            Children = new Drawable[]
            {
                new EzOptionalStringDropdown
                {
                    LabelText = EzSkinStrings.JUDGEMENT_LINE_NAME,
                    TooltipText = EzSkinStrings.JUDGEMENT_LINE_NAME_TOOLTIP,
                    EmptyLabel = EzSkinStrings.JUDGEMENT_LINE_NONE,
                    Current = nameOfJudgementLine,
                    Items = judgementLines,
                },
                new EzHitTargetScopedSettingsControls(),
                new EzBuiltInLnScopedSettingsControls(),
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.NOTE_TRACK_LINE,
                    TooltipText = EzSkinStrings.NOTE_TRACK_LINE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
                },
            };
        }
    }
}
