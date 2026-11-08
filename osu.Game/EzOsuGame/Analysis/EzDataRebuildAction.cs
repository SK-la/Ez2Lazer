// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Database;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One dispatchable maintenance action. Identity only: which storage it lives in and what it reads is
    /// declared by <see cref="EzDataRebuildCatalog"/>.
    /// </summary>
    public enum EzDataRebuildAction
    {
        RealmChartMsd = 0,
        RealmChartCsi = 1,
        RealmChartDan = 2,
        RealmPpp = 3,
        RealmSunny = 4,
        RealmPp = 5,
        RealmTags = 6,
        RealmScores = 7,
        RealmAll = 8,
        SqliteMain = 9,
        SqliteSongsBranches = 10,
    }

    /// <summary>Which store an action writes to. The dialog renders one chain per side.</summary>
    public enum EzDataRebuildSide
    {
        Realm,
        Sqlite,
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
        EzDataRebuildSide Side,
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
    /// </remarks>
    public static class EzDataRebuildCatalog
    {
        /// <summary>Vertical gap between a node and the input it consumes.</summary>
        public const float CHAIN_GAP = 4;

        /// <summary>Vertical gap between two nodes that can be run on their own.</summary>
        public const float INDEPENDENT_GAP = 26;

        public static IReadOnlyList<EzDataRebuildNode> Realm { get; } = new[]
        {
            node(EzDataRebuildAction.RealmChartMsd, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_MSD_NAME, EzEnumStrings.DATA_REBUILD_ACTION_MSD_DETAIL),
            node(EzDataRebuildAction.RealmChartCsi, EzDataRebuildSide.Realm, true,
                EzEnumStrings.DATA_REBUILD_ACTION_CSI_NAME, EzEnumStrings.DATA_REBUILD_ACTION_CSI_DETAIL),
            node(EzDataRebuildAction.RealmChartDan, EzDataRebuildSide.Realm, true,
                EzEnumStrings.DATA_REBUILD_ACTION_DAN_NAME, EzEnumStrings.DATA_REBUILD_ACTION_DAN_DETAIL),

            node(EzDataRebuildAction.RealmPpp, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_PPP_NAME, EzEnumStrings.DATA_REBUILD_ACTION_PPP_DETAIL),
            node(EzDataRebuildAction.RealmSunny, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SUNNY_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SUNNY_DETAIL),
            node(EzDataRebuildAction.RealmPp, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_PP_NAME, EzEnumStrings.DATA_REBUILD_ACTION_PP_DETAIL),
            node(EzDataRebuildAction.RealmTags, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_TAGS_NAME, EzEnumStrings.DATA_REBUILD_ACTION_TAGS_DETAIL),
            node(EzDataRebuildAction.RealmScores, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SCORES_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SCORES_DETAIL),
            node(EzDataRebuildAction.RealmAll, EzDataRebuildSide.Realm, false,
                EzEnumStrings.DATA_REBUILD_ACTION_ALL_NAME, EzEnumStrings.DATA_REBUILD_ACTION_ALL_DETAIL),
        };

        public static IReadOnlyList<EzDataRebuildNode> Sqlite { get; } = new[]
        {
            node(EzDataRebuildAction.SqliteMain, EzDataRebuildSide.Sqlite, false,
                EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_MAIN_NAME, EzEnumStrings.DATA_REBUILD_ACTION_SQLITE_MAIN_DETAIL),
            node(EzDataRebuildAction.SqliteSongsBranches, EzDataRebuildSide.Sqlite, true,
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
            var node = Find(action);

            if (node == null)
                return Array.Empty<EzDataRebuildNode>();

            IReadOnlyList<EzDataRebuildNode> chain = node.Side == EzDataRebuildSide.Realm ? Realm : Sqlite;
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

        private static EzDataRebuildNode node(EzDataRebuildAction action, EzDataRebuildSide side, bool continuesChain,
                                              LocalisableString name, LocalisableString detail)
            => new EzDataRebuildNode(action, side, continuesChain, name, detail);

        private static IReadOnlyList<EzDataRebuildNode> concat(IReadOnlyList<EzDataRebuildNode> first, IReadOnlyList<EzDataRebuildNode> second)
        {
            var result = new List<EzDataRebuildNode>(first.Count + second.Count);
            result.AddRange(first);
            result.AddRange(second);
            return result;
        }
    }

    /// <summary>Storage scope an action clears / backfills, for the Realm side.</summary>
    public static class EzDataRebuildScopes
    {
        public static EzRealmMetadataScope? RealmScopeOf(EzDataRebuildAction action)
        {
            switch (action)
            {
                case EzDataRebuildAction.RealmChartMsd:
                    return EzRealmMetadataScope.MSD;

                case EzDataRebuildAction.RealmChartCsi:
                    return EzRealmMetadataScope.ChartSkillInfo;

                case EzDataRebuildAction.RealmChartDan:
                    return EzRealmMetadataScope.ChartDan;

                case EzDataRebuildAction.RealmPpp:
                    return EzRealmMetadataScope.PPP;

                case EzDataRebuildAction.RealmSunny:
                    return EzRealmMetadataScope.SSR;

                case EzDataRebuildAction.RealmPp:
                    return EzRealmMetadataScope.PP;

                case EzDataRebuildAction.RealmTags:
                    return EzRealmMetadataScope.Tags;

                case EzDataRebuildAction.RealmAll:
                    return EzRealmMetadataScope.All;

                default:
                    return null;
            }
        }
    }
}
