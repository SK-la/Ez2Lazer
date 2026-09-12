// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
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
    /// Chart side: Realm first, then always async live MSD/ChartDan for the selected map (xxySR rhythm; not written).
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

        private bool refreshScheduled;
        private CancellationTokenSource? liveChartCancellation;
        private ModSettingChangeTracker? modSettingTracker;

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

            TargetUsername.BindValueChanged(_ => requestRefresh(), true);
            KeyCount.BindValueChanged(_ => requestRefresh());
            DataSource.BindValueChanged(_ => requestRefresh());
            DualLayout.BindValueChanged(_ => applyLayoutMode(), true);
            ShowClearCounts.BindValueChanged(_ => requestRefresh());

            beatmap?.BindValueChanged(_ => requestRefresh());
            mods?.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = m.NewValue != null
                    ? new ModSettingChangeTracker(m.NewValue) { SettingChanged = _ => requestRefresh() }
                    : null;
                requestRefresh();
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelLiveChart();
            modSettingTracker?.Dispose();
            base.Dispose(isDisposing);
        }

        private void cancelLiveChart()
        {
            liveChartCancellation?.Cancel();
            liveChartCancellation?.Dispose();
            liveChartCancellation = null;
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

        /// <summary>
        /// Coalesce beatmap + KeyCount (and other bindable) changes in the same frame into one refresh.
        /// </summary>
        private void requestRefresh()
        {
            if (refreshScheduled)
                return;

            refreshScheduled = true;
            Schedule(() =>
            {
                refreshScheduled = false;
                refresh();
            });
        }

        private void refresh()
        {
            cancelLiveChart();
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
            EzPersistedChartDan? persistedChartDan = null;
            IReadOnlyList<Mod> localMods = mods?.Value ?? Array.Empty<Mod>();

            if (wantChart && beatmap?.Value.BeatmapInfo != null)
            {
                var info = beatmap.Value.BeatmapInfo;
                // Instant: Realm / session / MSD memory (same role as xxy L1).
                skillProvider.TryGetChartDanForUi(info, out persistedChartDan, allowMemoryCompute: true);

                if (keys <= 0 && info.Difficulty.CircleSize > 0)
                    keys = (int)Math.Round(info.Difficulty.CircleSize);

                if (keys <= 0 && persistedChartDan is { KeyCount: > 0 })
                    keys = persistedChartDan.KeyCount;

                if (keys > 0 && persistedChartDan != null)
                {
                    chartSkillsetLabelsRc = persistedChartDan.SkillsetLabelsFor(EzDanSide.Rc);
                    chartSkillsetLabelsLn = persistedChartDan.SkillsetLabelsFor(EzDanSide.Ln);
                }
            }

            applyPanelContent(keys, user, hasUser, wantChart, wantPlayer, chartSkillsetLabelsRc, chartSkillsetLabelsLn, persistedChartDan);

            // Live recompute (xxySR rhythm). ChartDan overlay:
            // - key convert → live keys + MSD Rating; dan badges only if Sunny available for that key
            // - rate (DT/HT) → keep Realm Sunny badges; refresh Overall MSD
            // - nomod → merge: keep Realm Sunny halves; only fill missing LN/RC
            if (!wantChart || beatmap?.Value.BeatmapInfo == null || skillProvider == null)
                return;

            var beatmapInfo = beatmap.Value.BeatmapInfo;
            var baselineChartDan = persistedChartDan;
            bool keyConvert = EzModRate.ChangesPlayableKeys(localMods);
            bool rateAffects = EzModRate.AffectsChartSkills(localMods) && !keyConvert;
            bool canSunny = EzModRate.CanUsePersistedXxy(localMods) && beatmapInfo.XxyStarRating >= 0;
            liveChartCancellation = new CancellationTokenSource();
            CancellationToken token = liveChartCancellation.Token;
            var provider = skillProvider;

            Task.Factory.StartNew(() => provider.TryComputeLiveChartSkills(beatmapInfo, localMods), token,
                    TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (token.IsCancellationRequested)
                            return;

                        if (task.IsCanceled || task.IsFaulted)
                            return;

                        var snap = task.GetResultSafely();
                        if (snap == null)
                            return;

                        EzPersistedChartDan? displayDan = snap.ChartDan;

                        if (keyConvert)
                        {
                            // Live snapshot already strips MSD-heuristic badges when no Sunny.
                            // Unsupported keys (e.g. 8K Mina): ChartDan null — still apply live keys + MSD if present.
                            if (displayDan == null && snap.Msd.Count == 0 && snap.KeyCount <= 0)
                                return;
                        }
                        else if (rateAffects && baselineChartDan != null && displayDan != null)
                        {
                            displayDan = EzPersistedChartDan.MergeKeepSunnyLabelsUpdateMsd(baselineChartDan, displayDan);
                        }
                        else if (baselineChartDan != null && displayDan != null)
                        {
                            // Nomod with baseline: merge missing halves only.
                            displayDan = EzPersistedChartDan.MergeNomodBaselineWithLive(baselineChartDan, displayDan);

                            if (!canSunny
                                && !baselineChartDan.HasSide(EzDanSide.Ln)
                                && snap.ChartDan!.HasSide(EzDanSide.Ln))
                            {
                                displayDan = new EzPersistedChartDan
                                {
                                    BeatmapHash = displayDan.BeatmapHash,
                                    BeatmapId = displayDan.BeatmapId,
                                    AlgorithmVersion = displayDan.AlgorithmVersion,
                                    KeyCount = displayDan.KeyCount,
                                    HoldRatio = displayDan.HoldRatio,
                                    OverallMsd = displayDan.OverallMsd,
                                    RcRawDan = displayDan.RcRawDan,
                                    RcLabel = displayDan.RcLabel,
                                    RcSkillsetLabels = displayDan.RcSkillsetLabels,
                                    LnRawDan = -1,
                                    LnLabel = string.Empty,
                                    LnSkillsetLabels = new Dictionary<string, string>(),
                                    ComputedAt = displayDan.ComputedAt,
                                };
                            }

                            bool filledHalf = displayDan.HasSide(EzDanSide.Ln) && !baselineChartDan.HasSide(EzDanSide.Ln)
                                              || displayDan.HasSide(EzDanSide.Rc) && !baselineChartDan.HasSide(EzDanSide.Rc);

                            if (!filledHalf)
                            {
                                if (snap.Msd.Count == 0)
                                    return;

                                applyPanelContent(
                                    KeyCount.Value > 0 ? KeyCount.Value : baselineChartDan.KeyCount,
                                    TargetUsername.Value,
                                    !string.IsNullOrWhiteSpace(TargetUsername.Value),
                                    wantChart,
                                    wantPlayer,
                                    baselineChartDan.SkillsetLabelsFor(EzDanSide.Rc),
                                    baselineChartDan.SkillsetLabelsFor(EzDanSide.Ln),
                                    baselineChartDan,
                                    snap.Msd);
                                return;
                            }
                        }
                        else if (displayDan == null)
                        {
                            return;
                        }

                        int liveKeys = displayDan is { KeyCount: > 0 }
                            ? displayDan.KeyCount
                            : (snap.KeyCount > 0 ? snap.KeyCount : KeyCount.Value);

                        applyPanelContent(
                            liveKeys,
                            TargetUsername.Value,
                            !string.IsNullOrWhiteSpace(TargetUsername.Value),
                            wantChart,
                            wantPlayer,
                            displayDan?.SkillsetLabelsFor(EzDanSide.Rc) ?? new Dictionary<string, string>(),
                            displayDan?.SkillsetLabelsFor(EzDanSide.Ln) ?? new Dictionary<string, string>(),
                            displayDan,
                            snap.Msd);
                    });
                }, token);
        }

        private void applyPanelContent(
            int keys,
            string? user,
            bool hasUser,
            bool wantChart,
            bool wantPlayer,
            IReadOnlyDictionary<string, string> chartSkillsetLabelsRc,
            IReadOnlyDictionary<string, string> chartSkillsetLabelsLn,
            EzPersistedChartDan? persistedChartDan,
            IReadOnlyDictionary<string, double>? liveMsd = null)
        {
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

            double? playerOverall = null;
            double? chartOverall = null;
            double? lnRawDanRating = null;

            if (wantPlayer && hasUser && skillProvider != null)
            {
                double overall = skillProvider.GetPlayerSsrSnapshot(user!, keys).Overall;
                if (overall > 0 && double.IsFinite(overall))
                    playerOverall = overall;
            }

            if (wantChart && skillProvider != null)
            {
                if (persistedChartDan != null
                    && persistedChartDan.OverallMsd > 0
                    && double.IsFinite(persistedChartDan.OverallMsd))
                {
                    chartOverall = persistedChartDan.OverallMsd;
                }
                else
                {
                    var msd = liveMsd
                              ?? (beatmap?.Value.BeatmapInfo != null
                                  ? skillProvider.GetBeatmapMsd(beatmap.Value.BeatmapInfo.Hash)
                                  : null)
                              ?? new Dictionary<string, double>();

                    if (msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall)
                        && overall > 0 && double.IsFinite(overall))
                    {
                        chartOverall = overall;
                    }
                }

                if (persistedChartDan is { LnRawDan: >= 0 } && double.IsFinite(persistedChartDan.LnRawDan))
                    lnRawDanRating = persistedChartDan.LnRawDan;
            }

            updateSide(rcList, EzDanSide.Rc, user, keys, chartSkillsetLabelsRc, wantChart, wantPlayer, showClearCounts, playerOverall, chartOverall, persistedChartDan);
            updateSide(lnList, EzDanSide.Ln, user, keys, chartSkillsetLabelsLn, wantChart, wantPlayer, showClearCounts, null, lnRawDanRating, persistedChartDan);
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
            bool showClearCounts,
            double? playerOverallRating,
            double? chartOverallRating,
            EzPersistedChartDan? persistedChartDan)
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

            if (wantChart)
                chartLabel = persistedChartDan?.LabelFor(side);

            // No secondary LN gate: ChartDan persist already applied holdCount/ratio; missing data stays empty.
            list.UpdateContent(
                keys,
                wantChart ? chartLabel : null,
                wantPlayer ? playerLabel : null,
                slots,
                wantChart ? chartSkillsetLabels : new Dictionary<string, string>(),
                wantPlayer ? playerSkillsets : new Dictionary<string, EzDanSkillsetVerdict>(),
                showClearCounts,
                wantPlayer ? playerOverallRating : null,
                wantChart ? chartOverallRating : null);
        }
    }
}
