// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills
{
    public abstract record EzMinaMaintenanceSection
    {
        public sealed record Skipped : EzMinaMaintenanceSection;

        public sealed record Unrateable : EzMinaMaintenanceSection;

        public sealed record Vector(EzMinaSkillAxisData Data, double? StarRating) : EzMinaMaintenanceSection;

        public sealed record TransientFailure : EzMinaMaintenanceSection;
    }

    public abstract record EzCsiMaintenanceSection
    {
        public sealed record Skipped : EzCsiMaintenanceSection;

        public sealed record Unavailable : EzCsiMaintenanceSection;

        public sealed record Computed(EzChartSkillInfo Info, IReadOnlyDictionary<string, double> Axes) : EzCsiMaintenanceSection;

        public sealed record TransientFailure : EzCsiMaintenanceSection;
    }

    public abstract record EzDanMaintenanceSection
    {
        public sealed record Skipped : EzDanMaintenanceSection;

        public sealed record Upsert(EzPersistedChartDan ChartDan) : EzDanMaintenanceSection;

        public sealed record TransientFailure : EzDanMaintenanceSection;
    }

    public abstract record EzPppMaintenanceSection
    {
        public sealed record Skipped : EzPppMaintenanceSection;

        public sealed record Skills(
            IReadOnlyDictionary<string, double> Values,
            int RulesetOnlineId,
            double? Star,
            double? PerformancePoints) : EzPppMaintenanceSection;

        public sealed record TransientFailure : EzPppMaintenanceSection;
    }

    /// <summary>Which chart-chain stages were enabled for one maintenance pass.</summary>
    [System.Flags]
    public enum EzChartChainApplyFlags
    {
        None = 0,
        Msd = 1 << 0,
        Csi = 1 << 1,
        Dan = 1 << 2,
        Ppp = 1 << 3,
    }
}