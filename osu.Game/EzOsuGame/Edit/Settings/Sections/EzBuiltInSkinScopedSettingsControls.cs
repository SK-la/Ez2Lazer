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
    /// 内置代码皮肤在 Appearance「皮肤专用」分组下的专用设置。按皮肤种类只渲染其生效的项。
    /// </summary>
    public partial class EzBuiltInSkinScopedSettingsControls : FillFlowContainer
    {
        public EzSkinEditorSkinKind Kind { get; }

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzBuiltInSkinScopedSettingsControls(EzSkinEditorSkinKind kind)
        {
            Kind = kind;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var children = new List<Drawable>();

            switch (Kind)
            {
                case EzSkinEditorSkinKind.EzStylePro:
                    // children.Add(createJudgementLineDropdown()); //暂时不显示
                    children.AddRange(createHitTargetSettings());
                    children.AddRange(createLnSettings());
                    children.Add(createNoteTrackLineSlider());
                    break;

                case EzSkinEditorSkinKind.Ez2:
                    children.AddRange(createHitTargetSettings());
                    break;

                case EzSkinEditorSkinKind.SbI:
                    children.AddRange(createLnSettings());
                    break;
            }

            Children = children.ToArray();
        }

        private Drawable createJudgementLineDropdown()
        {
            var nameOfJudgementLine = ezSkinConfig.GetBindable<string>(Ez2Setting.JudgementLineName);
            var judgementLines = new List<string> { string.Empty };
            judgementLines.AddRange(EzResourceDiscovery.ListJudgementLineCandidates(storage));

            if (!string.IsNullOrEmpty(nameOfJudgementLine.Value) && !judgementLines.Contains(nameOfJudgementLine.Value))
                judgementLines.Add(nameOfJudgementLine.Value);

            return new EzOptionalStringDropdown
            {
                LabelText = EzSkinStrings.JUDGEMENT_LINE_NAME,
                TooltipText = EzSkinStrings.JUDGEMENT_LINE_NAME_TOOLTIP,
                EmptyLabel = EzSkinStrings.JUDGEMENT_LINE_NONE,
                Current = nameOfJudgementLine,
                Items = judgementLines,
            };
        }

        private IEnumerable<Drawable> createHitTargetSettings() => new Drawable[]
        {
            new SettingsSlider<double>
            {
                LabelText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED,
                TooltipText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED_TOOLTIP,
                Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                KeyboardStep = 0.1f,
            },
            new SettingsSlider<double>
            {
                LabelText = EzSkinStrings.HIT_TARGET_ALPHA,
                TooltipText = EzSkinStrings.HIT_TARGET_ALPHA_TOOLTIP,
                Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                KeyboardStep = 0.01f,
            },
        };

        private IEnumerable<Drawable> createLnSettings() => new Drawable[]
        {
            new SettingsCheckbox
            {
                LabelText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE,
                TooltipText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE_TOOLTIP,
                Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.ManiaLNGradientEnable),
            },
            new SettingsCheckbox
            {
                LabelText = EzSkinStrings.LN_DYNAMIC_TAIL,
                TooltipText = EzSkinStrings.LN_DYNAMIC_TAIL_TOOLTIP,
                Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.ManiaHoldTailMaskDynamicEnable),
            },
            new SettingsSlider<double>
            {
                LabelText = EzSkinStrings.LN_GRADIENT_TAIL_HEIGHT,
                TooltipText = EzSkinStrings.LN_GRADIENT_TAIL_HEIGHT_TOOLTIP,
                Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight),
                KeyboardStep = 1.0f,
            },
            new SettingsSlider<double>
            {
                LabelText = EzSkinStrings.LN_TAIL_ALPHA,
                TooltipText = EzSkinStrings.LN_TAIL_ALPHA_TOOLTIP,
                Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha),
                KeyboardStep = 0.1f,
            },
        };

        private Drawable createNoteTrackLineSlider() => new SettingsSlider<double>
        {
            LabelText = EzSkinStrings.NOTE_TRACK_LINE,
            TooltipText = EzSkinStrings.NOTE_TRACK_LINE_TOOLTIP,
            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
        };
    }
}
