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

        /// <summary>Last live result the analysis cache handed this panel, keyed by chart + ruleset + mods.</summary>
        private EzAnalysisResult? cachedLiveAnalysis;

        private EzRadarComputeKey? cachedLiveAnalysisKey;

        /// <summary>Last chart + mods a live pass was kicked for; blocks duplicate kicks.</summary>
        private EzRadarComputeKey? lastLiveAnalysisKickKey;

        private static readonly IReadOnlyDictionary<string, double> no_skill_values
            = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Identity of one analysis request. The display mode is deliberately absent: every mode reads the same
        /// result, and switching modes must not invalidate or cancel a pass that is already running.
        /// </summary>
        private readonly record struct EzRadarComputeKey(string Hash, int RulesetId, int ModsSignature);

        /// <summary>One declared radar spoke. <see cref="Chart"/> / <see cref="Player"/> are radii, not axis identity.</summary>
        private readonly record struct SkillSpoke(string Label, double Chart, double Player);

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
                if (isSkillRadarMode())
                    updateSkillRadarPresentation();
            });

            chart?.SetData(parameterRatios);
            updateAxisTexts();

            beatmap.BindValueChanged(b =>
            {
                cachedLiveAnalysis = null;
                cachedLiveAnalysisKey = null;
                resetRadarKickGuards();
                cancelRadarAnalysis();
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
                cancelRadarAnalysis();
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
                cancelRadarAnalysis();
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
            if (RadarDisplayMode.Value == EzRadarDisplayMode.Metadate)
            {
                chart?.ClearSecondaryData();
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }
            else if (isSkillRadarMode())
                updateSkillRadarPresentation();
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
            if (isSkillRadarMode())
            {
                updateSkillRadarPresentation();
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
            if (RadarDisplayMode.Value == EzRadarDisplayMode.Metadate || isSkillRadarMode())
                return;

            if (beatmap.Value.BeatmapInfo is not BeatmapInfo beatmapInfo || !EzAnalysisProviderBridge.HasAnalysisProvider(ruleset.Value))
            {
                // No analysis provider for this ruleset: keep the general star/CS/OD radar (pre-existing fallback).
                cancelRadarAnalysis();
                chart?.ClearSecondaryData();
                applyRadarData(createGeneralRadarData(difficultyBindable?.Value ?? default), getGeneralAxisMaxValues());
                return;
            }

            requestLiveAnalysis(beatmapInfo);

            if (getLiveAnalysis(beatmapInfo) is { Radar: { } radar })
            {
                applyRulesetSpecificRadarResult(radar);
                return;
            }

            // Nothing for this chart + mods yet: paint nothing rather than the general star radar, so a
            // placeholder -> real star rebind does not visibly swap axis sets twice.
            clearChartData();
            chart?.ClearSecondaryData();
        }

        /// <summary>
        /// Chart-side PP+ vector from the analysis result, re-keyed to the stored skill ids this panel draws with.
        /// </summary>
        private static IReadOnlyDictionary<string, double> pppChartSkillsOf(EzAnalysisResult? analysis)
        {
            if (analysis is not { } live)
                return no_skill_values;

            var axes = live.AxesOf(EzMetricProducers.PPP_CHART);

            if (axes.Count == 0)
                return no_skill_values;

            var bySkillId = new Dictionary<string, double>(axes.Count, StringComparer.Ordinal);

            foreach (var (axisId, value) in axes)
                bySkillId[EzPPPSkillCatalog.ChartSkillId(axisId)] = value;

            return bySkillId;
        }

        /// <summary>
        /// The analysis result for this chart + mods, from the analysis cache only. A miss is "not computed yet",
        /// never "read the stored rows instead".
        /// </summary>
        private EzAnalysisResult? getLiveAnalysis(BeatmapInfo beatmapInfo)
        {
            if (analysisCache?.TryGet(beatmapInfo, mods.Value) is { } shared)
                return shared;

            if (cachedLiveAnalysis is not { } local || cachedLiveAnalysisKey is not { } key)
                return null;

            return key == createAnalysisKey(beatmapInfo, ruleset.Value, mods.Value) ? local : null;
        }

        /// <summary>
        /// Asks the analysis system for the current chart + mods and repaints when the result lands. Independent of
        /// <see cref="Ez2Setting.EzAnalysisRecEnabled"/>: the radar must reflect the selected mods.
        /// </summary>
        private void requestLiveAnalysis(BeatmapInfo beatmapInfo)
        {
            if (analysisCache == null)
                return;

            if (getLiveAnalysis(beatmapInfo) != null)
                return;

            var key = createAnalysisKey(beatmapInfo, ruleset.Value, mods.Value);

            if (lastLiveAnalysisKickKey == key)
                return;

            radarAnalysisCancellationSource ??= new CancellationTokenSource();
            CancellationToken cancellationToken = radarAnalysisCancellationSource.Token;

            lastLiveAnalysisKickKey = key;

            RulesetInfo localRuleset = ruleset.Value;
            IReadOnlyList<Mod> localMods = mods.Value;

            // The continuation is not registered against the pass' own token: a superseded pass that still completes
            // has warmed the shared store, and dropping its callback is what left the radar permanently empty.
            analysisCache.GetLiveAnalysisAsync(beatmapInfo, localRuleset, localMods, cancellationToken)
                         .ContinueWith(task =>
                         {
                             Schedule(() =>
                             {
                                 EzAnalysisResult? result = task.IsCanceled || task.IsFaulted ? null : task.GetResultSafely();

                                 if (result != null && !cancellationToken.IsCancellationRequested)
                                 {
                                     cachedLiveAnalysisKey = key;
                                     cachedLiveAnalysis = result;
                                 }

                                 // A failed or superseded pass must not block a retry on the next presentation.
                                 if (lastLiveAnalysisKickKey == key)
                                     lastLiveAnalysisKickKey = null;

                                 if (!cancellationToken.IsCancellationRequested)
                                     refreshRadarPresentation();
                             });
                         }, CancellationToken.None);
        }

        private void updateSkillRadarPresentation()
        {
            var beatmapInfo = beatmap.Value.BeatmapInfo;

            if (beatmapInfo == null)
            {
                clearSkillRadar();
                return;
            }

            // Skill modes name the vector the analysis system declared, not the layer it was read from. PP+ is the
            // declared default everywhere except mania, whose own rating engine owns the MSD modes.
            if (isPppSkillRadarMode() || beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
            {
                updatePppSkillRadarPresentation(beatmapInfo);
                return;
            }

            updateMinaSkillRadarPresentation(beatmapInfo);
        }

        private void updateMinaSkillRadarPresentation(BeatmapInfo beatmapInfo)
        {
            requestLiveAnalysis(beatmapInfo);

            var live = getLiveAnalysis(beatmapInfo);
            var msd = live is { } analysed ? analysed.AxesOf(EzMetricProducers.MINA_CHART) : no_skill_values;
            var structureAxes = live is { } structured ? structureAxesOf(structured) : no_skill_values;

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);

            EzPersistedChartDan? persistedChartDan = null;

            skillProvider?.TryGetPersistedChartDan(beatmapInfo, out persistedChartDan);

            if (keyCount <= 0 && persistedChartDan is { KeyCount: > 0 })
                keyCount = persistedChartDan.KeyCount;

            double holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            int holdCount = EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);

            // Decided before the values land: the long-note frame has its own axis set, and painting the RC frame
            // first would visibly morph into it.
            if (expectsLnSkillRadar(keyCount, persistedChartDan, holdRatio, holdCount))
            {
                applyLnSkillRadar(EzLnSkillRadar.BuildAxes(holdRatio, structureAxes, keyCount));
                return;
            }

            string? username = TargetUsername.Value;
            IReadOnlyDictionary<string, double> ssr = no_skill_values;

            if (!string.IsNullOrWhiteSpace(username) && keyCount > 0 && skillProvider != null)
                ssr = skillProvider.GetPlayerSsrSnapshot(username, keyCount).Values;

            bool beatmapOnly = RadarDisplayMode.Value == EzRadarDisplayMode.MSDSkillChart;
            var spokes = new List<SkillSpoke>(skill_radar_axes.Length);

            foreach (var axis in skill_radar_axes)
            {
                spokes.Add(new SkillSpoke(
                    skillAxisDisplayName(axis),
                    displayValue(msd.GetValueOrDefault(axis.ToId(), 0)),
                    displayValue(ssr.GetValueOrDefault(axis.ToSsrSkillId(), 0))));
            }

            applySkillSpokes(spokes, beatmapOnly);
        }

        /// <summary>
        /// The generic n-key engine returns a ~0.18 sliver for skillsets it does not rate, which is noise next to a
        /// real bar. The spoke stays; only its value reads as absent.
        /// </summary>
        private static double displayValue(double value)
            => double.IsFinite(value) && value >= EzPatternRatings.DISPLAY_MIN ? value : 0;

        private static IReadOnlyDictionary<string, double> structureAxesOf(in EzAnalysisResult analysis)
            => analysis.AxesOf(EzMetricProducers.CSI_CHART);

        /// <summary>
        /// Synchronous LN/RC pre-judgement. Realm-persisted ChartDan with an LN half is authoritative
        /// (written through the same hold gate); otherwise fall back to that gate using the MSD hold ratio
        /// and the stored hold-object count.
        /// </summary>
        private static bool expectsLnSkillRadar(int keyCount, EzPersistedChartDan? persistedChartDan, double holdRatio, int holdCount)
        {
            if (keyCount <= 0)
                return false;

            if (persistedChartDan != null && persistedChartDan.HasSide(EzDanSide.Ln))
                return true;

            return EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount);
        }

        /// <summary>
        /// Paints one skill frame. The spokes are fixed by the analysis system's declaration, so an absent value
        /// draws at the centre instead of removing its spoke and rotating every other one.
        /// </summary>
        private void applySkillSpokes(IReadOnlyList<SkillSpoke> spokes, bool beatmapOnly)
        {
            AxisCount = Math.Max(3, spokes.Count);

            activeAxisLabels = Enumerable.Repeat(string.Empty, AxisCount).ToArray();
            activeAxisFormats = Enumerable.Repeat("0.00", AxisCount).ToArray();

            double maxValue = 0;
            var chartValues = new double[AxisCount];
            var playerValues = new double[AxisCount];

            for (int i = 0; i < spokes.Count; i++)
            {
                SkillSpoke spoke = spokes[i];

                double chartValue = nonNegative(spoke.Chart);
                double playerValue = beatmapOnly ? 0 : nonNegative(spoke.Player);

                activeAxisLabels[i] = spoke.Label;
                chartValues[i] = chartValue;
                playerValues[i] = playerValue;
                parameterValues[i] = (float)chartValue;
                maxValue = Math.Max(maxValue, Math.Max(chartValue, playerValue));
            }

            if (maxValue <= 0)
                maxValue = 1;

            var beatmapRatios = new float[AxisCount];
            var playerRatios = new float[AxisCount];

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

        private static double nonNegative(double value)
            => double.IsFinite(value) && value > 0 ? value : 0;

        /// <summary>No axis set at all: draws nothing and leaves no stale labels behind.</summary>
        private void clearSkillRadar()
        {
            int slots = Math.Max(3, AxisCount);

            activeAxisLabels = Enumerable.Repeat(string.Empty, slots).ToArray();
            activeAxisFormats = Enumerable.Repeat("0.00", slots).ToArray();
            clearChartData();
        }

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

        private bool isSkillRadarMode()
            => RadarDisplayMode.Value is EzRadarDisplayMode.MSDSkillDual or EzRadarDisplayMode.MSDSkillChart
                or EzRadarDisplayMode.PPPSkillDual or EzRadarDisplayMode.PPPSkillChart;

        private bool isPppSkillRadarMode()
            => RadarDisplayMode.Value is EzRadarDisplayMode.PPPSkillDual or EzRadarDisplayMode.PPPSkillChart;

        private void updatePppSkillRadarPresentation(BeatmapInfo beatmapInfo)
        {
            if (!EzPPPSkillCatalog.SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
            {
                clearSkillRadar();
                return;
            }

            requestLiveAnalysis(beatmapInfo);

            int rulesetId = beatmapInfo.Ruleset.OnlineID;
            var chartSkills = pppChartSkillsOf(getLiveAnalysis(beatmapInfo));

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            int slice = EzPPPSkillCatalog.PlayerSliceKey(rulesetId, keyCount);
            string? username = TargetUsername.Value;
            IReadOnlyDictionary<string, double> playerSkills = no_skill_values;

            if (!string.IsNullOrWhiteSpace(username) && skillProvider != null)
                playerSkills = skillProvider.GetPlayerSystemSkills(username, slice, EzSkillSystems.PLAYER_PPP, rulesetId);

            bool beatmapOnly = RadarDisplayMode.Value == EzRadarDisplayMode.PPPSkillChart;
            var axes = EzPPPSkillCatalog.RadarAxes(rulesetId);
            var spokes = new List<SkillSpoke>(axes.Count);

            foreach (var axis in axes)
            {
                // A blank slot is part of the ruleset's declared shape, not a value that failed to load.
                if (axis.MetricId.StartsWith("_pad", StringComparison.Ordinal))
                {
                    spokes.Add(new SkillSpoke(string.Empty, 0, 0));
                    continue;
                }

                spokes.Add(new SkillSpoke(
                    axis.DisplayName.ToString(),
                    nonNegative(chartSkills.GetValueOrDefault(axis.ChartSkillId, 0)),
                    nonNegative(playerSkills.GetValueOrDefault(axis.PlayerSkillId, 0))));
            }

            applySkillSpokes(spokes, beatmapOnly);
        }

        private static readonly EzMinaSkillAxis[] skill_radar_axes = EzMinaSkillAxisExtensions.RadarAxes;

        private static string skillAxisDisplayName(EzMinaSkillAxis axis)
            => axis.Chip().Name.ToString();

        private static EzRadarComputeKey createAnalysisKey(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod>? mods)
            => new EzRadarComputeKey(beatmapInfo.Hash, ruleset.OnlineID, EzAnalysisLookupCache.ComputeModsSignature(mods));
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
