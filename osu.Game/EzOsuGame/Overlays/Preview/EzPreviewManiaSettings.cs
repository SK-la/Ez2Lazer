// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 全谱预览外观（颜色、厚度、LN 风格）。进程内唯一实例由 <see cref="osu.Game.EzOsuGame.Layout.EzConfigStore"/> 持有，写盘也由它负责。
    /// </summary>
    public sealed class EzPreviewManiaSettings
    {
        public const string FILENAME = "ManiaBeatmapPreviewSettings.json";

        public const float MIN_THICKNESS_SCALE = 0.25f;

        public const float MAX_THICKNESS_SCALE = 3f;

        public const float DEFAULT_THICKNESS_SCALE = 1f;

        public static readonly Colour4 DEFAULT_LANE_BACKGROUND_COLOUR = Colour4.FromHex("#181818");

        public static readonly Colour4 DEFAULT_NOTE_COLOUR = Colour4.FromHex("#00AAFF");

        public static readonly Colour4 DEFAULT_HOLD_HEAD_COLOUR = Colour4.FromHex("#FF5050EB");

        public static readonly Colour4 DEFAULT_HOLD_BODY_COLOUR = Colour4.FromHex("#FFE782B2");

        public BindableColour4 NoteColour { get; } = new BindableColour4(DEFAULT_NOTE_COLOUR);

        public BindableColour4 HoldHeadColour { get; } = new BindableColour4(DEFAULT_HOLD_HEAD_COLOUR);

        public BindableColour4 HoldBodyColour { get; } = new BindableColour4(DEFAULT_HOLD_BODY_COLOUR);

        public BindableColour4 LaneBackgroundColour { get; } = new BindableColour4(DEFAULT_LANE_BACKGROUND_COLOUR);

        public BindableFloat ThicknessScale { get; } = new BindableFloat(DEFAULT_THICKNESS_SCALE)
        {
            MinValue = MIN_THICKNESS_SCALE,
            MaxValue = MAX_THICKNESS_SCALE,
        };

        public Bindable<ManiaLongNoteStyle> Style { get; } = new Bindable<ManiaLongNoteStyle>(ManiaLongNoteStyle.Column);

        public static Colour4 DefaultColourFor(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => DEFAULT_HOLD_HEAD_COLOUR,
            EzManiaNotePart.HoldBody => DEFAULT_HOLD_BODY_COLOUR,
            _ => DEFAULT_NOTE_COLOUR,
        };

        public Colour4 For(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => HoldHeadColour.Value,
            EzManiaNotePart.HoldBody => HoldBodyColour.Value,
            _ => NoteColour.Value,
        };

        public BindableColour4 ColourFor(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => HoldHeadColour,
            EzManiaNotePart.HoldBody => HoldBodyColour,
            _ => NoteColour,
        };

        internal void ApplyDocument(EzPreviewManiaSettingsDocument? document) => document?.ApplyTo(this);
    }

    internal sealed class EzPreviewManiaSettingsDocument
    {
        public string? NoteColour { get; set; }

        public string? HoldHeadColour { get; set; }

        public string? HoldBodyColour { get; set; }

        public string? LaneBackgroundColour { get; set; }

        public float? ThicknessScale { get; set; }

        public string? Style { get; set; }

        public static EzPreviewManiaSettingsDocument From(EzPreviewManiaSettings settings) => new EzPreviewManiaSettingsDocument
        {
            NoteColour = settings.NoteColour.Value.ToHex(),
            HoldHeadColour = settings.HoldHeadColour.Value.ToHex(),
            HoldBodyColour = settings.HoldBodyColour.Value.ToHex(),
            LaneBackgroundColour = settings.LaneBackgroundColour.Value.ToHex(),
            ThicknessScale = settings.ThicknessScale.Value,
            Style = settings.Style.Value.ToString(),
        };

        public void ApplyTo(EzPreviewManiaSettings settings)
        {
            settings.NoteColour.Value = parse(NoteColour, EzPreviewManiaSettings.DEFAULT_NOTE_COLOUR);
            settings.HoldHeadColour.Value = parse(HoldHeadColour, EzPreviewManiaSettings.DEFAULT_HOLD_HEAD_COLOUR);
            settings.HoldBodyColour.Value = parse(HoldBodyColour, EzPreviewManiaSettings.DEFAULT_HOLD_BODY_COLOUR);
            settings.LaneBackgroundColour.Value = parse(LaneBackgroundColour, EzPreviewManiaSettings.DEFAULT_LANE_BACKGROUND_COLOUR);
            settings.ThicknessScale.Value = parseThickness(ThicknessScale);
            settings.Style.Value = parseStyle(Style);
        }

        private static ManiaLongNoteStyle parseStyle(string? value) =>
            Enum.TryParse(value, out ManiaLongNoteStyle style) ? style : ManiaLongNoteStyle.Column;

        private static Colour4 parse(string? hex, Colour4 fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            try
            {
                return Colour4.FromHex(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static float parseThickness(float? value) => Math.Clamp(
            value ?? EzPreviewManiaSettings.DEFAULT_THICKNESS_SCALE,
            EzPreviewManiaSettings.MIN_THICKNESS_SCALE,
            EzPreviewManiaSettings.MAX_THICKNESS_SCALE);
    }
}
