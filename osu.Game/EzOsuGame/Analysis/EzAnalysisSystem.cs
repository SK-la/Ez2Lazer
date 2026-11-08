// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One analysis system, or a set of them.
    /// </summary>
    /// <remarks>
    /// The same value serves both roles: a stored row names exactly one system, while a rebuild request and a
    /// selectivity filter name a set. That is why this is a flags enum rather than two types - the identity of a
    /// system and the scope that covers it are the same vocabulary.
    /// <para>
    /// A system may have any subset of a chart vector, a player vector and a scalar rating; which subset it has
    /// follows from what it computes, not from a declaration. Mina owns both of its vectors; SSR owns one chart
    /// star and nothing else.
    /// </para>
    /// </remarks>
    [Flags]
    public enum EzAnalysisSystem
    {
        /// <summary>No system. Never stored.</summary>
        [EzSkillMeta("none", "无", "None")]
        None = 0,

        /// <summary>Official star rating and pp. Mirrored onto the beatmap's own columns.</summary>
        [EzSkillMeta("official", "官方", "Official")]
        Official = 1 << 0,

        /// <summary>Sunny chart star. The algorithm lives in a ruleset assembly; the rating is one number per chart.</summary>
        [EzSkillMeta("ssr", "Sunny", "Sunny")]
        SSR = 1 << 1,

        /// <summary>PP+ chart and player vectors, plus their star rating and pp.</summary>
        [EzSkillMeta("ppp", "PP+", "PP+")]
        PPP = 1 << 2,

        /// <summary>Mina vectors: the chart difficulty vector and the player rating vector.</summary>
        [EzSkillMeta("mina", "Mina", "Mina")]
        Mina = 1 << 3,

        /// <summary>Chart structure vector. No rating of its own.</summary>
        [EzSkillMeta("csi", "特点", "CSI")]
        CSI = 1 << 4,

        /// <summary>Dan classification. No vector.</summary>
        [EzSkillMeta("dan", "段位", "Dan")]
        Dan = 1 << 5,

        /// <summary>Player pattern ratings derived from plays.</summary>
        [EzSkillMeta("pattern", "键型", "Pattern")]
        Pattern = 1 << 6,

        /// <summary>Lightweight chart tags. A data group, not a system.</summary>
        [EzSkillMeta("metadata_tags", "标签", "Tags")]
        MetadataTags = 1 << 7,

        /// <summary>The player score chain. A data group spanning every system.</summary>
        [EzSkillMeta("scores", "成绩", "Scores")]
        Scores = 1 << 8,

        /// <summary>Every system and data group.</summary>
        [EzSkillMeta("all", "全部", "All")]
        All = Official | SSR | PPP | Mina | CSI | Dan | Pattern | MetadataTags | Scores,
    }

    /// <summary>
    /// The stored spelling of an <see cref="EzAnalysisSystem"/>, plus the set membership helpers.
    /// </summary>
    /// <remarks>
    /// The spelling comes from <see cref="EzSkillMetaAttribute.Id"/>, never from the member name: renaming a member
    /// cannot move stored rows, and the id can be read by a human.
    /// </remarks>
    public static class EzAnalysisSystemStorage
    {
        /// <summary>The declared id of a single system, or empty for <see cref="EzAnalysisSystem.None"/> and sets.</summary>
        public static string ToStorageId(this EzAnalysisSystem system)
            => IsSingle(system) ? EzEnumMetaCache<EzAnalysisSystem>.Meta(system).Id : string.Empty;

        /// <summary>Reads a stored id back, or <see cref="EzAnalysisSystem.None"/> when it names no single system.</summary>
        public static EzAnalysisSystem FromStorageId(string? id)
            => EzEnumMetaCache<EzAnalysisSystem>.TryParse(id, out var system) && IsSingle(system)
                ? system
                : EzAnalysisSystem.None;

        /// <summary>Whether the value names exactly one system rather than a set.</summary>
        public static bool IsSingle(EzAnalysisSystem system)
        {
            if (system <= EzAnalysisSystem.None)
                return false;

            int bits = (int)system;
            return (bits & (bits - 1)) == 0;
        }

        /// <summary>Single systems contained in <paramref name="set"/>, in declaration order.</summary>
        public static IEnumerable<EzAnalysisSystem> Members(this EzAnalysisSystem set)
        {
            var all = Enum.GetValues<EzAnalysisSystem>();

            foreach (var system in all)
            {
                if (system != EzAnalysisSystem.None && system != EzAnalysisSystem.All && set.HasFlag(system))
                    yield return system;
            }
        }
    }

    /// <summary>
    /// Resolves the revision a system stamps on its rows.
    /// </summary>
    /// <remarks>
    /// The number is defined by the code that computes it - an engine, a math class - and this only knows which
    /// one owns which system. A bump is therefore made where the algorithm is changed, not here.
    /// <para>
    /// There is deliberately no per-ruleset and no per-submodule revision. A change that only affects one ruleset
    /// or one internal stage is still that system changing, so one system has one number.
    /// </para>
    /// <para>
    /// Official is not answerable here: its algorithm is upstream difficulty, not an Ez system, and a process that
    /// cannot see the algorithm must not guess its revision.
    /// </para>
    /// </remarks>
    public static class EzAnalysisRevision
    {
        /// <summary>Revision of <paramref name="system"/>, or 0 when it has no algorithm of its own.</summary>
        public static int OfSystem(EzAnalysisSystem system) => system switch
        {
            EzAnalysisSystem.Mina => EzMinaEngine.VERSION,
            EzAnalysisSystem.CSI => EzCSIEngine.VERSION,
            EzAnalysisSystem.Dan => EzDanEngine.VERSION,
            EzAnalysisSystem.PPP => EzPPPEngine.VERSION,
            EzAnalysisSystem.Pattern => EzPatternRatings.VERSION,
            EzAnalysisSystem.SSR => EzSSRUnit.VERSION,
            _ => 0,
        };
    }

    /// <summary>
    /// Row encodings shared by every system.
    /// </summary>
    public static class EzAxisIds
    {
        /// <summary>Key count sentinel for rows that are not sliced by key mode.</summary>
        public const int UNSLICED_KEY = 0;

        /// <summary>
        /// Row tag for a chart the engine rates as a zero vector on a supported key mode. The row carries
        /// <see cref="Skills.EzSkillRowState.Unrateable"/>; the tag itself is not an axis.
        /// </summary>
        public const string UNRATEABLE = "__unrateable";

        /// <summary>Rulesets the analysis layer knows by online id.</summary>
        public static readonly int[] ALL_RULESETS =
        {
            EzLocalProfileConstants.OSU_RULESET_ID,
            EzLocalProfileConstants.TAIKO_RULESET_ID,
            EzLocalProfileConstants.CATCH_RULESET_ID,
            EzLocalProfileConstants.MANIA_RULESET_ID,
        };

        public static string DanSkillId(int keyCount, EzDanSide side) => $"{keyCount}k.{side.ToId()}";
    }
}
