// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    public enum EzSkinIniFieldKind
    {
        Text,
        Bool,
        Colour,
    }

    public readonly struct EzSkinIniFieldDefinition
    {
        public string Key { get; init; }
        public string Label { get; init; }
        public EzSkinIniFieldKind Kind { get; init; }
        public string? Category { get; init; }
    }

    /// <summary>
    /// Known editable <c>skin.ini</c> keys aligned with legacy decoders.
    /// Fields exposed in the skin editor sidebar participate in <see cref="EzSkinEditorComparisonSnapshot"/> default baselines.
    /// </summary>
    public static class EzSkinIniFieldCatalog
    {
        private const int max_combo_colours = 8;

        public static IReadOnlyList<EzSkinIniFieldDefinition> GeneralFields { get; } = new[]
        {
            new EzSkinIniFieldDefinition { Key = "Name", Label = "Name", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = "Author", Label = "Author", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = "Version", Label = "Version", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = nameof(SkinConfiguration.LegacySetting.AnimationFramerate), Label = "AnimationFramerate", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = nameof(SkinConfiguration.LegacySetting.LayeredHitSounds), Label = "LayeredHitSounds", Kind = EzSkinIniFieldKind.Bool },
            new EzSkinIniFieldDefinition { Key = nameof(SkinConfiguration.LegacySetting.AllowSliderBallTint), Label = "AllowSliderBallTint", Kind = EzSkinIniFieldKind.Bool },
            new EzSkinIniFieldDefinition { Key = "SliderBallFlip", Label = "SliderBallFlip", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = "SliderBallFrames", Label = "SliderBallFrames", Kind = EzSkinIniFieldKind.Text },
            new EzSkinIniFieldDefinition { Key = "CursorTrailRotate", Label = "CursorTrailRotate", Kind = EzSkinIniFieldKind.Text },
        };

        public static IReadOnlyList<string> ColourKeys { get; } = buildColourKeys();

        public static IReadOnlyList<EzSkinIniFieldDefinition> ManiaLayoutFields { get; } = new[]
        {
            field("ColumnWidth", "ColumnWidth", "布局"),
            field("ColumnSpacing", "ColumnSpacing", "布局"),
            field("ColumnLineWidth", "ColumnLineWidth", "布局"),
        };

        public static IReadOnlyList<EzSkinIniFieldDefinition> ManiaPositionFields { get; } = new[]
        {
            field("HitPosition", "HitPosition", "位置"),
            field("LightPosition", "LightPosition", "位置"),
            field("ComboPosition", "ComboPosition", "位置"),
            field("ScorePosition", "ScorePosition", "位置"),
            field("WidthForNoteHeightScale", "WidthForNoteHeightScale", "位置"),
            field("StagePaddingTop", "StagePaddingTop", "位置"),
            field("StagePaddingBottom", "StagePaddingBottom", "位置"),
        };

        public static IReadOnlyList<EzSkinIniFieldDefinition> ManiaDisplayFields { get; } = new[]
        {
            field("BarlineHeight", "BarlineHeight", "显示"),
            field("JudgementLine", "JudgementLine", "显示", EzSkinIniFieldKind.Bool),
            field("KeysUnderNotes", "KeysUnderNotes", "显示", EzSkinIniFieldKind.Bool),
            field("NoteBodyStyle", "NoteBodyStyle", "显示"),
            field("LightFramePerSecond", "LightFramePerSecond", "显示"),
        };

        public static IReadOnlyList<EzSkinIniFieldDefinition> ManiaExplosionFields { get; } = new[]
        {
            field("LightingNWidth", "LightingNWidth", "爆炸"),
            field("LightingLWidth", "LightingLWidth", "爆炸"),
        };

        public static IEnumerable<string> GetManiaPerKeyColourKeys(int keys)
        {
            for (int i = 1; i <= keys; i++)
                yield return $"Colour{i}";

            yield return "ColourBarline";
            yield return "ColourColumnBg";
            yield return "ColourLine";
        }

        private static IReadOnlyList<string> buildColourKeys()
        {
            var keys = new List<string>();

            for (int i = 1; i <= max_combo_colours; i++)
                keys.Add($"Combo{i}");

            keys.Add(nameof(GlobalSkinColours.MenuGlow));
            keys.Add("SliderBorder");
            keys.Add("SliderTrackOverride");
            keys.Add("SliderBall");
            keys.Add("SpinnerBackground");
            keys.Add("StarBreakAdditive");

            return keys;
        }

        private static EzSkinIniFieldDefinition field(string key, string label, string category, EzSkinIniFieldKind kind = EzSkinIniFieldKind.Text) =>
            new EzSkinIniFieldDefinition
            {
                Key = key,
                Label = label,
                Kind = kind,
                Category = category,
            };
    }
}
