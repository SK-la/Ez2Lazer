// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>4K Reform rice ladder: 1–10 then alpha…kappa (mania-hub reform/).</summary>
    public sealed class EzDanLadderReform4K : IEzDanLadder
    {
        private static readonly string[] labels =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
        };

        public EzDanLadderKind Kind => EzDanLadderKind.Reform4K;

        public string TextureFolder => "reform";

        public bool TextureUsesLnPrefix => false;

        public double Floor => 0.5;

        public double? Ceiling => null;

        public ReadOnlySpan<string> BareLabels => labels;

        public string ParseLabel(double rawDan)
        {
            int maxLevel = labels.Length;
            int level = Math.Min(maxLevel, Math.Max(1, (int)Math.Round(rawDan)));
            return EzDanLadders.AppendTierSuffix(labels[level - 1], rawDan, level);
        }

        public bool TryResolveTextureStem(string bareLabel, out string stem)
        {
            stem = bareLabel.ToLowerInvariant();

            foreach (string label in labels)
            {
                if (label == stem)
                    return true;
            }

            stem = string.Empty;
            return false;
        }
    }

    /// <summary>4K LN numeric ladder 1–17 (mania-hub ln/).</summary>
    public sealed class EzDanLadderLn4K : IEzDanLadder
    {
        public const int TOP = 17;

        private static readonly string[] labels = createLabels();

        private static string[] createLabels()
        {
            string[] list = new string[TOP];
            for (int i = 0; i < TOP; i++)
                list[i] = (i + 1).ToString();
            return list;
        }

        public EzDanLadderKind Kind => EzDanLadderKind.Ln4K;

        public string TextureFolder => "ln";

        public bool TextureUsesLnPrefix => false;

        public double Floor => 0.5;

        public double? Ceiling => TOP + 0.5;

        public ReadOnlySpan<string> BareLabels => labels;

        public string ParseLabel(double rawDan)
        {
            int level = Math.Max(1, Math.Min(TOP, (int)Math.Round(rawDan)));
            return EzDanLadders.AppendTierSuffix(level.ToString(), rawDan, level);
        }

        public bool TryResolveTextureStem(string bareLabel, out string stem)
        {
            stem = bareLabel;
            return int.TryParse(bareLabel, out int n) && n >= 1 && n <= TOP;
        }
    }

    /// <summary>6K RC/LN: 0–9 then terra…finish (mania-hub 6k/).</summary>
    public sealed class EzDanLadderSixK : IEzDanLadder
    {
        private static readonly string[] labels =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "terra", "celestial", "mystery", "nihility", "finish",
        };

        public EzDanLadderKind Kind => EzDanLadderKind.SixK;

        public string TextureFolder => "6k";

        public bool TextureUsesLnPrefix => true;

        public double Floor => 0;

        public double? Ceiling => null;

        public ReadOnlySpan<string> BareLabels => labels;

        public string ParseLabel(double rawDan)
        {
            // Levels are indexed 0..14 in the community table; rawDan rounds to that index.
            int maxIndex = labels.Length - 1;
            int index = Math.Min(maxIndex, Math.Max(0, (int)Math.Round(rawDan)));
            // Tier offset uses the same numeric level as the index for named bands (10=terra…).
            return EzDanLadders.AppendTierSuffix(labels[index], rawDan, index);
        }

        public bool TryResolveTextureStem(string bareLabel, out string stem)
        {
            stem = bareLabel.ToLowerInvariant();

            foreach (string label in labels)
            {
                if (label == stem)
                    return true;
            }

            stem = string.Empty;
            return false;
        }
    }

    /// <summary>7K RC/LN: 0–10 then gamma/azimuth/zenith/stellium (mania-hub 7k/).</summary>
    public sealed class EzDanLadderSevenK : IEzDanLadder
    {
        private static readonly string[] labels =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "gamma", "azimuth", "zenith", "stellium",
        };

        public EzDanLadderKind Kind => EzDanLadderKind.SevenK;

        public string TextureFolder => "7k";

        public bool TextureUsesLnPrefix => true;

        public double Floor => 0;

        public double? Ceiling => null;

        public ReadOnlySpan<string> BareLabels => labels;

        public string ParseLabel(double rawDan)
        {
            int maxIndex = labels.Length - 1;
            int index = Math.Min(maxIndex, Math.Max(0, (int)Math.Round(rawDan)));
            return EzDanLadders.AppendTierSuffix(labels[index], rawDan, index);
        }

        public bool TryResolveTextureStem(string bareLabel, out string stem)
        {
            stem = bareLabel.ToLowerInvariant();

            foreach (string label in labels)
            {
                if (label == stem)
                    return true;
            }

            stem = string.Empty;
            return false;
        }
    }
}
