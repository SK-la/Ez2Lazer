// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// 谱面预览场景的全谱预览外观：绘制风格、note 厚度、按部件的颜色。
    /// </summary>
    public partial class BeatmapPreviewManiaSettingsSection : EzSkinEditorSettingsGroup
    {
        [Resolved]
        private EzConfigStore configStore { get; set; } = null!;

        private EzPreviewManiaSettings settings => configStore.PreviewManiaSettings;

        public BeatmapPreviewManiaSettingsSection()
            : base(EzEditorStrings.GROUP_BEATMAP_PREVIEW_MANIA)
        {
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Children = new Drawable[]
            {
                new SettingsEnumDropdown<ManiaLongNoteStyle>
                {
                    LabelText = ManiaBeatmapPreviewStrings.LN_STYLE,
                    Current = settings.Style,
                },
                new SettingsSlider<float>
                {
                    LabelText = ManiaBeatmapPreviewStrings.NOTE_THICKNESS,
                    Current = settings.ThicknessScale,
                    KeyboardStep = 0.05f,
                },
                new EzSettingsColour
                {
                    LabelText = ManiaBeatmapPreviewStrings.COLOUR_LANE_BACKGROUND,
                    Current = settings.LaneBackgroundColour,
                },
                new EzSettingsColour
                {
                    LabelText = ManiaBeatmapPreviewStrings.COLOUR_NOTE,
                    Current = settings.NoteColour,
                },
                new EzSettingsColour
                {
                    LabelText = ManiaBeatmapPreviewStrings.COLOUR_HOLD_HEAD,
                    Current = settings.HoldHeadColour,
                },
                new EzSettingsColour
                {
                    LabelText = ManiaBeatmapPreviewStrings.COLOUR_HOLD_BODY,
                    Current = settings.HoldBodyColour,
                },
                // new EzSettingsColour
                // {
                //     LabelText = ManiaBeatmapPreviewStrings.COLOUR_HOLD_TAIL,
                //     Current = settings.HoldTailColour,
                // },
            };
        }
    }

    public static class ManiaBeatmapPreviewStrings
    {
        public static readonly LocalisableString LN_STYLE = new EzLocalizationManager.EzLocalisableString("绘制风格", "Draw style");

        public static readonly LocalisableString BEATMAP_NOT_MANIA = new EzLocalizationManager.EzLocalisableString(
            "当前谱面不是 mania。请切换至 mania 谱面来预览。",
            "The current beatmap is not mania. Switch to a mania beatmap to preview.");

        public static readonly LocalisableString NOTE_THICKNESS = new EzLocalizationManager.EzLocalisableString("note 厚度", "Note thickness");

        public static readonly LocalisableString COLOUR_LANE_BACKGROUND = new EzLocalizationManager.EzLocalisableString("轨道背景颜色", "Lane background colour");

        public static readonly LocalisableString COLOUR_NOTE = new EzLocalizationManager.EzLocalisableString("普通 note 颜色", "Note colour");

        public static readonly LocalisableString COLOUR_HOLD_HEAD = new EzLocalizationManager.EzLocalisableString("长条面头颜色", "Hold head colour");

        public static readonly LocalisableString COLOUR_HOLD_BODY = new EzLocalizationManager.EzLocalisableString("长条身颜色", "Hold body colour");

        public static readonly LocalisableString COLOUR_HOLD_TAIL = new EzLocalizationManager.EzLocalisableString("长条尾颜色", "Hold tail colour");
    }
}
