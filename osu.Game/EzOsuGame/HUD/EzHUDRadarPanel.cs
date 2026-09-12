// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Screens;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using Triangle = osu.Framework.Graphics.Primitives.Triangle;

namespace osu.Game.EzOsuGame.HUD
{
    public enum EzRadarDisplayMode
    {
        [Description("Metadate")]
        Metadate,

        [Description("Key Pattern")]
        KeyPattern,

        [Description("XxySR Pattern")]
        XxySrPattern,

        [Description("Skill")]
        Skill
    }

    /// <summary>
    /// 雷达图面板，显示当前谱面参数的六边形图形化表示
    /// </summary>
    public partial class EzHUDRadarPanel : CompositeDrawable, ISerialisableDrawable
    {
        // Layout constants
        private const float chart_size = 144f;
        private const float corner_radius = ScreenFooterButton.CORNER_RADIUS;
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
        public BindableColour4 BaseLineColour { get; } = new BindableColour4(new Color4(255, 255, 210, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BaseAreaColour { get; } = new BindableColour4(new Color4(255, 255, 200, 128));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataLineColour { get; } = new BindableColour4(new Color4(255, 230, 128, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataAreaColour { get; } = new BindableColour4(new Color4(255, 215, 0, 128));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_PLAYER_DATA_LINE_COLOUR), nameof(EzHUDStrings.RADAR_PLAYER_DATA_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 PlayerDataLineColour { get; } = new BindableColour4(new Color4(80, 220, 120, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_PLAYER_DATA_AREA_COLOUR), nameof(EzHUDStrings.RADAR_PLAYER_DATA_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 PlayerDataAreaColour { get; } = new BindableColour4(new Color4(80, 220, 120, 128));

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

        private IBindable<StarDifficulty>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private CancellationTokenSource? radarAnalysisCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;

        private Container? axisLabelContainer;
        private RadarChart? chart;
        private Box background = null!;
        private FillFlowContainer[] axisLabelContainers = Array.Empty<FillFlowContainer>();
        private string[] activeAxisLabels = default_axis_labels;
        private string[] activeAxisFormats = default_axis_formats;
        private EzRulesetSpecificRadarResult? cachedRulesetSpecificRadarResult;

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
                    CornerRadius = corner_radius,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BackgroundColour.Value,
                        },
                    },
                },
                chart = new RadarChart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
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
                if (RadarDisplayMode.Value == EzRadarDisplayMode.Skill)
                    updateSkillRadarPresentation();
            });

            chart?.SetData(parameterRatios);
            updateAxisTexts();

            beatmap.BindValueChanged(b =>
            {
                cachedRulesetSpecificRadarResult = null;
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
                cachedRulesetSpecificRadarResult = null;
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => updateParameterRatios(difficultyBindable?.Value ?? default)
                };
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }, true);

