// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 全谱预览的绘制外观：按 note 部件的颜色，以及作用于普通 note 与面头的厚度缩放。
    /// 与游戏内皮肤无关，只影响全谱预览组件。改动即预览色板，由协调器/预览屏直接读取。
    /// </summary>
    public sealed class EzPreviewManiaSettings
    {
        public const string FILENAME = "ManiaBeatmapPreviewSettings.json";

        /// <summary>厚度缩放，1 为基准。</summary>
        public const float MIN_THICKNESS_SCALE = 0.25f;

        public const float MAX_THICKNESS_SCALE = 3f;

        public const float DEFAULT_THICKNESS_SCALE = 1f;

        public static readonly Colour4 DEFAULT_LANE_BACKGROUND_COLOUR = Colour4.FromHex("#181818");

        public static readonly Colour4 DEFAULT_NOTE_COLOUR = Colour4.FromHex("#00AAFF");

        public static readonly Colour4 DEFAULT_HOLD_HEAD_COLOUR = Colour4.FromHex("#FF5050EB");

        public static readonly Colour4 DEFAULT_HOLD_BODY_COLOUR = Colour4.FromHex("#FFE782B2");

        // public static readonly Colour4 DEFAULT_HOLD_TAIL_COLOUR = Colour4.FromHex("#FFFF00");

        /// <summary>
        /// 共享默认实例。没有用户设置的调用点（如预览叠层）从这里取默认值，不再各自重复字面量。
        /// </summary>
        public static readonly EzPreviewManiaSettings DEFAULT = new EzPreviewManiaSettings();

        /// <summary>
        /// 单个部件的默认颜色。设置缺省（如 <see cref="ManiaPreviewDrawHelper"/> 无设置可读）时用。
        /// </summary>
        public static Colour4 DefaultColourFor(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => DEFAULT_HOLD_HEAD_COLOUR,
            EzManiaNotePart.HoldBody => DEFAULT_HOLD_BODY_COLOUR,
            // EzManiaNotePart.HoldTail => DEFAULT_HOLD_TAIL_COLOUR,
            _ => DEFAULT_NOTE_COLOUR,
        };

        public BindableColour4 NoteColour { get; } = new BindableColour4(DEFAULT_NOTE_COLOUR);

        public BindableColour4 HoldHeadColour { get; } = new BindableColour4(DEFAULT_HOLD_HEAD_COLOUR);

        public BindableColour4 HoldBodyColour { get; } = new BindableColour4(DEFAULT_HOLD_BODY_COLOUR);

        // public BindableColour4 HoldTailColour { get; } = new BindableColour4(DEFAULT_HOLD_TAIL_COLOUR); //不绘制Tail

        /// <summary>全键道统一底色。交替配色预留为后续扩展，当前仅使用本项。</summary>
        public BindableColour4 LaneBackgroundColour { get; } = new BindableColour4(DEFAULT_LANE_BACKGROUND_COLOUR);

        public BindableFloat ThicknessScale { get; } = new BindableFloat(DEFAULT_THICKNESS_SCALE)
        {
            MinValue = MIN_THICKNESS_SCALE,
            MaxValue = MAX_THICKNESS_SCALE,
        };

        public Bindable<ManiaLongNoteStyle> Style { get; } = new Bindable<ManiaLongNoteStyle>(ManiaLongNoteStyle.Column);

        public Colour4 For(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => HoldHeadColour.Value,
            EzManiaNotePart.HoldBody => HoldBodyColour.Value,
            // EzManiaNotePart.HoldTail => HoldTailColour.Value,
            _ => NoteColour.Value,
        };

        public BindableColour4 ColourFor(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => HoldHeadColour,
            EzManiaNotePart.HoldBody => HoldBodyColour,
            // EzManiaNotePart.HoldTail => HoldTailColour,
            _ => NoteColour,
        };

        /// <summary>把另一份设置的值覆盖到本实例（同一批 bindable，不替换引用）。</summary>
        public void CopyTo(EzPreviewManiaSettings target)
        {
            target.NoteColour.Value = NoteColour.Value;
            target.HoldHeadColour.Value = HoldHeadColour.Value;
            target.HoldBodyColour.Value = HoldBodyColour.Value;
            // target.HoldTailColour.Value = HoldTailColour.Value;
            target.LaneBackgroundColour.Value = LaneBackgroundColour.Value;
            target.ThicknessScale.Value = ThicknessScale.Value;
            target.Style.Value = Style.Value;
        }
    }

    /// <summary>
    /// 存档格式。字段缺失时回落到默认调色板/厚度。
    /// </summary>
    internal sealed class EzPreviewManiaSettingsDocument
    {
        public string? NoteColour { get; set; }

        public string? HoldHeadColour { get; set; }

        public string? HoldBodyColour { get; set; }

        // public string? HoldTailColour { get; set; }

        public string? LaneBackgroundColour { get; set; }

        public float? ThicknessScale { get; set; }

        public string? Style { get; set; }

        public static EzPreviewManiaSettingsDocument From(EzPreviewManiaSettings maniaSettings) => new EzPreviewManiaSettingsDocument
        {
            NoteColour = maniaSettings.NoteColour.Value.ToHex(),
            HoldHeadColour = maniaSettings.HoldHeadColour.Value.ToHex(),
            HoldBodyColour = maniaSettings.HoldBodyColour.Value.ToHex(),
            // HoldTailColour = maniaSettings.HoldTailColour.Value.ToHex(),
            LaneBackgroundColour = maniaSettings.LaneBackgroundColour.Value.ToHex(),
            ThicknessScale = maniaSettings.ThicknessScale.Value,
            Style = maniaSettings.Style.Value.ToString(),
        };

        public void ApplyTo(EzPreviewManiaSettings maniaSettings)
        {
            maniaSettings.NoteColour.Value = parse(NoteColour, EzPreviewManiaSettings.DEFAULT_NOTE_COLOUR);
            maniaSettings.HoldHeadColour.Value = parse(HoldHeadColour, EzPreviewManiaSettings.DEFAULT_HOLD_HEAD_COLOUR);
            maniaSettings.HoldBodyColour.Value = parse(HoldBodyColour, EzPreviewManiaSettings.DEFAULT_HOLD_BODY_COLOUR);
            // maniaSettings.HoldTailColour.Value = parse(HoldTailColour, EzPreviewManiaSettings.DEFAULT_HOLD_TAIL_COLOUR);
            maniaSettings.LaneBackgroundColour.Value = parse(LaneBackgroundColour, EzPreviewManiaSettings.DEFAULT_LANE_BACKGROUND_COLOUR);
            maniaSettings.ThicknessScale.Value = parseThickness(ThicknessScale);
            maniaSettings.Style.Value = parseStyle(Style);
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
