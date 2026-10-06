// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.BeatmapPools
{
    /// <summary>
    /// A path association owned by one ruleset and gameplay variant.
    /// </summary>
    public sealed class EzBeatmapPoolPath
    {
        public string RulesetShortName { get; set; } = string.Empty;
        public int Variant { get; set; }
        public string Path { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int Order { get; set; }

        public EzBeatmapPoolPath Clone() => new EzBeatmapPoolPath
        {
            RulesetShortName = RulesetShortName,
            Variant = Variant,
            Path = Path,
            Enabled = Enabled,
            Order = Order,
        };
    }

    /// <summary>
    /// Versioned document persisted in the game configuration.
    /// </summary>
    public sealed class EzBeatmapPoolDocument
    {
        public int Version { get; set; } = 1;
        public List<EzBeatmapPoolPath> Paths { get; set; } = new List<EzBeatmapPoolPath>();
    }

    public readonly struct EzBeatmapPoolKey : IEquatable<EzBeatmapPoolKey>
    {
        public EzBeatmapPoolKey(string rulesetShortName, int variant)
        {
            RulesetShortName = rulesetShortName?.Trim() ?? string.Empty;
            Variant = variant;
        }

        public string RulesetShortName { get; }
        public int Variant { get; }

        public bool Equals(EzBeatmapPoolKey other)
            => Variant == other.Variant && string.Equals(RulesetShortName, other.RulesetShortName, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is EzBeatmapPoolKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RulesetShortName.ToLowerInvariant(), Variant);
    }
}