            ruleset.BindValueChanged(_ =>
            {
                cachedRulesetSpecificRadarResult = null;
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
            else if (RadarDisplayMode.Value == EzRadarDisplayMode.Skill)
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
            if (RadarDisplayMode.Value == EzRadarDisplayMode.Skill)
            {
                updateSkillRadarPresentation();
                return;
            }

            if (RadarDisplayMode.Value != EzRadarDisplayMode.Metadate && beginRulesetSpecificRadarUpdate())
                return;

            cancelRadarAnalysis();
            chart?.ClearSecondaryData();
            applyRadarData(createGeneralRadarData(difficulty), getGeneralAxisMaxValues());
        }

        private void updateRulesetSpecificRadarPresentation()
        {
            if (RadarDisplayMode.Value is EzRadarDisplayMode.Metadate or EzRadarDisplayMode.Skill)
                return;

            if (cachedRulesetSpecificRadarResult is EzRulesetSpecificRadarResult cachedResult)
                applyRulesetSpecificRadarResult(cachedResult);
            else
                updateParameterRatios(difficultyBindable?.Value ?? default);
        }

        private void updateSkillRadarPresentation()
        {
            cancelRadarAnalysis();

            var beatmapInfo = beatmap.Value.BeatmapInfo;

            if (beatmapInfo == null || beatmapInfo.Ruleset.OnlineID != 3)
            {
                clearChartData();
                chart?.ClearSecondaryData();
                return;
            }

            // Key locked to current beatmap CS (same-beatmap ChartDan only if CS missing).
            // Live mods may override after async compute.
            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);

            if (keyCount <= 0
                && skillProvider != null
                && skillProvider.TryGetPersistedChartDan(beatmapInfo, out var chartDan)
                && chartDan is { KeyCount: > 0 })
            {
                keyCount = chartDan.KeyCount;
            }

            string? username = TargetUsername.Value;
            IReadOnlyList<Mod> localMods = mods.Value;

            // Instant: Realm MSD (xxy L1).
            IReadOnlyDictionary<string, double> msd = skillProvider?.GetBeatmapMsd(beatmapInfo.Hash)
                                                      ?? new Dictionary<string, double>();

            applySkillRadarLayers(keyCount, username, msd);

            // Always live-recompute selected chart MSD (xxySR analysis rhythm), including nomod.
            if (skillProvider == null)
                return;

            var provider = skillProvider;
            radarAnalysisCancellationSource = new CancellationTokenSource();
            CancellationToken token = radarAnalysisCancellationSource.Token;

            Task.Factory.StartNew(() => provider.TryComputeLiveChartSkills(beatmapInfo, localMods), token,
                    TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (token.IsCancellationRequested)
                            return;

                        var snap = task.GetResultSafely();
                        if (snap == null || snap.Msd.Count == 0)
                            return;

                        applySkillRadarLayers(snap.KeyCount > 0 ? snap.KeyCount : keyCount, TargetUsername.Value, snap.Msd);
                    });
                }, token);
        }

        private void applySkillRadarLayers(int keyCount, string? username, IReadOnlyDictionary<string, double> msd)
        {
            // RC skill (6/7/8K): fixed pattern Meta axes — independent of whether the player has ratings.
            // LN skill radar axes are deferred (separate follow-up).
            if (keyCount > 0 && EzDanSkillsetFiling.UsesPatternSkillAxes(keyCount))
            {
                IReadOnlyList<EzPatternRating> patterns = Array.Empty<EzPatternRating>();

                if (!string.IsNullOrWhiteSpace(username) && skillProvider != null)
                    patterns = skillProvider.GetPlayerPatternRatings(username, keyCount);

                var modeEntries = EzPatternRatings.FixedPatternRadarEntries(patterns);
                AxisCount = modeEntries.Count;
                activeAxisLabels = modeEntries.Select(static e => e.DisplayName.ToString()).ToArray();
                activeAxisFormats = Enumerable.Repeat("0.00", modeEntries.Count).ToArray();

                double max = modeEntries.Max(static e => e.Value);
                if (max <= 0)
                    max = 1;

                float[] beatmapRatios = new float[modeEntries.Count];
                float[] playerRatios = new float[modeEntries.Count];

                for (int i = 0; i < modeEntries.Count; i++)
                {
                    double playerValue = modeEntries[i].Value;
                    // Axis labels show player RC skill floats; chart (yellow) stays empty until CSI layer lands.
                    parameterValues[i] = (float)playerValue;
                    playerRatios[i] = (float)(playerValue / max);
                }

                chart?.SetData(beatmapRatios);
                chart?.SetSecondaryData(playerRatios);
                updateAxisTexts();
                applyChartColours();
                return;
            }

            var axes = skill_radar_axes;
            AxisCount = axes.Length;
            activeAxisLabels = axes.Select(skillAxisDisplayName).ToArray();
            activeAxisFormats = Enumerable.Repeat("0.00", axes.Length).ToArray();

            IReadOnlyDictionary<string, double> ssr = new Dictionary<string, double>();

            if (!string.IsNullOrWhiteSpace(username) && keyCount > 0 && skillProvider != null)
                ssr = skillProvider.GetPlayerSsrSnapshot(username, keyCount).Values;

            double maxMina = 0;

            for (int i = 0; i < axes.Length; i++)
            {
                var axis = axes[i];
                double beatmapValue = msd.GetValueOrDefault(axis.ToMsdSkillId(), 0);
                double playerValue = ssr.GetValueOrDefault(axis.ToSsrSkillId(), 0);
                if (playerValue < EzPatternRatings.DISPLAY_MIN)
                    playerValue = 0;
                parameterValues[i] = (float)beatmapValue;
                maxMina = Math.Max(maxMina, Math.Max(beatmapValue, playerValue));
            }

            if (maxMina <= 0)
                maxMina = 1;

            float[] playerRatiosMina = new float[axes.Length];

            for (int i = 0; i < axes.Length; i++)
            {
                var axis = axes[i];
                parameterRatios[i] = (float)(parameterValues[i] / maxMina);
                double playerValue = ssr.GetValueOrDefault(axis.ToSsrSkillId(), 0);
                if (playerValue < EzPatternRatings.DISPLAY_MIN)
                    playerValue = 0;
                playerRatiosMina[i] = (float)(playerValue / maxMina);
            }

            chart?.SetData(parameterRatios);
            chart?.SetSecondaryData(playerRatiosMina);
            updateAxisTexts();
            applyChartColours();
        }

        private static readonly EzMinaSkillAxis[] skill_radar_axes = EzMinaSkillAxisExtensions.RadarAxes;

        private static string skillAxisDisplayName(EzMinaSkillAxis axis)
            => axis.Chip().Name.ToString();

        private void cancelRadarAnalysis()
        {
            radarAnalysisCancellationSource?.Cancel();
            radarAnalysisCancellationSource?.Dispose();
            radarAnalysisCancellationSource = null;
        }

        private bool beginRulesetSpecificRadarUpdate()
        {
            if (!EzAnalysisProviderBridge.HasAnalysisProvider(ruleset.Value))
                return false;

            cancelRadarAnalysis();

            if (beatmap.Value.BeatmapInfo is not BeatmapInfo beatmapInfo)
            {
                clearChartData();
                return true;
            }

            clearChartData();

            radarAnalysisCancellationSource = new CancellationTokenSource();

            CancellationToken cancellationToken = radarAnalysisCancellationSource.Token;
            WorkingBeatmap workingBeatmap = beatmap.Value;
            RulesetInfo localRuleset = ruleset.Value;
            IReadOnlyList<Mod> localMods = mods.Value;

            Task.Factory.StartNew(() => computeRulesetSpecificRadarData(workingBeatmap, beatmapInfo, localRuleset, localMods, cancellationToken), cancellationToken,
                    TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        EzRulesetSpecificRadarResult? radarResult = task.GetResultSafely();

                        if (radarResult != null)
                        {
                            cachedRulesetSpecificRadarResult = radarResult.Value;
                            applyRulesetSpecificRadarResult(radarResult.Value);
                        }
                    });
                }, cancellationToken);

            return true;
        }

        private EzRulesetSpecificRadarResult? computeRulesetSpecificRadarData(WorkingBeatmap workingBeatmap, BeatmapInfo beatmapInfo, RulesetInfo localRuleset,
                                                                              IReadOnlyList<Mod> localMods, CancellationToken cancellationToken)
        {
            try
            {
                var lookup = new EzAnalysisLookupCache(beatmapInfo, localRuleset, localMods);

                return EzAnalysisComputation.TryComputeRulesetSpecificRadarData(workingBeatmap, lookup, cancellationToken, out EzRulesetSpecificRadarResult radarResult)
                    ? radarResult
                    : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[EzComRadarPanel] Failed to compute ruleset radar for beatmapId={beatmapInfo.ID}.");
                return null;
            }
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

                activeAxisLabels[i] = EzPatternAxisExtensions.TryParse(axis.Axis, out var pattern)
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
            => RadarDisplayMode.Value == EzRadarDisplayMode.XxySrPattern
                ? new[] { MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr, MaxXxySr }
                : UseAbsoluteValue.Value
                    ? new[] { MaxXxySr, MaxXxySr, long_note_ratio_max, MaxXxySr, MaxXxySr, MaxXxySr }
                    : new[] { MaxKeyPatternScore, MaxKeyPatternScore, long_note_ratio_max, MaxKeyPatternScore, MaxKeyPatternScore, MaxKeyPatternScore };

        private EzRadarChartData<string> createDisplayedRulesetRadarData(EzRulesetSpecificRadarResult radarResult)
        {
            EzRadarChartData<string> sourceRadarData = getSelectedRulesetSpecificRadarData(radarResult);

            if (RadarDisplayMode.Value == EzRadarDisplayMode.XxySrPattern)
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
            => RadarDisplayMode.Value == EzRadarDisplayMode.XxySrPattern ? radarResult.XxySrPatternRadarData : radarResult.KeyPatternRadarData;

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
            chart.Invalidate(Invalidation.DrawNode);

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

    public partial class RadarChart : Drawable
    {
        private int axisCount = 6;
        private float[] dataRatios = new float[6];
        private float[]? secondaryDataRatios;
        private Texture? whitePixel;

        public int AxisCount
        {
            get => axisCount;
            set
            {
                int clamped = Math.Max(3, value);

                if (axisCount == clamped)
                    return;

                axisCount = clamped;
                Array.Resize(ref dataRatios, axisCount);
                if (secondaryDataRatios != null)
                    Array.Resize(ref secondaryDataRatios, axisCount);
                Invalidate(Invalidation.DrawNode);
            }
        }

        public int GridLevels { get; set; } = 4;

        public float RadiusRatio { get; set; } = 0.82f;

        public float GridThickness { get; set; } = 1.5f;

        public float AxisThickness { get; set; } = 1.5f;

        public float DataOutlineThickness { get; set; } = 2.2f;

        public float DataPointSize { get; set; } = 5f;

        public Color4 GridColour { get; set; } = new Color4(255, 255, 210, 110);

        public Color4 AxisColour { get; set; } = new Color4(255, 255, 210, 95);

        public Color4 BaseFillColour { get; set; } = new Color4(255, 255, 200, 30);

        public Color4 DataFillColour { get; set; } = new Color4(255, 215, 0, 95);

        public Color4 DataStrokeColour { get; set; } = new Color4(255, 230, 128, 230);

        public Color4 DataPointColour { get; set; } = new Color4(255, 242, 176, 255);

        public Color4 SecondaryDataFillColour { get; set; } = new Color4(80, 220, 120, 95);

        public Color4 SecondaryDataStrokeColour { get; set; } = new Color4(80, 220, 120, 230);

        public Color4 SecondaryDataPointColour { get; set; } = new Color4(120, 240, 160, 255);

        public RadarChart()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            whitePixel = createWhitePixelTexture(renderer);
        }

        private static Texture createWhitePixelTexture(IRenderer renderer)
        {
            var texture = renderer.CreateTexture(1, 1, true);
            var image = new Image<Rgba32>(1, 1);
            image[0, 0] = new Rgba32(255, 255, 255, 255);
            texture.SetData(new TextureUpload(image));
            return texture;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                whitePixel?.Dispose();

            base.Dispose(isDisposing);
        }

        public void SetData(IReadOnlyList<float> ratios)
        {
            for (int i = 0; i < axisCount; i++)
                dataRatios[i] = i < ratios.Count ? Math.Clamp(ratios[i], 0, 1) : 0;

            Invalidate(Invalidation.DrawNode);
        }

        public void SetSecondaryData(IReadOnlyList<float> ratios)
        {
            secondaryDataRatios ??= new float[axisCount];
            if (secondaryDataRatios.Length != axisCount)
                Array.Resize(ref secondaryDataRatios, axisCount);

            for (int i = 0; i < axisCount; i++)
                secondaryDataRatios[i] = i < ratios.Count ? Math.Clamp(ratios[i], 0, 1) : 0;

            Invalidate(Invalidation.DrawNode);
        }

        public void ClearSecondaryData()
        {
            secondaryDataRatios = null;
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new RadarChartDrawNode(this);

        private class RadarChartDrawNode : DrawNode
        {
            private readonly RadarChart source;

            private float[] ratios = Array.Empty<float>();
            private float[]? secondaryRatios;
            private int axisCount;

            private int gridLevels;
            private float radiusRatio;
            private float gridThickness;
            private float axisThickness;
            private float dataOutlineThickness;
            private float dataPointSize;

            private Color4 gridColour;
            private Color4 axisColour;
            private Color4 baseFillColour;
            private Color4 dataFillColour;
            private Color4 dataStrokeColour;
            private Color4 dataPointColour;
            private Color4 secondaryDataFillColour;
            private Color4 secondaryDataStrokeColour;
            private Color4 secondaryDataPointColour;

            private Vector2 drawSize;
            private Texture? texture;

            // Reused across Draw frames — never allocate Vector2[] per frame.
            private Vector2[] outerVertices = Array.Empty<Vector2>();
            private Vector2[] levelVertices = Array.Empty<Vector2>();
            private Vector2[] primaryVertices = Array.Empty<Vector2>();
            private Vector2[] secondaryVertices = Array.Empty<Vector2>();

            public RadarChartDrawNode(RadarChart chart)
                : base(chart)
            {
                source = chart;
            }

            public override void ApplyState()
            {
                base.ApplyState();

                texture = source.whitePixel;

                drawSize = source.DrawSize;
                axisCount = source.AxisCount;

                if (ratios.Length != axisCount)
                    Array.Resize(ref ratios, axisCount);

                ensureVertexCapacity(ref outerVertices, axisCount);
                ensureVertexCapacity(ref levelVertices, axisCount);
                ensureVertexCapacity(ref primaryVertices, axisCount);
                ensureVertexCapacity(ref secondaryVertices, axisCount);

                gridLevels = Math.Max(1, source.GridLevels);
                radiusRatio = Math.Clamp(source.RadiusRatio, 0.1f, 1);
                gridThickness = Math.Max(0.5f, source.GridThickness);
                axisThickness = Math.Max(0.5f, source.AxisThickness);
                dataOutlineThickness = Math.Max(0.5f, source.DataOutlineThickness);
                dataPointSize = Math.Max(1, source.DataPointSize);

                gridColour = source.GridColour;
                axisColour = source.AxisColour;
                baseFillColour = source.BaseFillColour;
                dataFillColour = source.DataFillColour;
                dataStrokeColour = source.DataStrokeColour;
                dataPointColour = source.DataPointColour;
                secondaryDataFillColour = source.SecondaryDataFillColour;
                secondaryDataStrokeColour = source.SecondaryDataStrokeColour;
                secondaryDataPointColour = source.SecondaryDataPointColour;

                for (int i = 0; i < axisCount; i++)
                    ratios[i] = source.dataRatios[i];

                if (source.secondaryDataRatios == null)
                {
                    secondaryRatios = null;
                }
                else
                {
                    secondaryRatios ??= new float[axisCount];
                    if (secondaryRatios.Length != axisCount)
                        Array.Resize(ref secondaryRatios, axisCount);

                    for (int i = 0; i < axisCount; i++)
                        secondaryRatios[i] = source.secondaryDataRatios[i];
                }
            }

            protected override void Draw(IRenderer renderer)
            {
                if (texture == null)
                    return;

                float radius = Math.Min(drawSize.X, drawSize.Y) * 0.5f * radiusRatio;
                if (radius <= 0)
                    return;

                Vector2 center = drawSize * 0.5f;

                renderer.PushLocalMatrix(DrawInfo.Matrix);

                fillVerticesUniform(outerVertices, center, radius, 1);
                drawPolygonFill(renderer, outerVertices, baseFillColour);

                for (int level = 1; level <= gridLevels; level++)
                {
                    float ratio = level / (float)gridLevels;
                    fillVerticesUniform(levelVertices, center, radius, ratio);
                    drawPolygonOutline(renderer, levelVertices, gridColour, gridThickness);
                }

                for (int i = 0; i < axisCount; i++)
                    drawLine(renderer, center, outerVertices[i], axisColour, axisThickness);

                fillVerticesFromRatios(primaryVertices, center, radius, ratios);

                if (secondaryRatios == null)
                {
                    drawFanFill(renderer, center, primaryVertices, dataFillColour);
                    drawPolygonOutline(renderer, primaryVertices, dataStrokeColour, dataOutlineThickness);
                    drawPoints(renderer, primaryVertices, dataPointColour, dataPointSize);
                }
                else
                {
                    fillVerticesFromRatios(secondaryVertices, center, radius, secondaryRatios);

                    // Smaller coverage draws above so both polygons stay readable when nested.
                    bool primaryIsSmaller = averageRatio(ratios) <= averageRatio(secondaryRatios);

                    if (primaryIsSmaller)
                    {
                        drawFanFill(renderer, center, secondaryVertices, secondaryDataFillColour);
                        drawPolygonOutline(renderer, secondaryVertices, secondaryDataStrokeColour, dataOutlineThickness);
                        drawFanFill(renderer, center, primaryVertices, dataFillColour);
                        drawPolygonOutline(renderer, primaryVertices, dataStrokeColour, dataOutlineThickness);
                    }
                    else
                    {
                        drawFanFill(renderer, center, primaryVertices, dataFillColour);
                        drawPolygonOutline(renderer, primaryVertices, dataStrokeColour, dataOutlineThickness);
                        drawFanFill(renderer, center, secondaryVertices, secondaryDataFillColour);
                        drawPolygonOutline(renderer, secondaryVertices, secondaryDataStrokeColour, dataOutlineThickness);
                    }

                    // Endpoint nodes always on top of both fills/strokes.
                    drawPoints(renderer, primaryVertices, dataPointColour, dataPointSize);
                    drawPoints(renderer, secondaryVertices, secondaryDataPointColour, dataPointSize);
                }

                renderer.PopLocalMatrix();
            }

            private static float averageRatio(IReadOnlyList<float> values)
            {
                if (values.Count == 0)
                    return 0;

                float sum = 0;

                for (int i = 0; i < values.Count; i++)
                    sum += Math.Clamp(values[i], 0, 1);

                return sum / values.Count;
            }

            private static void ensureVertexCapacity(ref Vector2[] buffer, int count)
            {
                if (buffer.Length != count)
                    Array.Resize(ref buffer, count);
            }

            private void fillVerticesUniform(Vector2[] vertices, Vector2 center, float radius, float ratio)
            {
                for (int i = 0; i < axisCount; i++)
                {
                    float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                    vertices[i] = new Vector2(
                        center.X + radius * ratio * (float)Math.Cos(angle),
                        center.Y + radius * ratio * (float)Math.Sin(angle)
                    );
                }
            }

            private void fillVerticesFromRatios(Vector2[] vertices, Vector2 center, float radius, IReadOnlyList<float> axisRatios)
            {
                for (int i = 0; i < axisCount; i++)
                {
                    float clampedRatio = Math.Clamp(axisRatios[i], 0, 1);
                    float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                    vertices[i] = new Vector2(
                        center.X + radius * clampedRatio * (float)Math.Cos(angle),
                        center.Y + radius * clampedRatio * (float)Math.Sin(angle)
                    );
                }
            }

            private void drawPolygonFill(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour)
            {
                for (int i = 1; i < polygonVertices.Count - 1; i++)
                {
                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            polygonVertices[0],
                            polygonVertices[i],
                            polygonVertices[i + 1]),
                        colour);
                }
            }

            private void drawFanFill(IRenderer renderer, Vector2 center, IReadOnlyList<Vector2> polygonVertices, Color4 colour)
            {
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            center,
                            polygonVertices[i],
                            polygonVertices[(i + 1) % polygonVertices.Count]),
                        colour);
                }
            }

            private void drawPolygonOutline(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour, float thickness)
            {
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector2 start = polygonVertices[i];
                    Vector2 end = polygonVertices[(i + 1) % polygonVertices.Count];
                    drawLine(renderer, start, end, colour, thickness);
                }
            }

            private void drawPoints(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour, float pointSize)
            {
                float half = pointSize * 0.5f;

                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector2 point = polygonVertices[i];

                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            new Vector2(point.X - half, point.Y - half),
                            new Vector2(point.X + half, point.Y - half),
                            new Vector2(point.X + half, point.Y + half)),
                        colour);

                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            new Vector2(point.X - half, point.Y - half),
                            new Vector2(point.X + half, point.Y + half),
                            new Vector2(point.X - half, point.Y + half)),
                        colour);
                }
            }

            private void drawLine(IRenderer renderer, Vector2 start, Vector2 end, Color4 colour, float thickness)
            {
                Vector2 direction = end - start;
                if (direction.LengthSquared <= float.Epsilon)
                    return;

                direction.Normalize();
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * (thickness * 0.5f);

                renderer.DrawQuad(
                    texture!,
                    new Quad(
                        start - perpendicular,
                        start + perpendicular,
                        end - perpendicular,
                        end + perpendicular),
                    colour);
            }
        }
    }
}
