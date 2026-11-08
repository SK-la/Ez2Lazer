// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// The closed vocabulary of pattern tags produced by the structure pass.
    /// </summary>
    /// <remarks>
    /// Tag ids are storage keys and must stay stable. <see cref="EzManiaRCAxisExtensions.PLAYER_AXES"/> is the
    /// subset that carries a player rating, in presentation order.
    /// </remarks>
    public enum EzManiaRCAxis
    {
        [EzSkillMeta("chordstream", "大切", "Chordstream", "#5ab2f2")]
        Chordstream,

        [EzSkillMeta("bracket", "衩", "Bracket", "#f3c24a")]
        Bracket,

        [EzSkillMeta("delay", "延迟", "Delay", "#46c7b8")]
        Delay,

        [EzSkillMeta("stream", "切", "Stream", "#8f6bd8")]
        Stream,

        [EzSkillMeta("jack", "叠", "Jack", "#ec6a9c")]
        Jack,

        [EzSkillMeta("tech", "技", "Tech", "#83cf6b")]
        Tech,

        [EzSkillMeta("ln", "LN", "LN", "#f07474")]
        Ln,

        [EzSkillMeta("chordjack", "大叠", "Chordjack", "#c59a5c")]
        Chordjack,

        [EzSkillMeta("speedjack", "速叠", "Speedjack", "#d78fb0")]
        Speedjack,

        [EzSkillMeta("handjack", "手叠", "Handjack", "#a88fd7")]
        Handjack,

        [EzSkillMeta("jumpstream", "滑切", "Jumpstream", "#6f87d8")]
        Jumpstream,

        [EzSkillMeta("handstream", "多切", "Handstream", "#b06bc0")]
        Handstream,

        [EzSkillMeta("dumpstream", "乱切", "Dumpstream", "#7f9aa8")]
        Dumpstream,

        [EzSkillMeta("quadstream", "四押", "Quadstream", "#8fa8b8")]
        Quadstream,
    }

    public static class EzManiaRCAxisExtensions
    {
        public static EzManiaRCAxis[] All => EzEnumMetaCache<EzManiaRCAxis>.ALL;

        /// <summary>Tags that carry a player rating. Order defines the rating presentation order.</summary>
        public static readonly EzManiaRCAxis[] PLAYER_AXES =
        {
            EzManiaRCAxis.Chordstream,
            EzManiaRCAxis.Bracket,
            EzManiaRCAxis.Delay,
            EzManiaRCAxis.Stream,
            EzManiaRCAxis.Jack,
            EzManiaRCAxis.Tech,
            EzManiaRCAxis.Ln,
        };

        /// <summary>Player axes drawn on the pattern radar polygon; LN has its own layer.</summary>
        public static EzManiaRCAxis[] PlayerRadarAxes { get; } = PLAYER_AXES.Where(static a => a != EzManiaRCAxis.Ln).ToArray();

        /// <summary>Every declared pattern id, for set membership tests.</summary>
        public static IReadOnlySet<string> AllIds { get; } = new HashSet<string>(All.Select(static a => a.ToId()), StringComparer.Ordinal);

        public static EzSkillMetaAttribute Meta(this EzManiaRCAxis tag) => EzEnumMetaCache<EzManiaRCAxis>.Meta(tag);

        public static string ToId(this EzManiaRCAxis tag) => tag.Meta().Id;

        /// <summary>Player rating row id: the pattern system prefix plus the tag id.</summary>
        public static string ToSkillId(this EzManiaRCAxis tag) => $"{EzSkillSystems.PLAYER_PATTERN}.{tag.ToId()}";

        public static EzSkillChip Chip(this EzManiaRCAxis tag) => tag.Meta().Chip;

        public static bool TryParse(string? patternId, out EzManiaRCAxis tag) => EzEnumMetaCache<EzManiaRCAxis>.TryParseIgnoreCase(bareId(patternId), out tag);

        private static string? bareId(string? patternId)
        {
            if (string.IsNullOrEmpty(patternId))
                return patternId;

            int dot = patternId.LastIndexOf('.');
            return dot >= 0 && dot < patternId.Length - 1 ? patternId[(dot + 1)..] : patternId;
        }
    }
}
