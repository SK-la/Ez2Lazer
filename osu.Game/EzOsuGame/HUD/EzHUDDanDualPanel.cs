// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
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

        /// <summary>
        /// Last (beatmap, ruleset, mods) the live chart pass was kicked for. Song select fires placeholder then
        /// real star difficulty for the same chart, and several bindables fire in one switch; without this the
        /// whole playable + Mina/xxy pass restarted each time.
        /// </summary>
        private EzDanLiveKickKey? lastLiveKickKey;

        private ScheduledDelegate? liveKickDebounce;

        /// <summary>
        /// Last live overlay actually applied for <see cref="cachedLiveKey"/>. Re-applied after the instant
        /// (Realm) content so a settings-driven refresh does not silently drop back to Realm values.
        /// </summary>
        private EzDanLiveKickKey? cachedLiveKey;

        private EzDanSet? cachedLiveDan;
        private double cachedLiveOverall;
        private int cachedLiveKeys;

        /// <summary>Coalesce fast song-select scrolling so only the settled chart runs the live pass.</summary>
        private const double live_kick_debounce_ms = 150;

        private readonly record struct EzDanLiveKickKey(string Hash, int RulesetId, int ModsSignature);

        [Resolved]
        private EzSkillProvider? skillProvider { get; set; }

        [Resolved(canBeNull: true)]
        private EzAnalysisCache? analysisCache { get; set; }

        /// <summary>
        /// 非 null 表示这块面板挂在 Player 里。游玩期间只读已有分析结果，不发起计算。
        /// </summary>
        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

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
            liveKickDebounce?.Cancel();
            liveKickDebounce = null;
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

        /// <summary>Cancel a pending (not yet started) live kick and clear its guard.</summary>
        private void cancelLiveKick()
        {
            liveKickDebounce?.Cancel();
            liveKickDebounce = null;
            lastLiveKickKey = null;
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
            applyLayoutMode();

            if (skillProvider == null)
            {
                cancelLiveKick();
                cancelLiveChart();
                showEmpty(EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER);
                return;
            }

            int keyCount = KeyCount.Value;
            string? user = TargetUsername.Value;
            bool hasUser = !string.IsNullOrWhiteSpace(user);
            var source = DataSource.Value;

            bool wantChart = source is EzDanPanelDataSource.Chart or EzDanPanelDataSource.Both;
            bool wantPlayer = source is EzDanPanelDataSource.Player or EzDanPanelDataSource.Both;

            IReadOnlyDictionary<string, string> chartSkillsetLabelsRc = new Dictionary<string, string>();
            IReadOnlyDictionary<string, string> chartSkillsetLabelsLn = new Dictionary<string, string>();
            string? chartLabelRc = null;
            string? chartLabelLn = null;
            double? chartOverall = null;
            double? chartLnRaw = null;
            IReadOnlyList<Mod> localMods = mods?.Value ?? Array.Empty<Mod>();

            if (wantChart && beatmap?.Value.BeatmapInfo != null)
            {
                var info = beatmap.Value.BeatmapInfo;
                // Instant: Realm / session / MSD memory (same role as xxy L1).
                if (skillProvider != null && skillProvider.TryGetChartDanForUi(info, out var persistedChartDan, allowMemoryCompute: true)
                    && persistedChartDan != null)
                {
                    chartSkillsetLabelsRc = persistedChartDan.SkillsetLabelsFor(EzDanSide.Rc);
                    chartSkillsetLabelsLn = persistedChartDan.SkillsetLabelsFor(EzDanSide.Ln);
                    chartLabelRc = persistedChartDan.LabelFor(EzDanSide.Rc);
                    chartLabelLn = persistedChartDan.LabelFor(EzDanSide.Ln);
                    chartOverall = persistedChartDan.OverallMsd > 0 ? persistedChartDan.OverallMsd : null;
                    chartLnRaw = persistedChartDan.LnRawDan >= 0 ? persistedChartDan.LnRawDan : null;

                    if (keyCount <= 0 && persistedChartDan.KeyCount > 0)
                        keyCount = persistedChartDan.KeyCount;
                }

                if (keyCount <= 0 && info.Difficulty.CircleSize > 0)
                    keyCount = (int)Math.Round(info.Difficulty.CircleSize);
            }

            applyPanelContent(keyCount, user, hasUser, wantChart, wantPlayer, chartSkillsetLabelsRc, chartSkillsetLabelsLn, chartLabelRc, chartLabelLn, chartOverall, chartLnRaw);

            // Live: one analysis pass serves this panel and the radar alike.
            if (!wantChart || beatmap?.Value.BeatmapInfo == null || analysisCache == null)
            {
                cancelLiveKick();
                cancelLiveChart();
                return;
            }

            var beatmapInfo = beatmap.Value.BeatmapInfo;

            var liveKey = new EzDanLiveKickKey(
                beatmapInfo.Hash,
                beatmapInfo.Ruleset.OnlineID,
                EzAnalysisLookupCache.ComputeModsSignature(localMods));

            void applyLive(EzDanSet? dan, double overall, int keys)
            {
                cachedLiveKey = liveKey;
                cachedLiveDan = dan;
                cachedLiveOverall = overall;
                cachedLiveKeys = keys;

                // A side the live pass could not rate keeps the stored estimate, so a mod change does not blank
                // a half that the baseline already had.
                var rc = dan?.For(EzDanSide.Rc);
                var ln = dan?.For(EzDanSide.Ln);

                applyPanelContent(keys, TargetUsername.Value, !string.IsNullOrWhiteSpace(TargetUsername.Value),
                    wantChart, wantPlayer,
                    rc?.SkillsetLabels ?? chartSkillsetLabelsRc,
                    ln?.SkillsetLabels ?? chartSkillsetLabelsLn,
                    rc?.Label ?? chartLabelRc,
                    ln?.Label ?? chartLabelLn,
                    overall > 0 ? overall : chartOverall,
                    ln?.RawDan ?? chartLnRaw);
            }

            // Already have the live overlay for this exact (beatmap, mods): re-apply it instead of computing again.
            if (cachedLiveKey == liveKey && cachedLiveDan != null)
            {
                applyLive(cachedLiveDan, cachedLiveOverall, cachedLiveKeys);
                lastLiveKickKey = liveKey;
                return;
            }

            // 游玩期间只读已有结果：分析计算放到选歌、结算等非游玩路径，避免进局瞬间抢 CPU 和大量分配。
            if (gameplayState != null)
                return;

            // Same (beatmap, mods) already kicked: keep the in-flight pass alive.
            if (lastLiveKickKey == liveKey)
                return;

            // Superseded chart / mods: stop the previous in-flight pass and pending kick now.
            cancelLiveKick();
            cancelLiveChart();

            lastLiveKickKey = liveKey;

            // Coalesce the per-frame refresh storm from one song switch (and fast scrolling) into a single pass.
            liveKickDebounce?.Cancel();
            liveKickDebounce = Scheduler.AddDelayed(() =>
            {
                liveKickDebounce = null;

                if (lastLiveKickKey != liveKey)
                    return;

                liveChartCancellation = new CancellationTokenSource();
                CancellationToken token = liveChartCancellation.Token;

                analysisCache.GetAnalysisAsync(beatmapInfo, null, localMods, token)
                             .ContinueWith(task =>
                             {
                                 Schedule(() =>
                                 {
                                     if (token.IsCancellationRequested)
                                         return;

                                     if (task.IsCanceled || task.IsFaulted)
                                         return;

                                     if (task.GetResultSafely() is not { } result)
                                     {
                                         // Allow a later re-presentation to retry rather than blocking on a failed kick.
                                         if (lastLiveKickKey == liveKey)
                                             lastLiveKickKey = null;

                                         return;
                                     }

                                     int liveKeys = result.Dan is { KeyCount: > 0 } chartDan
                                         ? chartDan.KeyCount
                                         : KeyCount.Value;

                                     double overall = result.AxesOf(EzChartMetricProducer.Mina.Id())
                                                            .TryGetValue(EzMinaSkillAxis.Overall.ToId(), out double value)
                                         ? value
                                         : 0;

                                     applyLive(result.Dan, overall, liveKeys);
                                 });
                             }, token);
            }, live_kick_debounce_ms);
        }

        private void applyPanelContent(
            int keys,
            string? user,
            bool hasUser,
            bool wantChart,
            bool wantPlayer,
            IReadOnlyDictionary<string, string> chartSkillsetLabelsRc,
            IReadOnlyDictionary<string, string> chartSkillsetLabelsLn,
            string? chartLabelRc,
            string? chartLabelLn,
            double? chartOverall,
            double? chartLnRaw)
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

            if (wantPlayer && hasUser && skillProvider != null)
            {
                double overall = skillProvider.GetPlayerSsrSnapshot(user!, keys).Overall;
                if (overall > 0 && double.IsFinite(overall))
                    playerOverall = overall;
            }

            if (wantChart)
            {
                if (chartOverall is not double || !double.IsFinite(chartOverall.Value) || chartOverall.Value <= 0)
                    chartOverall = null;

                if (chartLnRaw is not double || !double.IsFinite(chartLnRaw.Value) || chartLnRaw.Value < 0)
                    chartLnRaw = null;
            }

            updateSide(rcList, EzDanSide.Rc, user, keys, chartSkillsetLabelsRc, wantChart, wantPlayer, showClearCounts, playerOverall, chartOverall, chartLabelRc);
            updateSide(lnList, EzDanSide.Ln, user, keys, chartSkillsetLabelsLn, wantChart, wantPlayer, showClearCounts, null, chartLnRaw, chartLabelLn);
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
            string? chartLabel)
        {
            string? playerLabel = null;
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets =
                new Dictionary<string, EzDanSkillsetVerdict>();

            var slots = skillProvider?.GetDanSkillsetSlots(keys, side) ?? Array.Empty<EzDanSkillsetSlot>();

            if (wantPlayer && !string.IsNullOrWhiteSpace(user) && keys > 0 && skillProvider != null)
            {
                (string label, double rawDan) = skillProvider.GetDanDisplay(user, keys, side.ToId());
                if (!string.IsNullOrEmpty(label) && rawDan >= 0)
                    playerLabel = label;

                playerSkillsets = skillProvider.GetDanSkillsets(user, keys, side.ToId());
            }

            // No secondary LN gate: the estimate already applied holdCount/ratio; missing data stays empty.
            list.UpdateContent(
                keys,
                wantChart && !string.IsNullOrEmpty(chartLabel) ? chartLabel : null,
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
