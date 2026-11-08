// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One dispatchable maintenance action, or a set of them.
    /// </summary>
    /// <remarks>
    /// Every Realm member is bit-identical to the <see cref="EzAnalysisSystem"/> it rebuilds, so the action's scope
    /// is a mask rather than a table kept beside the systems. The two SQLite members sit above the system bits
    /// because they address a database, not an analysis system.
    /// <para>
    /// <see cref="RealmAll"/> covers every system, including ones that have no card of their own.
    /// </para>
    /// </remarks>
    [Flags]
    public enum EzDataRebuildAction
    {
        None = 0,

        RealmOfficial = (int)EzAnalysisSystem.Official,

        RealmSSR = (int)EzAnalysisSystem.SSR,

        RealmPPP = (int)EzAnalysisSystem.PPP,

        RealmMina = (int)EzAnalysisSystem.Mina,

        RealmCSI = (int)EzAnalysisSystem.CSI,

        RealmDan = (int)EzAnalysisSystem.Dan,

        RealmMetadataTags = (int)EzAnalysisSystem.MetadataTags,

        /// <summary>
        /// Full score recalculation. It carries the Scores bit but is dispatched to its own queue, so it is
        /// deliberately outside the Realm metadata set.
        /// </summary>
        RealmScores = (int)EzAnalysisSystem.Scores,

        RealmAll = (int)EzAnalysisSystem.All,

        SqliteMain = 1 << 16,

        SqliteSongsBranches = 1 << 17,
    }

    /// <summary>
    /// The systems an action rebuilds, or null when it addresses a store instead.
    /// </summary>
    public static class EzDataRebuildScopes
    {
        /// <summary>Actions that go to the Realm metadata queue, one bit per system.</summary>
        private const EzDataRebuildAction realm_actions = EzDataRebuildAction.RealmOfficial
                                                          | EzDataRebuildAction.RealmSSR
                                                          | EzDataRebuildAction.RealmPPP
                                                          | EzDataRebuildAction.RealmMina
                                                          | EzDataRebuildAction.RealmCSI
                                                          | EzDataRebuildAction.RealmDan
                                                          | EzDataRebuildAction.RealmMetadataTags;

        public static EzAnalysisSystem? RealmScopeOf(EzDataRebuildAction action)
        {
            if (action == EzDataRebuildAction.RealmAll)
                return EzAnalysisSystem.All;

            if (action == EzDataRebuildAction.None || (action & ~realm_actions) != 0)
                return null;

            return (EzAnalysisSystem)action;
        }
    }

    /// <summary>
    /// One node of a maintenance chain.
    /// </summary>
    /// <param name="ContinuesChain">
    /// Whether this node reads the node directly above it. That is the only thing the dialog's spacing encodes:
    /// a dependent step sits tight under its input, an independent one gets a visible break.
    /// </param>
    public sealed record EzDataRebuildNode(
        EzDataRebuildAction Action,
        bool ContinuesChain,
        LocalisableString Name,
        LocalisableString Detail);

    /// <summary>
    /// The maintenance surface's content: two chains, one per store, each listing its actions in dependency order.
    /// </summary>
    /// <remarks>
    /// The graph is declared once here so the dialog (layout), the dispatcher (scope) and the tests read the same
    /// edges. Adding an action means adding an enum member here and a dispatch arm; nothing in the UI enumerates
    /// actions by hand.
    /// <para>
    /// Which store a node belongs to is its chain, not a field: a node appears in exactly one of <see cref="Realm"/>
    /// and <see cref="Sqlite"/>, so carrying a side on the node would only repeat where it was declared.
    /// </para>
    /// </remarks>
    public static class EzDataRebuildCatalog
    {
        /// <summary>Vertical gap between a node and the input it consumes.</summary>
        public const float CHAIN_GAP = 4;

        /// <summary>Vertical gap between two nodes that can be run on their own.</summary>
        public const float INDEPENDENT_GAP = 26;

        public static IReadOnlyList<EzDataRebuildNode> Realm { get; } = new[]
        {
            node(EzDataRebuildAction.RealmMina, false,
                EzEnumStrings.DATA_REBUILD_ACTION_MINA_NAME, EzEnumStrings.DATA_REBUILD_ACTION_MINA_DETAIL),
            node(EzDataRebuildAction.RealmCSI, true,
                EzEnumStrings.DATA_REBUILD_ACTION_CSI_NAME, EzEnumStrings.DATA_REBUILD_ACTION_CSI_DETAIL),
            node(EzDataRebuildAction.RealmDan, true,
                EzEnumStrings.DATA_REBUILD_ACTION_DAN_NAME, EzEnumStrings.DATA_REBUILD_ACTION_DAN_DETAIL),

            node(EzDataRebuildAction.RealmPPP, false,
                EzEnumStrings.DATA_REBUILD_ACTION_PPP_NAME, EzEnumStrings.DATA_REBUILD_ACTION_PPP_DETAIL),
            node(EzDataRebuildAction.RealmSSR, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SSR_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SSR_DETAIL),
            node(EzDataRebuildAction.RealmOfficial, false,
                EzEnumStrings.DATA_REBUILD_ACTION_OFFICIAL_NAME, EzEnumStrings.DATA_REBUILD_ACTION_OFFICIAL_DETAIL),
            node(EzDataRebuildAction.RealmMetadataTags, false,
                EzEnumStrings.DATA_REBUILD_ACTION_TAGS_NAME, EzEnumStrings.DATA_REBUILD_ACTION_TAGS_DETAIL),
            node(EzDataRebuildAction.RealmScores, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SCORES_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SCORES_DETAIL),
            node(EzDataRebuildAction.RealmAll, false,
                EzEnumStrings.DATA_REBUILD_ACTION_ALL_NAME, EzEnumStrings.DATA_REBUILD_ACTION_ALL_DETAIL),
        };

        public static IReadOnlyList<EzDataRebuildNode> Sqlite { get; } = new[]
        {
            node(EzDataRebuildAction.SqliteMain, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_MAIN_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_MAIN_DETAIL),
            node(EzDataRebuildAction.SqliteSongsBranches, true,
                EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_BRANCHES_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_BRANCHES_DETAIL),
        };

        public static IReadOnlyList<EzDataRebuildNode> All { get; } = concat(Realm, Sqlite);

        /// <summary>Space the dialog leaves above <paramref name="node"/>.</summary>
        public static float GapBefore(EzDataRebuildNode node) => node.ContinuesChain ? CHAIN_GAP : INDEPENDENT_GAP;

        public static EzDataRebuildNode? Find(EzDataRebuildAction action)
        {
            foreach (var node in All)
            {
                if (node.Action == action)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// Nodes this action reads from, nearest first. Walked transitively along the declared chain so a hint can
        /// mention every input that may be stale, not just the immediate one.
        /// </summary>
        public static IReadOnlyList<EzDataRebuildNode> DependenciesOf(EzDataRebuildAction action)
        {
            if (Find(action) == null)
                return Array.Empty<EzDataRebuildNode>();

            IReadOnlyList<EzDataRebuildNode> chain = chainOf(action);
            int index = -1;

            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i].Action == action)
                {
                    index = i;
                    break;
                }
            }

            if (index <= 0)
                return Array.Empty<EzDataRebuildNode>();

            var dependencies = new List<EzDataRebuildNode>();

            for (int i = index; i > 0 && chain[i].ContinuesChain; i--)
                dependencies.Add(chain[i - 1]);

            return dependencies;
        }

        /// <summary>Current-culture names, joined for footer and confirmation text.</summary>
        public static string DisplayNames(IEnumerable<EzDataRebuildAction> actions)
            => string.Join(" / ", actions.Select(action => (Find(action)?.Name ?? action.ToString()).ToString()));

        /// <summary>The chain an action was declared in: that is where its side comes from.</summary>
        private static IReadOnlyList<EzDataRebuildNode> chainOf(EzDataRebuildAction action)
        {
            foreach (var node in Realm)
            {
                if (node.Action == action)
                    return Realm;
            }

            return Sqlite;
        }

        private static EzDataRebuildNode node(EzDataRebuildAction action, bool continuesChain,
                                              LocalisableString name, LocalisableString detail)
            => new EzDataRebuildNode(action, continuesChain, name, detail);

        private static IReadOnlyList<EzDataRebuildNode> concat(IReadOnlyList<EzDataRebuildNode> first, IReadOnlyList<EzDataRebuildNode> second)
        {
            var result = new List<EzDataRebuildNode>(first.Count + second.Count);
            result.AddRange(first);
            result.AddRange(second);
            return result;
        }
    }
}
