// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Known community dan ladders. 5K / 8K / 9K fall back to <see cref="Reform4K"/> text labels until dedicated tables exist.
    /// Reserved: add <see cref="IEzDanLadder"/> implementations and wire them in <see cref="EzDanLadders.For"/> —
    /// do not treat the Reform fallback as the permanent multi-key solution.
    /// </summary>
    public enum EzDanLadderKind
    {
        Reform4K,
        Ln4K,
        SixK,
        SevenK,
    }

    /// <summary>
    /// Per-keymode / side label table: rawDan → display label (+ optional texture key).
    /// </summary>
    public interface IEzDanLadder
    {
        EzDanLadderKind Kind { get; }

        /// <summary>Asset folder under EzResources/Dans (reform, ln, 6k, 7k).</summary>
        string TextureFolder { get; }

        /// <summary>When true, LN badges use <c>ln-{bare}</c> file names inside <see cref="TextureFolder"/>.</summary>
        bool TextureUsesLnPrefix { get; }

        double Floor { get; }

        double? Ceiling { get; }

        /// <summary>Bare level ids in ascending order (e.g. 1…10, alpha…kappa).</summary>
        ReadOnlySpan<string> BareLabels { get; }

        string ParseLabel(double rawDan);

        /// <summary>Texture file stem for a bare label (no extension, no folder).</summary>
        bool TryResolveTextureStem(string bareLabel, out string stem);
    }

    public static class EzDanLadders
    {
        private static readonly IEzDanLadder reform_4k = new EzDanLadderReform4K();
        private static readonly IEzDanLadder ln_4k = new EzDanLadderLn4K();
        private static readonly IEzDanLadder six_k = new EzDanLadderSixK();
        private static readonly IEzDanLadder seven_k = new EzDanLadderSevenK();

        public static IEzDanLadder For(int keyCount, EzDanSide side)
        {
            if (keyCount == 4 && side == EzDanSide.Ln)
                return ln_4k;

            return keyCount switch
            {
                6 => six_k,
                7 => seven_k,
                // 4K RC and other keymodes without a dedicated community ladder yet.
                _ => reform_4k,
            };
        }

        public static IEzDanLadder For(int keyCount, string sideId)
            => For(keyCount, EzDanSideExtensions.ParseOrRc(sideId));

        /// <summary>Shared --/-/+/++ banding used by all numeric/named ladders.</summary>
        public static string AppendTierSuffix(string bareLabel, double rawDan, int levelIndex1Based)
        {
            double offset = rawDan - levelIndex1Based;
            string? variant = offset <= -0.45 ? "--"
                : offset <= -0.25 ? "-"
                : offset < 0.1 ? null
                : offset < 0.26 ? "+"
                : "++";
            return $"{bareLabel}{variant ?? string.Empty}";
        }

        public static string BareLabel(string displayLabel)
        {
            if (string.IsNullOrEmpty(displayLabel))
                return string.Empty;

            return displayLabel.TrimEnd('+', '-').Trim().ToLowerInvariant();
        }

        public static string TierSuffix(string displayLabel)
        {
            if (string.IsNullOrEmpty(displayLabel))
                return string.Empty;

            var match = Regex.Match(displayLabel.Trim(), @"[+-]+$");
            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>mania-hub danTierColor.</summary>
        public static Colour4? TierColour(string suffix) => suffix switch
        {
            "--" => Colour4.FromHex("#4db8ff"),
            "-" => Colour4.FromHex("#7ac8ea"),
            "+" => Colour4.FromHex("#ffab74"),
            "++" => Colour4.FromHex("#ef6f7f"),
            _ => null,
        };

        /// <summary>
        /// EzResources path without extension, e.g. <c>Dans/6k/7</c> or <c>Dans/6k/ln-7</c>.
        /// </summary>
        public static string? TryGetTexturePath(int keyCount, string sideId, string displayOrBareLabel)
            => TryGetTexturePath(keyCount, EzDanSideExtensions.ParseOrRc(sideId), displayOrBareLabel);

        public static string? TryGetTexturePath(int keyCount, EzDanSide side, string displayOrBareLabel)
        {
            var ladder = For(keyCount, side);
            string bare = BareLabel(displayOrBareLabel);

            if (!ladder.TryResolveTextureStem(bare, out string stem))
                return null;

            string fileStem = side == EzDanSide.Ln && ladder.TextureUsesLnPrefix
                ? $"ln-{stem}"
                : stem;

            return $"Dans/{ladder.TextureFolder}/{fileStem}";
        }

        /// <summary>
        /// DLL-embedded path for <see cref="TryGetTexturePath"/> when a folder starts with a digit
        /// (MSBuild → <c>_6k</c> / <c>_7k</c>). Null if unchanged.
        /// </summary>
        public static string? TryGetEmbeddedTexturePath(string relativePathWithoutExtension)
        {
            if (string.IsNullOrEmpty(relativePathWithoutExtension))
                return null;

            string[] parts = relativePathWithoutExtension.Split('/');
            bool changed = false;

            // Only directory segments — file stems like "7" / "ln-7" stay as-is.
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Length > 0 && char.IsDigit(parts[i][0]))
                {
                    parts[i] = "_" + parts[i];
                    changed = true;
                }
            }

            return changed ? string.Join('/', parts) : null;
        }
    }
}
