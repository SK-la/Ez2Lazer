// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// HUD RC|LN skills-dan dual panel (Skill-radar style Chart / Player / Both sources).
    /// Borrowed by EzAnalysis wedge and Local Profile Track with bindable settings.
    /// </summary>
    public partial class EzHUDDanDualPanel : CompositeDrawable, ISerialisableDrawable
    {
        /// <summary>
        /// Song-select wedge content is often ~450–550px; keep Auto on Horizontal RC|LN there.
        /// Vertical dual stacks both sides and clips under BeatmapDetailsArea height.
        /// </summary>
        private float wideThreshold => 420f;

        public static readonly Colour4 RC_ACCENT = Colour4.FromHex("#e0b04c");
        public static readonly Colour4 LN_ACCENT = Colour4.FromHex("#f07474");

        public bool UsesFixedAnchor { get; set; }

        /// <summary>Chart MSD / Player SSR / Both — same idea as Skill radar layers.</summary>
        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DAN_PANEL_DATA_SOURCE), nameof(EzHUDStrings.DAN_PANEL_DATA_SOURCE_TOOLTIP))]
        public Bindable<EzDanPanelDataSource> DataSource { get; } = new Bindable<EzDanPanelDataSource>(EzDanPanelDataSource.Both);

        /// <summary>RC|LN dual arrangement (auto / horizontal / vertical).</summary>
        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DAN_PANEL_DUAL_LAYOUT), nameof(EzHUDStrings.DAN_PANEL_DUAL_LAYOUT_TOOLTIP))]
        public Bindable<EzDanPanelDualLayout> DualLayout { get; } = new Bindable<EzDanPanelDualLayout>(EzDanPanelDualLayout.Auto);

        /// <summary>
        /// When true, skillset chips show clear counts (Local Profile only). HUD keeps this false —
        /// clear lists are a separate profile component, not DualPanel.
        /// </summary>
        public BindableBool ShowClearCounts { get; } = new BindableBool(false);

        /// <summary>Target player for SSR / dan. Externally bindable (EzAnalysis header).</summary>
        public Bindable<string?> TargetUsername { get; } = new Bindable<string?>();

        public BindableInt KeyCount { get; } = new BindableInt();

        private FillFlowContainer dualFlow = null!;
        private EzDanLabeledStatList rcList = null!;
        private EzDanLabeledStatList lnList = null!;
        private OsuSpriteText emptyHint = null!;

        private readonly LayoutValue sizeLayout = new LayoutValue(Invalidation.DrawSize);

        [Resolved]
        private EzSkillProvider? skillProvider { get; set; }

        [Resolved(canBeNull: true)]
        private IBindable<WorkingBeatmap>? beatmap { get; set; }

        [Resolved(canBeNull: true)]
        private IBindable<IReadOnlyList<Mod>>? mods { get; set; }

        [Resolved]
        private EzAnalysisPlayerSelection? ezAnalysisPlayerSelection { get; set; }

        public EzHUDDanDualPanel()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            AddLayout(sizeLayout);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChildren = new Drawable[]
            {
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 13),
                    Colour = colours.Content2,
                    Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER,
                    Alpha = 0,
                },
                dualFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        rcList = new EzDanLabeledStatList(EzDanSide.Rc, RC_ACCENT)
                        {
                            RelativeSizeAxes = Axes.X,
                        },
                        lnList = new EzDanLabeledStatList(EzDanSide.Ln, LN_ACCENT)
                        {
                            RelativeSizeAxes = Axes.X,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tryBindSharedPlayerSelection();

            TargetUsername.BindValueChanged(_ => refresh(), true);
            KeyCount.BindValueChanged(_ => refresh());
            DataSource.BindValueChanged(_ => refresh());
            DualLayout.BindValueChanged(_ => applyLayoutMode(), true);
            ShowClearCounts.BindValueChanged(_ => refresh());

            beatmap?.BindValueChanged(_ => refresh());
            mods?.BindValueChanged(_ => refresh());
        }

        /// <summary>
        /// SongSelect skin / wedge: follow shared Ez analysis player when host did not bind a username.
        /// </summary>
        private void tryBindSharedPlayerSelection()
        {
            if (ezAnalysisPlayerSelection == null)
                return;

            if (!string.IsNullOrEmpty(TargetUsername.Value))
                return;

            TargetUsername.BindTo(ezAnalysisPlayerSelection.Current);
        }

        protected override void Update()
        {
            base.Update();

            if (!sizeLayout.IsValid)
            {
                sizeLayout.Validate();
                applyLayoutMode();
            }
        }

        private void applyLayoutMode()
        {
            // RC|LN groups may sit side-by-side or stack; skill rows stay a vertical 3-column grid.
            bool wide = DualLayout.Value switch
            {
                EzDanPanelDualLayout.Horizontal => true,
                EzDanPanelDualLayout.Vertical => false,
                _ => DrawWidth >= wideThreshold,
            };

            if (wide)
            {
                dualFlow.Direction = FillDirection.Horizontal;
                dualFlow.Spacing = Vector2.Zero;
                rcList.Width = 0.5f;
                lnList.Width = 0.5f;
                rcList.SetLayoutInset(new MarginPadding { Right = 6 });
                lnList.SetLayoutInset(new MarginPadding { Left = 6 });
            }
            else
            {
                dualFlow.Direction = FillDirection.Vertical;
                dualFlow.Spacing = new Vector2(0, 12);
                rcList.Width = 1;
                lnList.Width = 1;
                rcList.SetLayoutInset(new MarginPadding());
                lnList.SetLayoutInset(new MarginPadding());
            }
        }

        private void refresh()
        {
            applyLayoutMode();

            if (skillProvider == null)
            {
                showEmpty(EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER);
                return;
            }

            int keys = KeyCount.Value;
            string? user = TargetUsername.Value;
            bool hasUser = !string.IsNullOrWhiteSpace(user);
            var source = DataSource.Value;

            bool wantChart = source is EzDanPanelDataSource.Chart or EzDanPanelDataSource.Both;
            bool wantPlayer = source is EzDanPanelDataSource.Player or EzDanPanelDataSource.Both;

            IReadOnlyDictionary<string, string> chartSkillsetLabelsRc = new Dictionary<string, string>();
            IReadOnlyDictionary<string, string> chartSkillsetLabelsLn = new Dictionary<string, string>();

            if (wantChart && beatmap?.Value.BeatmapInfo != null)
            {
                var modsList = mods?.Value ?? Array.Empty<Mod>();
                var info = beatmap.Value.BeatmapInfo;

                // Warm MSD when incomplete so filing uses current axis ids.
                var msd = skillProvider.GetBeatmapMsd(info.Hash);
                if (!EzBeatmapMsdComputer.IsCurrentMsdCache(msd))
                    skillProvider.TryGetChartDan(info, modsList);

                if (keys <= 0)
                {
                    try
                    {
                        var playable = beatmap.Value.GetPlayableBeatmap(info.Ruleset, modsList);
                        keys = EzMinaNoteConverter.ResolveKeyCount(playable);
                        if (KeyCount.Value <= 0 && keys > 0)
                            KeyCount.Value = keys;
                    }
                    catch
                    {
                        if (info.Difficulty.CircleSize > 0)
                            keys = (int)info.Difficulty.CircleSize;
                    }
                }

                if (keys > 0)
                {
                    IBeatmap? playable = null;

                    // Warm chart skill info for filing tags (current map DualPanel).
                    try
                    {
                        playable = beatmap.Value.GetPlayableBeatmap(info.Ruleset, modsList);
                        skillProvider.TryGetOrComputeChartSkillInfo(info, playable, modsList);
                    }
                    catch
                    {
                        skillProvider.TryGetOrComputeChartSkillInfo(info, mods: modsList);
                    }

                    chartSkillsetLabelsRc = skillProvider.GetChartDanSkillsetLabels(info, keys, EzDanSide.Rc, modsList, playable);
                    chartSkillsetLabelsLn = skillProvider.GetChartDanSkillsetLabels(info, keys, EzDanSide.Ln, modsList, playable);
                }
            }

            bool hasBeatmap = beatmap?.Value.BeatmapInfo != null;
            bool canShowChart = wantChart && hasBeatmap && keys > 0;
            bool canShowPlayer = wantPlayer && hasUser && keys > 0;

            if (keys <= 0
                || (!canShowChart && !canShowPlayer)
                || (wantPlayer && !wantChart && !hasUser)
                || (wantChart && !wantPlayer && !hasBeatmap))
            {
                showEmpty(EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER);
                return;
            }

            emptyHint.Hide();
            dualFlow.Show();

            bool showClearCounts = ShowClearCounts.Value && wantPlayer && hasUser;

            updateSide(rcList, EzDanSide.Rc, user, keys, chartSkillsetLabelsRc, wantChart, wantPlayer, showClearCounts);
            updateSide(lnList, EzDanSide.Ln, user, keys, chartSkillsetLabelsLn, wantChart, wantPlayer, showClearCounts);
        }

        private void showEmpty(LocalisableString text)
        {
            emptyHint.Text = text;
            emptyHint.Show();
            dualFlow.Hide();
        }

        private void updateSide(
            EzDanLabeledStatList list,
            EzDanSide side,
            string? user,
            int keys,
            IReadOnlyDictionary<string, string> chartSkillsetLabels,
            bool wantChart,
            bool wantPlayer,
            bool showClearCounts)
        {
            string? chartLabel = null;
            string? playerLabel = null;
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets =
                new Dictionary<string, EzDanSkillsetVerdict>();

            var slots = skillProvider?.GetDanSkillsetSlots(keys, side) ?? Array.Empty<EzDanSkillsetSlot>();

            if (wantPlayer && !string.IsNullOrWhiteSpace(user) && keys > 0 && skillProvider != null)
            {
                var estimate = skillProvider.GetDan(user, keys, side.ToId());
                if (estimate != null && !string.IsNullOrEmpty(estimate.Label) && estimate.RawDan >= 0)
                    playerLabel = estimate.Label;

                playerSkillsets = skillProvider.GetDanSkillsets(user, keys, side.ToId());
            }

            if (wantChart && beatmap?.Value.BeatmapInfo != null && skillProvider != null)
                chartLabel = resolveChartAggregateLabel(side, keys);

            list.UpdateContent(
                keys,
                wantChart ? chartLabel : null,
                wantPlayer ? playerLabel : null,
                slots,
                wantChart ? chartSkillsetLabels : new Dictionary<string, string>(),
                wantPlayer ? playerSkillsets : new Dictionary<string, EzDanSkillsetVerdict>(),
                showClearCounts);
        }

        /// <summary>
        /// Hub-style RC|LN chart halves: Sunny/xxy per side only when hold ratio matches that side's primary identity.
        /// Sparse-LN rice charts must not print a high LN Sunny label.
        /// </summary>
        private string? resolveChartAggregateLabel(EzDanSide side, int keys)
        {
            if (beatmap?.Value.BeatmapInfo == null || skillProvider == null)
                return null;

            var info = beatmap.Value.BeatmapInfo;
            var modsList = mods?.Value ?? Array.Empty<Mod>();

            double holdRatio = 0;
            var msd = skillProvider.GetBeatmapMsd(info.Hash);
            if (msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double cachedHold) && double.IsFinite(cachedHold))
                holdRatio = Math.Clamp(cachedHold, 0, 1);

            var chart = skillProvider.TryGetChartDan(info, modsList)
                        ?? skillProvider.TryGetCachedChartDan(info);

            if (holdRatio <= 0 && chart != null && double.IsFinite(chart.HoldRatio))
                holdRatio = Math.Clamp(chart.HoldRatio, 0, 1);

            int gateKeys = keys > 0 ? keys : chart?.KeyCount ?? 0;
            if (gateKeys > 0 && !EzDanAlgorithm.AllowsChartSideHalf(side, gateKeys, holdRatio))
                return null;

            double xxy = info.XxyStarRating;

            if (keys > 0 && xxy >= 0 && double.IsFinite(xxy)
                && EzSunnyDanIntervals.TryLookup(keys, side.ToId(), xxy, out var sunny)
                && !string.IsNullOrEmpty(sunny.DisplayLabel))
            {
                return sunny.DisplayLabel;
            }

            if (chart != null && (keys <= 0 || chart.KeyCount == keys) && chart.Side == side && !string.IsNullOrEmpty(chart.Label))
                return chart.Label;

            return null;
        }
    }
}
