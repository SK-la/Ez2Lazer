// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Screens;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace osu.Game.EzOsuGame.HUD
{
    public enum EzRadarDisplayMode
    {
        [Description("Metadate")]
        Metadate,

        [Description("Key Pattern")]
        KeyPattern,

        [Description("SSR Skill")]
        SSRSkill,

        [Description("Skill Dual")]
        MSDSkillDual,

        [Description("Skill Chart")]
        MSDSkillChart,

        [Description("PP+ Dual")]
        PPPSkillDual,

        [Description("PP+ Chart")]
        PPPSkillChart
    }

    /// <summary>
    /// 雷达图面板，显示当前谱面参数的六边形图形化表示
    /// </summary>
    public partial class EzHUDRadarPanel : CompositeDrawable, ISerialisableDrawable
    {
        // Layout constants
        private const float chart_size = 144f;
        private const float axis_label_padding = 36f;
        private const float axis_label_offset = 24f; // Offset from chart edge to label center
        private const float axis_label_alpha = 1f;

        // Data normalization constants
        private const float key_pattern_full_score = 10f;
        private const float long_note_ratio_max = 100f;

        private float[] parameterRatios = new float[6];
        private float[] parameterValues = new float[6];

        private static readonly string[] default_axis_labels = { "BPM", "STAR", "CS", "OD", "HP", "AR" };
        private static readonly string[] default_axis_formats = { "0", "0.00", "0.0", "0.0", "0.0", "0.0" };

        public bool UsesFixedAnchor { get; set; }

        public float MaxBpm { get; } = 200f;

        public float MaxStar { get; } = 10f;

        public float MaxCs { get; } = 10f;

        public float MaxOd { get; } = 10f;

        public float MaxDr { get; } = 10f;

        public float MaxAr { get; } = 10f;

        public float MaxKeyPatternScore { get; } = 10f;

        public float MaxXxySr { get; } = 10f;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DISPLAY_MODE), nameof(EzHUDStrings.RADAR_DISPLAY_MODE_TOOLTIP))]
        public Bindable<EzRadarDisplayMode> RadarDisplayMode { get; } = new Bindable<EzRadarDisplayMode>();

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_USE_ABSOLUTE_VALUE), nameof(EzHUDStrings.RADAR_USE_ABSOLUTE_VALUE_TOOLTIP))]
        public Bindable<bool> UseAbsoluteValue { get; } = new BindableBool();

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BACKGROUND_COLOUR), nameof(EzHUDStrings.RADAR_BOX_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BackgroundColour { get; } = new BindableColour4(new Color4(35, 40, 42, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_LABEL_COLOUR), nameof(EzHUDStrings.RADAR_LABEL_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 LabelColour { get; } = new BindableColour4(new Color4(255, 230, 128, 255));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_BASE_LINE_COLOUR), nameof(EzHUDStrings.RADAR_BASE_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BaseLineColour { get; } = new BindableColour4(new Color4(255, 255, 255, 51));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BaseAreaColour { get; } = new BindableColour4(new Color4(44, 44, 50, 128));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataLineColour { get; } = new BindableColour4(new Color4(255, 217, 102, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataAreaColour { get; } = new BindableColour4(new Color4(255, 217, 102, 128));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_PLAYER_DATA_LINE_COLOUR), nameof(EzHUDStrings.RADAR_PLAYER_DATA_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 PlayerDataLineColour { get; } = new BindableColour4(new Color4(133, 196, 108, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_PLAYER_DATA_AREA_COLOUR), nameof(EzHUDStrings.RADAR_PLAYER_DATA_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 PlayerDataAreaColour { get; } = new BindableColour4(new Color4(133, 196, 108, 128));

        /// <summary>Target player for Skill-mode SSR overlay. Externally bindable (e.g. EzAnalysis header).</summary>
        public Bindable<string?> TargetUsername { get; } = new Bindable<string?>();

        public int AxisCount
        {
            get => chart?.AxisCount ?? parameterRatios.Length;
            set
            {
                int clamped = Math.Max(3, value);

                if (parameterRatios.Length != clamped)
                {
                    Array.Resize(ref parameterRatios, clamped);
                    Array.Resize(ref parameterValues, clamped);
                }

                if (chart != null)
                {
                    chart.AxisCount = clamped;
                    chart.SetData(parameterRatios);
                }

                ensureAxisTexts();
                updateAxisTexts();
            }
        }

        [Resolved]
        private OverlayColourProvider? colourProvider { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private EzSkillProvider? skillProvider { get; set; }

        [Resolved]
        private EzAnalysisPlayerSelection? ezAnalysisPlayerSelection { get; set; }

        [Resolved(CanBeNull = true)]
        private EzAnalysisCache? analysisCache { get; set; }

        private IBindable<StarDifficulty>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private CancellationTokenSource? radarAnalysisCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;

        private Container? axisLabelContainer;
        private EzRadarChart? chart;
        private Box background = null!;
        private FillFlowContainer[] axisLabelContainers = Array.Empty<FillFlowContainer>();
        private string[] activeAxisLabels = default_axis_labels;
        private string[] activeAxisFormats = default_axis_formats;

        /// <summary>Last live L3 result for the current chart + mods; the single source of chart-side vectors.</summary>
        private EzAnalysisResult? cachedLiveAnalysis;

        private EzRadarComputeKey? cachedLiveAnalysisKey;

        /// <summary>Last (beatmap, mods, mode) a live L3 pass was kicked for; blocks duplicate kicks.</summary>
        private EzRadarComputeKey? lastLiveAnalysisKickKey;

        private ScheduledDelegate? liveKickDebounce;

        /// <summary>
        /// Song select fires placeholder then real star difficulty for the same chart; without a coalescing delay a
        /// fast scroll runs the full playable + Mina/xxy pass for every chart passed over. Only the settled chart computes.
        /// </summary>
        private const double live_kick_debounce_ms = 100;

        /// <summary>
        /// Cheap cache/kick identity for both radar paths. <see cref="EzAnalysisLookupCache.ComputeModsSignature"/>
        /// reads live mods directly (no <c>DeepClone</c>), which is what made the old key construction allocate.
        /// </summary>
        private readonly record struct EzRadarComputeKey(string Hash, int RulesetId, int ModsSignature, EzRadarDisplayMode Mode);

        public EzHUDRadarPanel()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = EzUIStyleV1.CORNER_RADIUS_WEDGE,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BackgroundColour.Value,
                        },
                    },
                },
                chart = new EzRadarChart
                {
                    AxisCount = parameterRatios.Length,
                    Size = new Vector2(chart_size),
                    GridLevels = 4,
                    GridColour = BaseLineColour.Value,
                    AxisColour = BaseLineColour.Value,
                    BaseFillColour = BaseAreaColour.Value,
                    DataFillColour = DataAreaColour.Value,
                    DataStrokeColour = DataLineColour.Value,
                    DataPointColour = DataLineColour.Value,
                },
                axisLabelContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(chart_size + axis_label_padding * 2),
                },
            };

            ensureAxisTexts();
            updateAxisTexts();
            applyChartColours();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tryBindSharedPlayerSelection();

            bindPreserveAlpha(BaseLineColour);
            bindPreserveAlpha(BaseAreaColour);
            bindPreserveAlpha(DataLineColour);
            bindPreserveAlpha(DataAreaColour);
            bindPreserveAlpha(PlayerDataLineColour);
            bindPreserveAlpha(PlayerDataAreaColour);
            bindPreserveAlpha(BackgroundColour);
            bindPreserveAlpha(LabelColour);

            // Set default background colour using framework standard if not already set
            if (colourProvider != null && BackgroundColour.IsDefault)
                background.Colour = colourProvider.Background2;

            BaseLineColour.BindValueChanged(_ => applyChartColours(), true);
            BaseAreaColour.BindValueChanged(_ => applyChartColours(), true);
            DataLineColour.BindValueChanged(_ => applyChartColours(), true);
            DataAreaColour.BindValueChanged(_ => applyChartColours(), true);
            PlayerDataLineColour.BindValueChanged(_ => applyChartColours(), true);
            PlayerDataAreaColour.BindValueChanged(_ => applyChartColours(), true);
            BackgroundColour.BindValueChanged(e =>
            {
                background.Colour = e.NewValue;
            }, true);
            LabelColour.BindValueChanged(_ => applyChartColours(), true);
            RadarDisplayMode.BindValueChanged(_ => refreshRadarPresentation(), true);
            UseAbsoluteValue.BindValueChanged(_ => refreshRadarPresentation(), true);
            TargetUsername.BindValueChanged(_ =>
            {
                if (isMinaSkillRadarMode())
                    updateSkillRadarPresentation();
                else if (isPppSkillRadarMode())
                    updatePppSkillRadarPresentation();
            });

            chart?.SetData(parameterRatios);
            updateAxisTexts();

            beatmap.BindValueChanged(b =>
            {
                cachedLiveAnalysis = null;
                cachedLiveAnalysisKey = null;
                resetRadarKickGuards();
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource = new CancellationTokenSource();

                difficultyBindable?.UnbindAll();
                difficultyBindable = difficultyCache.GetBindableDifficulty(b.NewValue.BeatmapInfo, difficultyCancellationSource.Token);
                difficultyBindable.BindValueChanged(d =>
                {
                    updateParameterRatios(d.NewValue);
                }, true);
            }, true);

            mods.BindValueChanged(m =>
            {
                cachedLiveAnalysis = null;
                cachedLiveAnalysisKey = null;
                resetRadarKickGuards();
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => updateParameterRatios(difficultyBindable?.Value ?? default)
                };
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }, true);

            ruleset.BindValueChanged(_ =>
            {
                cachedLiveAnalysis = null;
                cachedLiveAnalysisKey = null;
                resetRadarKickGuards();
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }, true);
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

        private void refreshRadarPresentation()
        {
            // Leaving skill radar mode: drop any pending live kick rather than letting it land on the new mode.
            if (!isMinaSkillRadarMode())
                cancelLiveKick();

            if (RadarDisplayMode.Value == EzRadarDisplayMode.Metadate)
            {
                chart?.ClearSecondaryData();
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }
            else if (isMinaSkillRadarMode())
                updateSkillRadarPresentation();
            else if (isPppSkillRadarMode())
                updatePppSkillRadarPresentation();
            else
            {
                chart?.ClearSecondaryData();
                updateRulesetSpecificRadarPresentation();
            }
        }

        private static void bindPreserveAlpha(BindableColour4 colourBindable)
        {
            colourBindable.BindValueChanged(e =>
            {
                bool rgbChanged = !nearlyEqual(e.OldValue.R, e.NewValue.R) ||
                                  !nearlyEqual(e.OldValue.G, e.NewValue.G) ||
                                  !nearlyEqual(e.OldValue.B, e.NewValue.B);

                bool alphaChanged = !nearlyEqual(e.OldValue.A, e.NewValue.A);

                if (!rgbChanged || !alphaChanged)
                    return;

                colourBindable.Value = new Color4(e.NewValue.R, e.NewValue.G, e.NewValue.B, e.OldValue.A);
            });
        }

        private static bool nearlyEqual(float x, float y) => Math.Abs(x - y) < 0.0001f;

        private void updateParameterRatios(StarDifficulty difficulty)
        {
            if (isMinaSkillRadarMode())
            {
                updateSkillRadarPresentation();
                return;
            }

            if (isPppSkillRadarMode())
            {
                updatePppSkillRadarPresentation();
                return;
            }

            if (RadarDisplayMode.Value != EzRadarDisplayMode.Metadate)
            {
                updateRulesetSpecificRadarPresentation();
                return;
            }

            cancelRadarAnalysis();
            chart?.ClearSecondaryData();
            applyRadarData(createGeneralRadarData(difficulty), getGeneralAxisMaxValues());
        }

        private void updateRulesetSpecificRadarPresentation()
        {
            if (RadarDisplayMode.Value is EzRadarDisplayMode.Metadate or EzRadarDisplayMode.MSDSkillDual or EzRadarDisplayMode.MSDSkillChart
                or EzRadarDisplayMode.PPPSkillDual or EzRadarDisplayMode.PPPSkillChart)
                return;

            if (beatmap.Value.BeatmapInfo is BeatmapInfo beatmapInfo && EzAnalysisProviderBridge.HasAnalysisProvider(ruleset.Value))
            {
                if (tryGetCachedLiveAnalysis(beatmapInfo, out var analysis) && analysis?.Radar is { } cachedRadar)
                {
                    applyRulesetSpecificRadarResult(cachedRadar);
                    return;
                }

                // Nothing live for this chart + mods yet: paint nothing rather than the general star radar, so a
                // placeholder -> real star rebind does not visibly swap axis sets twice.
                cancelRadarAnalysis();
                clearChartData();
                chart?.ClearSecondaryData();
                ensureLiveAnalysis(beatmapInfo);
                return;
            }

            // No analysis provider for this ruleset: keep the general star/CS/OD radar (pre-existing fallback).
            cancelRadarAnalysis();
            chart?.ClearSecondaryData();
            applyRadarData(createGeneralRadarData(difficultyBindable?.Value ?? default), getGeneralAxisMaxValues());
        }

        /// <summary>
        /// Chart-side PP+ vector for the radar, re-keyed to the stored skill ids this panel draws with.
        /// Falls back to the stored rows when no live analysis is available for this chart and mod set.
        /// </summary>
        private IReadOnlyDictionary<string, double> resolvePppChartSkills(BeatmapInfo beatmapInfo)
        {
            if (tryGetCachedLiveAnalysis(beatmapInfo, out var analysis) && analysis is { } live)
            {
                var axes = live.AxesOf(EzMetricProducers.PPP_CHART);

                if (axes.Count > 0)
                {
                    var bySkillId = new Dictionary<string, double>(axes.Count, StringComparer.Ordinal);

                    foreach (var (axisId, value) in axes)
                        bySkillId[EzPPPSkillCatalog.ChartSkillId(axisId)] = value;

                    return bySkillId;
                }
            }

            return skillProvider?.GetBeatmapSystemSkills(beatmapInfo, EzSkillSystems.BEATMAP_PPP)
                   ?? new Dictionary<string, double>();
        }

        /// <summary>
        /// True when the cached live result belongs to this chart + ruleset + mods, independent of display mode.
        /// </summary>
        private bool tryGetCachedLiveAnalysis(BeatmapInfo beatmapInfo, out EzAnalysisResult? analysis)
        {
            analysis = null;

            if (cachedLiveAnalysis is not { } cached || cachedLiveAnalysisKey is not { } key)
                return false;

            if (key.Hash != beatmapInfo.Hash || key.RulesetId != ruleset.Value.OnlineID || key.ModsSignature != EzAnalysisLookupCache.ComputeModsSignature(mods.Value))
                return false;

            analysis = cached;
            return true;
        }

        /// <summary>
        /// Asks L3 for the current chart + mods. Live on every distinct input, independent of
        /// <see cref="Ez2Setting.EzAnalysisRecEnabled"/>: the radar must reflect the selected mods.
        /// </summary>
        private void ensureLiveAnalysis(BeatmapInfo beatmapInfo)
        {
            if (analysisCache == null)
                return;

            var key = createRadarKey(beatmapInfo, ruleset.Value, mods.Value, RadarDisplayMode.Value);

            if (cachedLiveAnalysisKey == key || lastLiveAnalysisKickKey == key)
                return;

            cancelRadarAnalysis();
            radarAnalysisCancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = radarAnalysisCancellationSource.Token;

            lastLiveAnalysisKickKey = key;

            RulesetInfo localRuleset = ruleset.Value;
            IReadOnlyList<Mod> localMods = mods.Value;

            analysisCache.GetLiveAnalysisAsync(beatmapInfo, localRuleset, localMods, cancellationToken)
                         .ContinueWith(task =>
                         {
                             Schedule(() =>
                             {
                                 if (cancellationToken.IsCancellationRequested)
                                     return;

                                 EzAnalysisResult? result = task.IsCanceled || task.IsFaulted ? null : task.GetResultSafely();

                                 if (result == null)
                                 {
                                     // A failed pass must not block a retry on the next presentation.
                                     if (lastLiveAnalysisKickKey == key)
                                         lastLiveAnalysisKickKey = null;

                                     return;
                                 }

                                 cachedLiveAnalysisKey = key;
                                 cachedLiveAnalysis = result;

                                 if (lastLiveAnalysisKickKey == key)
                                     lastLiveAnalysisKickKey = null;

                                 refreshRadarPresentation();
                             });
                         }, cancellationToken);
        }

        private void updateSkillRadarPresentation()
        {
            var beatmapInfo = beatmap.Value.BeatmapInfo;

            if (beatmapInfo == null)
            {
                cancelLiveKick();
                cancelRadarAnalysis();
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            var profile = skillProvider?.GetProfile(beatmapInfo.Ruleset.OnlineID);

            if (profile == null)
            {
                cancelLiveKick();
                cancelRadarAnalysis();
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            if (beatmapInfo.Ruleset.OnlineID == EzLocalProfileConstants.OSU_RULESET_ID)
            {
                cancelLiveKick();
                cancelRadarAnalysis();
                updateOsuSkillRadarPresentation(beatmapInfo, profile);
                return;
            }

            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
            {
                cancelLiveKick();
                cancelRadarAnalysis();
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            // Key locked to current beatmap CS (same-beatmap ChartDan only if CS missing).
            // Live mods may override after async compute.
            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);

            // One Realm read serves both the CS-missing key fallback and the LN pre-judgement below.
            EzPersistedChartDan? persistedChartDan = null;

            skillProvider?.TryGetPersistedChartDan(beatmapInfo, out persistedChartDan);

            if (keyCount <= 0 && persistedChartDan is { KeyCount: > 0 })
                keyCount = persistedChartDan.KeyCount;

            string? username = TargetUsername.Value;
            IReadOnlyList<Mod> localMods = mods.Value;

            // Instant: stored rows, replaced by the live analysis once it lands.
            IReadOnlyDictionary<string, double> storedMsd = skillProvider?.GetBeatmapMsd(beatmapInfo.Hash)
                                                            ?? new Dictionary<string, double>();

            double holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            int holdCount = EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);

            // Decide LN vs RC *before* painting: the live analysis lands a few frames later, and painting the RC
            // radar first visibly morphs into the LN radar (different axis count + labels).
            bool lnExpected = expectsLnSkillRadar(beatmapInfo, keyCount, storedMsd, persistedChartDan);

            if (tryGetCachedLiveAnalysis(beatmapInfo, out var analysis) && analysis is { } live)
            {
                // Known result for this exact (beatmap, mods): paint it directly, no LN/RC frame to correct.
                applySkillRadarLayers(keyCount, username, minaAxesOf(live), structureAxesOf(live), holdRatio, holdCount);
                return;
            }

            if (lnExpected)
                applyLnSkillRadar(EzLnSkillRadar.BuildAxes(holdRatio, EzManiaLNAxisExtensions.FromFeatures(empty_metrics), keyCount));
            else
                applySkillRadarLayers(keyCount, username, storedMsd, EzManiaLNAxisExtensions.FromFeatures(empty_metrics), holdRatio, holdCount);

            ensureLiveAnalysis(beatmapInfo);
        }

        /// <summary>
        /// Mina axis values re-keyed to the stored skill ids this panel draws with.
        /// </summary>
        private static IReadOnlyDictionary<string, double> minaAxesOf(in EzAnalysisResult analysis)
        {
            var axes = analysis.AxesOf(EzMetricProducers.MINA_CHART);

            if (axes.Count == 0)
                return new Dictionary<string, double>();

            var byStoredId = new Dictionary<string, double>(axes.Count, StringComparer.Ordinal);

            foreach (var (axisId, value) in axes)
                byStoredId[$"{EzSkillSystems.BEATMAP_MINA}.{axisId}"] = value;

            return byStoredId;
        }

        private static IReadOnlyDictionary<string, double> structureAxesOf(in EzAnalysisResult analysis)
            => analysis.AxesOf(EzMetricProducers.CSI_CHART);

        /// <summary>
        /// Synchronous LN/RC pre-judgement. Realm-persisted ChartDan with an LN half is authoritative
        /// (written through the same hold gate); otherwise fall back to that gate using Realm MSD hold ratio
        /// and the stored hold-object count.
        /// </summary>
        private bool expectsLnSkillRadar(BeatmapInfo beatmapInfo, int keyCount, IReadOnlyDictionary<string, double> msd, EzPersistedChartDan? persistedChartDan)
        {
            if (keyCount <= 0)
                return false;

            if (persistedChartDan != null && persistedChartDan.HasSide(EzDanSide.Ln))
                return true;

            double holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            int holdCount = EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);

            return EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount);
        }

        private void updateOsuSkillRadarPresentation(BeatmapInfo beatmapInfo, EzSkillProfile profile)
        {
            string chartSystemId = profile.DefaultChartSystemId ?? EzSkillSystems.BEATMAP_PPP;
            string playerSystemId = profile.DefaultPlayerSystemId ?? EzSkillSystems.PLAYER_PPP;

            IReadOnlyDictionary<string, double> chartSkills = chartSystemId == EzSkillSystems.BEATMAP_PPP
                ? resolvePppChartSkills(beatmapInfo)
                : skillProvider?.GetBeatmapSystemSkills(beatmapInfo, chartSystemId) ?? new Dictionary<string, double>();

            ensureLiveAnalysis(beatmapInfo);

            string? username = TargetUsername.Value;
            IReadOnlyDictionary<string, double> playerSkills = new Dictionary<string, double>();

            if (!string.IsNullOrWhiteSpace(username) && skillProvider != null)
                playerSkills = skillProvider.GetPlayerSystemSkills(username, EzSkillSystems.UNSLICED_KEY, playerSystemId);

            bool beatmapOnly = RadarDisplayMode.Value == EzRadarDisplayMode.MSDSkillChart;
            const double display_min = 0.01;

            var selected = new List<EzPPPSkillAxis>();

            foreach (var axis in EzOsuSkillAxisExtensions.RadarAxes)
            {
                double chartValue = chartSkills.GetValueOrDefault(axis.ToChartSkillId(), 0);
                if (!double.IsFinite(chartValue) || chartValue < display_min)
                    continue;

                if (beatmapOnly)
                {
                    selected.Add(axis);
                    continue;
                }

                double playerValue = playerSkills.GetValueOrDefault(axis.ToPlayerSkillId(), 0);
                if (!double.IsFinite(playerValue) || playerValue < display_min)
                    continue;

                selected.Add(axis);
            }

            if (selected.Count == 0 && !beatmapOnly)
            {
                foreach (var axis in EzOsuSkillAxisExtensions.RadarAxes)
                {
                    double chartValue = chartSkills.GetValueOrDefault(axis.ToChartSkillId(), 0);
                    if (double.IsFinite(chartValue) && chartValue >= display_min)
                        selected.Add(axis);
                }
            }

            if (selected.Count == 0)
            {
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            AxisCount = Math.Max(3, selected.Count);
            activeAxisLabels = new string[AxisCount];
            activeAxisFormats = Enumerable.Repeat("0.00", AxisCount).ToArray();

            double maxValue = 0;
            double[] chartValues = new double[AxisCount];
            double[] playerValues = new double[AxisCount];

            for (int i = 0; i < selected.Count; i++)
            {
                var axis = selected[i];
                activeAxisLabels[i] = axis.Meta().NameEn;
                double beatmapValue = chartSkills.GetValueOrDefault(axis.ToChartSkillId(), 0);
                double playerValue = beatmapOnly ? 0 : playerSkills.GetValueOrDefault(axis.ToPlayerSkillId(), 0);
                if (playerValue < display_min)
                    playerValue = 0;

                chartValues[i] = beatmapValue;
                playerValues[i] = playerValue;
                parameterValues[i] = (float)beatmapValue;
                maxValue = Math.Max(maxValue, Math.Max(beatmapValue, playerValue));
            }

            for (int i = selected.Count; i < AxisCount; i++)
            {
                activeAxisLabels[i] = string.Empty;
                chartValues[i] = 0;
                playerValues[i] = 0;
                parameterValues[i] = 0;
            }

            if (maxValue <= 0)
                maxValue = 1;

            float[] beatmapRatios = new float[AxisCount];
            float[] playerRatios = new float[AxisCount];

            for (int i = 0; i < AxisCount; i++)
            {
                beatmapRatios[i] = (float)(chartValues[i] / maxValue);
                playerRatios[i] = (float)(playerValues[i] / maxValue);
            }

            chart?.SetData(beatmapRatios);

            if (beatmapOnly)
                chart?.ClearSecondaryData();
            else
                chart?.SetSecondaryData(playerRatios);

            updateAxisTexts();
            applyChartColours();
        }

        private void applySkillRadarLayers(
            int keyCount,
            string? username,
            IReadOnlyDictionary<string, double> msd,
            IReadOnlyDictionary<string, double> structureAxes,
            double holdRatio,
            int holdCount)
        {
            // LN skill: same gate as DualPanel / dan LN half (hold count above the gate OR hub ratio).
            if (keyCount > 0
                && EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount))
            {
                applyLnSkillRadar(EzLnSkillRadar.BuildAxes(holdRatio, structureAxes, keyCount));
                return;
            }

            // All keys: Mina SSR ↔ MSD (same algorithm). No fabricated pattern yellow.
            IReadOnlyDictionary<string, double> ssr = new Dictionary<string, double>();

            if (!string.IsNullOrWhiteSpace(username) && keyCount > 0 && skillProvider != null)
                ssr = skillProvider.GetPlayerSsrSnapshot(username, keyCount).Values;

            bool beatmapOnly = RadarDisplayMode.Value == EzRadarDisplayMode.MSDSkillChart;

            var selected = new List<EzMinaSkillAxis>(skill_radar_axes.Length);

            foreach (var axis in skill_radar_axes)
            {
                double chartValue = msd.GetValueOrDefault(axis.ToMsdSkillId(), 0);
                // Same floor as the player SSR side / hub skillModeEntries: the generic n-key
                // engine returns a ~0.18 sliver for skillsets it does not rate (Technical on
                // 5K and 8K+), which is noise next to a real 10+ bar — not a spoke.
                if (!double.IsFinite(chartValue) || chartValue < EzPatternRatings.DISPLAY_MIN)
                    continue;

                if (beatmapOnly)
                {
                    selected.Add(axis);
                    continue;
                }

                double playerValue = ssr.GetValueOrDefault(axis.ToSsrSkillId(), 0);
                if (!double.IsFinite(playerValue) || playerValue < EzPatternRatings.DISPLAY_MIN)
                    continue;

                // Dual: commonality only — both chart MSD and player SSR present.
                selected.Add(axis);
            }

            // Beatmap: show every real MSD axis above the hub display floor (pad to 3 for EzRadarChart).
            // Dual with no overlap: fall back to chart axes.
            if (selected.Count == 0 && !beatmapOnly)
            {
                foreach (var axis in skill_radar_axes)
                {
                    double chartValue = msd.GetValueOrDefault(axis.ToMsdSkillId(), 0);
                    if (double.IsFinite(chartValue) && chartValue >= EzPatternRatings.DISPLAY_MIN)
                        selected.Add(axis);
                }
            }

            if (selected.Count == 0)
            {
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            AxisCount = Math.Max(3, selected.Count);
            activeAxisLabels = new string[AxisCount];
            activeAxisFormats = Enumerable.Repeat("0.00", AxisCount).ToArray();

            double maxMina = 0;
            double[] chartValues = new double[AxisCount];
            double[] playerValues = new double[AxisCount];

            for (int i = 0; i < selected.Count; i++)
            {
                var axis = selected[i];
                activeAxisLabels[i] = skillAxisDisplayName(axis);
                double beatmapValue = msd.GetValueOrDefault(axis.ToMsdSkillId(), 0);
                double playerValue = beatmapOnly ? 0 : ssr.GetValueOrDefault(axis.ToSsrSkillId(), 0);
                if (playerValue < EzPatternRatings.DISPLAY_MIN)
                    playerValue = 0;

                chartValues[i] = beatmapValue;
                playerValues[i] = playerValue;
                parameterValues[i] = (float)beatmapValue;
                maxMina = Math.Max(maxMina, Math.Max(beatmapValue, playerValue));
            }

            for (int i = selected.Count; i < AxisCount; i++)
            {
                activeAxisLabels[i] = string.Empty;
                chartValues[i] = 0;
                playerValues[i] = 0;
                parameterValues[i] = 0;
            }

            if (maxMina <= 0)
                maxMina = 1;

            float[] beatmapRatios = new float[AxisCount];
            float[] playerRatios = new float[AxisCount];

            for (int i = 0; i < AxisCount; i++)
            {
                beatmapRatios[i] = (float)(chartValues[i] / maxMina);
                playerRatios[i] = (float)(playerValues[i] / maxMina);
            }

            chart?.SetData(beatmapRatios);

            if (beatmapOnly)
                chart?.ClearSecondaryData();
            else
                chart?.SetSecondaryData(playerRatios);

            updateAxisTexts();
            applyChartColours();
        }

        private void applyLnSkillRadar(
            EzChartFeatureMetrics metrics,
            IReadOnlyDictionary<string, double>? subtypeScores,
            double holdRatio,
            int keyCount)
            => applyLnSkillRadar(EzLnSkillRadar.BuildAxes(holdRatio, EzManiaLNAxisExtensions.FromFeatures(metrics, subtypeScores), keyCount));

        /// <summary>Zeroed frame with the right labels, painted before the async pass lands.</summary>
        private static readonly EzChartFeatureMetrics empty_metrics = new EzChartFeatureMetrics();

        private void applyLnSkillRadar(IReadOnlyList<EzLnSkillRadar.Axis> lnAxes)
        {
            AxisCount = lnAxes.Count;
            activeAxisLabels = lnAxes.Select(static a => a.Label.ToString()).ToArray();
            activeAxisFormats = Enumerable.Repeat("0.00", lnAxes.Count).ToArray();

            float[] beatmapRatios = new float[lnAxes.Count];
            float[] playerRatios = new float[lnAxes.Count];

            for (int i = 0; i < lnAxes.Count; i++)
            {
                // Labels show per-axis normalized floats (comparable across different raw scales).
                parameterValues[i] = lnAxes[i].Normalized;
                beatmapRatios[i] = lnAxes[i].Normalized;
                playerRatios[i] = 0;
            }

            chart?.SetData(beatmapRatios);
            chart?.SetSecondaryData(playerRatios);
            updateAxisTexts();
            applyChartColours();
        }

        private bool isMinaSkillRadarMode()
            => RadarDisplayMode.Value is EzRadarDisplayMode.MSDSkillDual or EzRadarDisplayMode.MSDSkillChart;

        private bool isPppSkillRadarMode()
            => RadarDisplayMode.Value is EzRadarDisplayMode.PPPSkillDual or EzRadarDisplayMode.PPPSkillChart;

        private void updatePppSkillRadarPresentation()
        {
            var beatmapInfo = beatmap.Value.BeatmapInfo;

            if (beatmapInfo == null || !EzPPPSkillCatalog.SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
            {
                cancelLiveKick();
                cancelRadarAnalysis();
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            cancelLiveKick();
            cancelRadarAnalysis();

            int rulesetId = beatmapInfo.Ruleset.OnlineID;
            IReadOnlyDictionary<string, double> chartSkills = resolvePppChartSkills(beatmapInfo);

            ensureLiveAnalysis(beatmapInfo);

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            int slice = EzPPPSkillCatalog.PlayerSliceKey(rulesetId, keyCount);
            string? username = TargetUsername.Value;
            IReadOnlyDictionary<string, double> playerSkills = new Dictionary<string, double>();

            if (!string.IsNullOrWhiteSpace(username) && skillProvider != null)
                playerSkills = skillProvider.GetPlayerSystemSkills(username, slice, EzSkillSystems.PLAYER_PPP);

            bool beatmapOnly = RadarDisplayMode.Value == EzRadarDisplayMode.PPPSkillChart;
            var axes = EzPPPSkillCatalog.RadarAxes(rulesetId);
            const double display_min = 0.01;
            var selected = new List<EzSkillSpec>();

            foreach (var axis in axes)
            {
                if (axis.MetricId.StartsWith("_pad", StringComparison.Ordinal))
                {
                    selected.Add(axis);
                    continue;
                }

                double chartValue = chartSkills.GetValueOrDefault(axis.ChartSkillId, 0);

                if (!double.IsFinite(chartValue) || chartValue < display_min)
                    continue;

                if (beatmapOnly)
                {
                    selected.Add(axis);
                    continue;
                }

                double playerValue = playerSkills.GetValueOrDefault(axis.PlayerSkillId, 0);

                if (!double.IsFinite(playerValue) || playerValue < display_min)
                    continue;

                selected.Add(axis);
            }

            if (selected.Count == 0 && !beatmapOnly)
            {
                foreach (var axis in axes)
                {
                    if (axis.MetricId.StartsWith("_pad", StringComparison.Ordinal))
                        continue;

                    double chartValue = chartSkills.GetValueOrDefault(axis.ChartSkillId, 0);

                    if (double.IsFinite(chartValue) && chartValue >= display_min)
                        selected.Add(axis);
                }
            }

            while (selected.Count < 3)
                selected.Add(EzSkillSpec.Pad(selected.Count));

            if (selected.Count == 0)
            {
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            AxisCount = Math.Max(3, selected.Count);
            activeAxisLabels = new string[AxisCount];
            activeAxisFormats = Enumerable.Repeat("0.00", AxisCount).ToArray();

            double maxValue = 0;
            double[] chartValues = new double[AxisCount];
            double[] playerValues = new double[AxisCount];

            for (int i = 0; i < selected.Count; i++)
            {
                var axis = selected[i];
                activeAxisLabels[i] = axis.DisplayName.ToString();
                bool padded = axis.MetricId.StartsWith("_pad", StringComparison.Ordinal);
                double beatmapValue = padded ? 0 : chartSkills.GetValueOrDefault(axis.ChartSkillId, 0);
                double playerValue = beatmapOnly || padded ? 0 : playerSkills.GetValueOrDefault(axis.PlayerSkillId, 0);

                if (playerValue < display_min)
                    playerValue = 0;

                chartValues[i] = beatmapValue;
                playerValues[i] = playerValue;
                parameterValues[i] = (float)beatmapValue;
                maxValue = Math.Max(maxValue, Math.Max(beatmapValue, playerValue));
            }

            for (int i = selected.Count; i < AxisCount; i++)
            {
                activeAxisLabels[i] = string.Empty;
                chartValues[i] = 0;
                playerValues[i] = 0;
                parameterValues[i] = 0;
            }

            if (maxValue <= 0)
                maxValue = 1;

            float[] beatmapRatios = new float[AxisCount];
            float[] playerRatios = new float[AxisCount];

            for (int i = 0; i < AxisCount; i++)
            {
                beatmapRatios[i] = (float)(chartValues[i] / maxValue);
                playerRatios[i] = (float)(playerValues[i] / maxValue);
            }

            chart?.SetData(beatmapRatios);

            if (beatmapOnly)
                chart?.ClearSecondaryData();
            else
                chart?.SetSecondaryData(playerRatios);

            updateAxisTexts();
            applyChartColours();
        }

        private static readonly EzMinaSkillAxis[] skill_radar_axes = EzMinaSkillAxisExtensions.RadarAxes;

        private static string skillAxisDisplayName(EzMinaSkillAxis axis)
            => axis.Chip().Name.ToString();

        private static EzRadarComputeKey createRadarKey(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod>? mods, EzRadarDisplayMode mode)
            => new EzRadarComputeKey(beatmapInfo.Hash, ruleset.OnlineID, EzAnalysisLookupCache.ComputeModsSignature(mods), mode);

        private void cancelRadarAnalysis()
        {
            radarAnalysisCancellationSource?.Cancel();
            radarAnalysisCancellationSource?.Dispose();
            radarAnalysisCancellationSource = null;
        }

        /// <summary>
        /// Drop pending kick guards. Called when the chart / mods / ruleset changed, so the next presentation is
        /// allowed to kick again (a chart switched away from and back must not be blocked by a cancelled kick).
        /// </summary>
        private void resetRadarKickGuards()
        {
            lastLiveAnalysisKickKey = null;
        }

        /// <summary>Let the next presentation kick a fresh live pass instead of waiting on a superseded one.</summary>
        private void cancelLiveKick()
        {
            lastLiveAnalysisKickKey = null;
        }

        private EzRadarChartData<string> createGeneralRadarData(StarDifficulty difficulty)
        {
            var beatmapInfo = beatmap.Value.BeatmapInfo;

            return EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("BPM", beatmapInfo.BPM, "0"),
                new EzRadarAxisValue<string>("STAR", difficulty.Stars > 0 ? difficulty.Stars : beatmapInfo.StarRating, "0.00"),
                new EzRadarAxisValue<string>("CS", beatmapInfo.Difficulty.CircleSize, "0.0"),
                new EzRadarAxisValue<string>("OD", beatmapInfo.Difficulty.OverallDifficulty, "0.0"),
                new EzRadarAxisValue<string>("HP", beatmapInfo.Difficulty.DrainRate, "0.0"),
                new EzRadarAxisValue<string>("AR", beatmapInfo.Difficulty.ApproachRate, "0.0"));
        }

        private void applyRadarData(EzRadarChartData<string> radarData, IReadOnlyList<float> maxValues)
        {
            AxisCount = radarData.Count;

            activeAxisLabels = new string[radarData.Count];
            activeAxisFormats = new string[radarData.Count];

            for (int i = 0; i < radarData.Count; i++)
            {
                EzRadarAxisValue<string> axis = radarData[i];

                activeAxisLabels[i] = EzKeyPatternAxisExtensions.TryParse(axis.Axis, out var pattern)
                    ? pattern.DisplayName().ToString()
                    : axis.Axis;
                activeAxisFormats[i] = axis.Format;
                parameterValues[i] = (float)axis.Value;
                parameterRatios[i] = normalise(parameterValues[i], i < maxValues.Count ? maxValues[i] : 0);
            }

            for (int i = radarData.Count; i < parameterValues.Length; i++)
            {
                parameterValues[i] = 0;
                parameterRatios[i] = 0;
            }

            chart?.SetData(parameterRatios);
            chart?.ClearSecondaryData();
            updateAxisTexts();
        }

        private void applyRulesetSpecificRadarResult(EzRulesetSpecificRadarResult radarResult)
        {
            chart?.ClearSecondaryData();
            EzRadarChartData<string> displayedRadarData = createDisplayedRulesetRadarData(radarResult);
            applyRadarData(displayedRadarData, getRulesetSpecificAxisMaxValues());
        }

        private IReadOnlyList<float> getGeneralAxisMaxValues() => new[] { MaxBpm, MaxStar, MaxCs, MaxOd, MaxDr, MaxAr };

        private IReadOnlyList<float> getRulesetSpecificAxisMaxValues()
            => RadarDisplayMode.Value == EzRadarDisplayMode.SSRSkill
                ? new[] { MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr }
                : UseAbsoluteValue.Value
                    ? new[] { MaxXxySr, MaxXxySr, long_note_ratio_max, MaxXxySr, MaxXxySr, MaxXxySr }
                    : new[] { MaxKeyPatternScore, MaxKeyPatternScore, long_note_ratio_max, MaxKeyPatternScore, MaxKeyPatternScore, MaxKeyPatternScore };

        private EzRadarChartData<string> createDisplayedRulesetRadarData(EzRulesetSpecificRadarResult radarResult)
        {
            EzRadarChartData<string> sourceRadarData = getSelectedRulesetSpecificRadarData(radarResult);

            if (RadarDisplayMode.Value == EzRadarDisplayMode.SSRSkill)
                return sourceRadarData;

            if (!UseAbsoluteValue.Value || radarResult.XxySr is not double xxySr || xxySr <= 0)
                return sourceRadarData;

            bool preserveLongNotePercentage = RadarDisplayMode.Value == EzRadarDisplayMode.KeyPattern;
            var axes = new EzRadarAxisValue<string>[sourceRadarData.Count];

            for (int i = 0; i < sourceRadarData.Count; i++)
            {
                EzRadarAxisValue<string> axis = sourceRadarData[i];

                if (preserveLongNotePercentage && axis.Axis == "LN%")
                {
                    axes[i] = axis;
                    continue;
                }

                double absoluteValue = Math.Round(axis.Value / key_pattern_full_score * xxySr, 2, MidpointRounding.AwayFromZero);
                axes[i] = new EzRadarAxisValue<string>(axis.Axis, absoluteValue, "0.00");
            }

            return EzRadarChartData<string>.Create(axes);
        }

        private EzRadarChartData<string> getSelectedRulesetSpecificRadarData(EzRulesetSpecificRadarResult radarResult)
            => RadarDisplayMode.Value == EzRadarDisplayMode.SSRSkill ? radarResult.XxySrPatternRadarData : radarResult.KeyPatternRadarData;

        private void clearChartData()
        {
            Array.Clear(parameterValues, 0, parameterValues.Length);
            Array.Clear(parameterRatios, 0, parameterRatios.Length);
            chart?.SetData(parameterRatios);
            chart?.ClearSecondaryData();
            updateAxisTexts();
        }

        private static float normalise(float value, float maxValue)
        {
            if (maxValue <= 0)
                return 0;

            return Math.Clamp(value / maxValue, 0, 1);
        }

        private void applyChartColours()
        {
            if (chart == null)
                return;

            chart.GridColour = BaseLineColour.Value;
            chart.AxisColour = BaseLineColour.Value;
            chart.BaseFillColour = BaseAreaColour.Value;
            chart.DataStrokeColour = DataLineColour.Value;
            chart.DataPointColour = DataLineColour.Value;
            chart.DataFillColour = DataAreaColour.Value;
            chart.SecondaryDataStrokeColour = PlayerDataLineColour.Value;
            chart.SecondaryDataPointColour = PlayerDataLineColour.Value;
            chart.SecondaryDataFillColour = PlayerDataAreaColour.Value;
            chart.InvalidateChartGeometry();

            foreach (var container in axisLabelContainers)
            {
                foreach (var text in container.OfType<OsuSpriteText>())
                    text.Colour = withFixedAlpha(LabelColour.Value);
            }
        }

        private void ensureAxisTexts()
        {
            if (axisLabelContainer == null || chart == null)
                return;

            int axisCount = chart.AxisCount;
            if (axisLabelContainers.Length == axisCount)
                return;

            axisLabelContainers = new FillFlowContainer[axisCount];

            for (int i = 0; i < axisCount; i++)
            {
                // Create a FillFlowContainer to automatically stack label and value texts
                axisLabelContainers[i] = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2), // Vertical spacing between lines
                    Children = new Drawable[]
                    {
                        // Label text (top line)
                        new OsuSpriteText
                        {
                            Name = "LabelText",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Colour = withFixedAlpha(LabelColour.Value),
                        },
                        // Value text (bottom line)
                        new OsuSpriteText
                        {
                            Name = "ValueText",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Colour = withFixedAlpha(LabelColour.Value),
                        },
                    },
                };
            }

            axisLabelContainer.Clear();

            foreach (var container in axisLabelContainers)
                axisLabelContainer.Add(container);
        }

        private void updateAxisTexts()
        {
            if (axisLabelContainers.Length == 0 || chart == null || axisLabelContainer == null)
                return;

            axisLabelContainer.Size = chart.Size + new Vector2(axis_label_padding * 2);

            float radius = Math.Min(chart.Size.X, chart.Size.Y) * 0.5f * chart.RadiusRatio;
            int axisCount = Math.Max(3, chart.AxisCount);

            for (int i = 0; i < axisCount && i < axisLabelContainers.Length; i++)
            {
                float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Vector2 position = direction * (radius + axis_label_offset);

                // Position the container at the calculated position
                axisLabelContainers[i].Position = position;

                // Update text content
                var labelText = axisLabelContainers[i].OfType<OsuSpriteText>().FirstOrDefault(t => t.Name == "LabelText");
                var valueText = axisLabelContainers[i].OfType<OsuSpriteText>().FirstOrDefault(t => t.Name == "ValueText");

                labelText?.Text = getAxisLabel(i);

                valueText?.Text = formatAxisValue(i);
            }
        }

        private string getAxisLabel(int index)
        {
            return index < activeAxisLabels.Length ? activeAxisLabels[index] : $"P{index + 1}";
        }

        private string formatAxisValue(int index)
        {
            if (index >= parameterValues.Length)
                return "0";

            float value = parameterValues[index];
            string format = index < activeAxisFormats.Length ? activeAxisFormats[index] : "0.0";
            return value.ToString(string.IsNullOrEmpty(format) ? "0.0" : format);
        }

        private static Color4 withFixedAlpha(Color4 colour) => new Color4(colour.R, colour.G, colour.B, axis_label_alpha);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource?.Dispose();
                cancelRadarAnalysis();
                modSettingTracker?.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
