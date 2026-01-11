// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.UserInterface;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class PanelBeatmap : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT;

        private const int mania_ui_update_throttle_ms = 15;

        private StarCounter starCounter = null!;
        private ConstrainedIconContainer difficultyIcon = null!;
        private OsuSpriteText keyCountText = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private Box backgroundBorder = null!;
        private Box backgroundDifficultyTint = null!;

        private TrianglesV2 triangles = null!;

        private EzDisplayLineGraph kpsGraph = null!;
        private EzKpsDisplay kpsDisplay = null!;
        private EzKpcDisplay kpcDisplay = null!;
        private EzDisplayXxySR displayXxySR = null!;
        private Bindable<bool> xxySrFilterSetting = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private IBindable<ManiaBeatmapAnalysisResult>? maniaAnalysisBindable;
        private CancellationTokenSource? maniaAnalysisCancellationSource;
        private bool applyNextManiaUiUpdateImmediately;
        private string? cachedScratchText;

        private ScheduledDelegate? scheduledManiaUiUpdate;
        private (double averageKps, double maxKps, List<double> kpsList) pendingKpsResult;
        private Dictionary<int, int>? pendingColumnCounts;
        private Dictionary<int, int>? pendingHoldNoteCounts;
        private bool hasPendingUiUpdate;

        private Bindable<EzKpcDisplay.KpcDisplayMode> kpcDisplayMode = null!;

        private int cachedKpcKeyCount = -1;
        private Guid cachedKpcBeatmapId;
        private int cachedKpcRulesetId = -1;
        private int cachedKpcModsHash;

        private Dictionary<int, int>? normalizedColumnCounts;
        private Dictionary<int, int>? normalizedHoldNoteCounts;
        private int normalizedCountsKeyCount;

        private int lastKpcCountsHash;
        private EzKpcDisplay.KpcDisplayMode lastKpcMode;

        public PanelBeatmap()
        {
            PanelXOffset = 60;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(9f),
                Margin = new MarginPadding { Left = 2.5f, Right = 1.5f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                },
                backgroundDifficultyTint = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new TrianglesV2
                {
                    ScaleAdjust = 1.2f,
                    Thickness = 0.01f,
                    Velocity = 0.3f,
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = 6.5f },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        localRank = new PanelLocalRankDisplay
                        {
                            Scale = new Vector2(0.8f),
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = 3.5f },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Bottom = 4 },
                                    Children = new Drawable[]
                                    {
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 3f },
                                        },
                                        authorText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        kpsDisplay = new EzKpsDisplay
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
                                        Empty(),
                                        kpsGraph = new EzDisplayLineGraph
                                        {
                                            Size = new Vector2(300, 20),
                                            LineColour = Color4.CornflowerBlue.Opacity(0.8f),
                                            Blending = BlendingParameters.Mixture,
                                            Colour = ColourInfo.GradientHorizontal(Color4.White, Color4.CornflowerBlue),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        displayXxySR = new EzDisplayXxySR
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        },
                                        kpcDisplay = new EzKpcDisplay()
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                    },
                                }
                            }
                        }
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ =>
            {
                computeStarRating();
                invalidateManiaAnalysisBinding();
                applyNextManiaUiUpdateImmediately = true;

                if (Item?.IsVisible == true)
                    bindManiaAnalysis();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                computeStarRating();
                invalidateManiaAnalysisBinding();
                applyNextManiaUiUpdateImmediately = true;

                if (Item?.IsVisible == true)
                    bindManiaAnalysis();
                updateKeyCount();
            }, true);

            // 设置 XxySRFilter 设置的绑定
            xxySrFilterSetting = ezConfig.GetBindable<bool>(Ez2Setting.XxySRFilter);
            xxySrFilterSetting.BindValueChanged(value =>
            {
                // 根据 XxySRFilter 设置切换图标
                starCounter.Icon = value.NewValue
                    ? FontAwesome.Solid.Moon
                    : FontAwesome.Solid.Star;
            }, true); // true 表示立即触发一次以设置初始状态

            kpcDisplayMode = ezConfig.GetBindable<EzKpcDisplay.KpcDisplayMode>(Ez2Setting.KpcDisplayMode);
            kpcDisplayMode.BindValueChanged(mode =>
            {
                kpcDisplay.CurrentKpcDisplayMode = mode.NewValue;
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            difficultyIcon.Icon = getRulesetIcon(beatmap.Ruleset);

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            cachedScratchText = null;

            bindManiaAnalysis();
            resetManiaAnalysisDisplay();
            computeStarRating();
            updateKeyCount();
        }

        private Drawable getRulesetIcon(RulesetInfo rulesetInfo)
        {
            var rulesetInstance = rulesets.GetRuleset(rulesetInfo.ShortName)?.CreateInstance();

            if (rulesetInstance is null)
                return new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };

            return rulesetInstance.CreateIcon();
        }

        private void bindManiaAnalysis()
        {
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = null;

            if (Item == null)
                return;

            if (ruleset.Value.OnlineID != 3)
                return;

            maniaAnalysisCancellationSource = new CancellationTokenSource();
            var localCancellationSource = maniaAnalysisCancellationSource;

            // Request baseline (no xxy) first so UI can update quickly from persisted data.
            // Requesting `requireXxySr: true` would force a potentially expensive xxy calculation
            // if the persisted entry is missing xxy, which causes visible delays. We instead
            // request the heavy xxy calculation separately in the background.
            // var requestTime = System.DateTimeOffset.UtcNow;
            // Logger.Log($"[PanelBeatmap] mania analysis requested for {beatmap.OnlineID}/{beatmap.ID} requireXxy=false at {requestTime:O}", LoggingTarget.Runtime, LogLevel.Debug);
            maniaAnalysisBindable = maniaAnalysisCache.GetBindableAnalysis(beatmap, maniaAnalysisCancellationSource.Token, computationDelay: 0, requireXxySr: false);
            maniaAnalysisBindable.BindValueChanged(result =>
            {
                // var responseTime = System.DateTimeOffset.UtcNow;
                // var latency = responseTime - requestTime;
                // Logger.Log($"[PanelBeatmap] mania analysis response for {beatmap.OnlineID}/{beatmap.ID} latency={latency.TotalMilliseconds}ms xxy_present={(result.NewValue.XxySr != null)} kps_count={(result.NewValue.KpsList?.Count ?? 0)}", LoggingTarget.Runtime, LogLevel.Debug);
                // 旧 bindable 的回调（比如切换 mods / 取消重绑）直接忽略。
                if (localCancellationSource != maniaAnalysisCancellationSource)
                    return;

                // DrawablePool 回收/Item 变更期间可能仍收到旧 bindable 的回调。
                // 这时 beatmap 属性会因为 Item 为 null 而抛出异常，因此直接忽略。
                if (Item == null)
                    return;

                // The bindable starts with ManiaBeatmapAnalysisDefaults.EMPTY as a placeholder.
                // Applying it would normalize to 0..N-1 and cause the per-column notes display to flicker to zero.
                // Ignore this placeholder update and wait for a real computed/persisted result.
                if (isPlaceholderAnalysisResult(result.NewValue))
                    return;

                if (!string.IsNullOrEmpty(result.NewValue.ScratchText))
                    cachedScratchText = result.NewValue.ScratchText;

                queueManiaUiUpdate((result.NewValue.AverageKps, result.NewValue.MaxKps, result.NewValue.KpsList), result.NewValue.ColumnCounts, result.NewValue.HoldNoteCounts);

                // Xxy may be null in baseline results. Only update display if present.
                if (result.NewValue.XxySr != null)
                    displayXxySR.Current.Value = result.NewValue.XxySr;

                // If xxy is missing from the baseline, trigger an on-demand background request
                // to compute and patch xxy without blocking the main UI updates.
                if (result.NewValue.XxySr == null && maniaAnalysisCancellationSource != null && !maniaAnalysisCancellationSource.IsCancellationRequested)
                {
                    var token = maniaAnalysisCancellationSource.Token;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var full = await maniaAnalysisCache.GetAnalysisAsync(beatmap, ruleset.Value, mods.Value, token, computationDelay: 0, requireXxySr: true).ConfigureAwait(false);

                            if (full?.XxySr != null)
                            {
                                Schedule(() => displayXxySR.Current.Value = full.Value.XxySr);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // ignore
                        }
                        catch
                        {
                            // ignore failures; shouldn't block UI
                        }
                    }, token);
                }

                updateKeyCount();
            }, true);
        }

        private static bool isPlaceholderAnalysisResult(ManiaBeatmapAnalysisResult result)
            => result.AverageKps == 0
               && result.MaxKps == 0
               && (result.KpsList.Count) == 0
               && (result.ColumnCounts.Count) == 0
               && (result.HoldNoteCounts.Count) == 0
               && string.IsNullOrEmpty(result.ScratchText)
               && result.XxySr == null;

        private void queueManiaUiUpdate((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts)
        {
            if (applyNextManiaUiUpdateImmediately && Item?.IsVisible == true)
            {
                applyNextManiaUiUpdateImmediately = false;
                scheduledManiaUiUpdate?.Cancel();
                scheduledManiaUiUpdate = null;
                hasPendingUiUpdate = false;
                updateUI(result, columnCounts, holdNoteCounts);
                return;
            }

            pendingKpsResult = result;
            pendingColumnCounts = columnCounts;
            pendingHoldNoteCounts = holdNoteCounts;
            hasPendingUiUpdate = true;

            if (scheduledManiaUiUpdate != null)
                return;

            scheduledManiaUiUpdate = Scheduler.AddDelayed(() =>
            {
                scheduledManiaUiUpdate = null;

                if (!hasPendingUiUpdate)
                    return;

                hasPendingUiUpdate = false;
                updateUI(pendingKpsResult, pendingColumnCounts, pendingHoldNoteCounts);
            }, mania_ui_update_throttle_ms, false);
        }

        private void invalidateManiaAnalysisBinding()
        {
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = null;
            maniaAnalysisBindable = null;

            scheduledManiaUiUpdate?.Cancel();
            scheduledManiaUiUpdate = null;
            hasPendingUiUpdate = false;
            pendingColumnCounts = null;
            pendingHoldNoteCounts = null;
        }

        private void resetManiaAnalysisDisplay()
        {
            cachedScratchText = null;
            kpcDisplay.Clear();

            displayXxySR.Current.Value = null;

            kpsGraph.Show();
            kpsDisplay.Show();
            kpsDisplay.SetKps(0, 0);
            kpcDisplay.Show();

            if (ruleset.Value.OnlineID == 3)
            {
                displayXxySR.Show();
            }
            else
            {
                // 非 mania：隐藏 mania 专属 UI。
                displayXxySR.Hide();
            }
        }

        private void updateUI((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts)
        {
            if (Item == null)
                return;

            // 滚动过程中会有大量不可见/刚离屏的面板仍收到分析回调。
            // 这些面板的 UI 更新会造成明显 GC 压力与 Draw FPS 下降，因此先缓存为 pending，等再次可见时再应用。
            if (Item.IsVisible != true)
            {
                pendingKpsResult = result;
                pendingColumnCounts = columnCounts;
                pendingHoldNoteCounts = holdNoteCounts;
                hasPendingUiUpdate = true;
                return;
            }

            var (averageKps, maxKps, kpsList) = result;

            kpsDisplay.SetKps(averageKps, maxKps);

            // Update KPS graph with the KPS list
            if (kpsList.Count > 0)
            {
                kpsGraph.SetValues(kpsList);
            }

            if (columnCounts != null)
            {
                // 注意：分析结果里的 ColumnCounts 只包含“出现过的列”。
                // 当某个 mod 删除了某一列的所有 notes 时，这一列会缺失，
                // 直接显示会导致列号错位（看起来像“没有更新”）。
                // 这里把字典补齐到 0..keyCount-1，缺失列填 0。
                int keyCount = getCachedKpcKeyCount();
                ensureNormalizedCounts(keyCount);

                for (int i = 0; i < keyCount; i++)
                {
                    normalizedColumnCounts![i] = columnCounts.GetValueOrDefault(i);
                    normalizedHoldNoteCounts![i] = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                }

                int countsHash = computeCountsHash(normalizedColumnCounts!, normalizedHoldNoteCounts!, keyCount);
                var mode = kpcDisplay.CurrentKpcDisplayMode;

                if (countsHash != lastKpcCountsHash || mode != lastKpcMode)
                {
                    lastKpcCountsHash = countsHash;
                    lastKpcMode = mode;
                    kpcDisplay.UpdateColumnCounts(normalizedColumnCounts!, normalizedHoldNoteCounts!);
                }
            }
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            localRank.Beatmap = null;
            starDifficultyBindable = null;

            starDifficultyCancellationSource?.Cancel();
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisBindable = null;
            cachedScratchText = null;

            scheduledManiaUiUpdate?.Cancel();
            scheduledManiaUiUpdate = null;
            hasPendingUiUpdate = false;
            pendingColumnCounts = null;
            pendingHoldNoteCounts = null;

            displayXxySR.Current.Value = null;

            cachedKpcKeyCount = -1;
            cachedKpcRulesetId = -1;
            cachedKpcModsHash = 0;
            normalizedColumnCounts = null;
            normalizedHoldNoteCounts = null;
            normalizedCountsKeyCount = 0;

            lastKpcCountsHash = 0;
            lastKpcMode = default;
        }

        private int getCachedKpcKeyCount()
        {
            Guid beatmapId = beatmap.ID;
            int rulesetId = ruleset.Value.OnlineID;
            int modsHash = computeModsHash(mods.Value);

            if (cachedKpcKeyCount >= 0
                && cachedKpcBeatmapId == beatmapId
                && cachedKpcRulesetId == rulesetId
                && cachedKpcModsHash == modsHash)
                return cachedKpcKeyCount;

            ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
            cachedKpcKeyCount = legacyRuleset.GetKeyCount(beatmap, mods.Value);
            cachedKpcBeatmapId = beatmapId;
            cachedKpcRulesetId = rulesetId;
            cachedKpcModsHash = modsHash;
            return cachedKpcKeyCount;
        }

        private void ensureNormalizedCounts(int keyCount)
        {
            if (normalizedColumnCounts != null && normalizedHoldNoteCounts != null && normalizedCountsKeyCount == keyCount)
                return;

            normalizedCountsKeyCount = keyCount;
            normalizedColumnCounts = new Dictionary<int, int>(keyCount);
            normalizedHoldNoteCounts = new Dictionary<int, int>(keyCount);

            for (int i = 0; i < keyCount; i++)
            {
                normalizedColumnCounts[i] = 0;
                normalizedHoldNoteCounts[i] = 0;
            }
        }

        private static int computeModsHash(IReadOnlyList<Mod> mods)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < mods.Count; i++)
                    hash = hash * 31 + mods[i].GetHashCode();

                return hash;
            }
        }

        private static int computeCountsHash(Dictionary<int, int> columnCounts, Dictionary<int, int> holdCounts, int keyCount)
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < keyCount; i++)
                {
                    hash = hash * 31 + columnCounts.GetValueOrDefault(i);
                    hash = hash * 31 + holdCounts.GetValueOrDefault(i);
                }

                return hash;
            }
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
            {
                // Logger.Log("[PanelBeatmap] computeStarRating called but Item is null; skipping.", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            // Logger.Log($"[PanelBeatmap] computeStarRating called for beatmap {beatmap.OnlineID}/{beatmap.ID}", LoggingTarget.Runtime, LogLevel.Debug);

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, computationDelay: 0);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                // Logger.Log($"[PanelBeatmap] starDifficulty changed for beatmap {beatmap.OnlineID}/{beatmap.ID} stars={starDifficulty.NewValue.Stars}", LoggingTarget.Runtime, LogLevel.Debug);
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                starCounter.Current = (float)starDifficulty.NewValue.Stars;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;

                // 离屏时取消 mania 分析（其中包含 xxy_SR），避免后台为不可见项占用计算预算。
                maniaAnalysisCancellationSource?.Cancel();
                maniaAnalysisCancellationSource = null;
            }
            else
            {
                // 重新可见时再触发一次绑定/计算。
                if (maniaAnalysisCancellationSource == null && Item != null && ruleset.Value.OnlineID == 3)
                {
                    // 离屏期间取消分析后再次可见时，先清空旧值，避免短暂显示上一次谱面的结果。
                    resetManiaAnalysisDisplay();
                    bindManiaAnalysis();
                }

                // 如果离屏期间收到过分析结果（或刚好在离屏时更新被跳过），这里补一次 UI 应用。
                if (hasPendingUiUpdate && scheduledManiaUiUpdate == null)
                {
                    scheduledManiaUiUpdate = Scheduler.AddDelayed(() =>
                    {
                        scheduledManiaUiUpdate = null;

                        if (!hasPendingUiUpdate)
                            return;

                        hasPendingUiUpdate = false;
                        updateUI(pendingKpsResult, pendingColumnCounts, pendingHoldNoteCounts);
                    }, 0, false);
                }
            }

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            if (AccentColour != diffColour)
            {
                AccentColour = diffColour;
                starCounter.Colour = diffColour;

                backgroundBorder.Colour = diffColour;
                backgroundDifficultyTint.Colour = ColourInfo.GradientHorizontal(diffColour.Opacity(0.25f), diffColour.Opacity(0f));

                difficultyIcon.Colour = starRatingDisplay.DisplayedStars.Value > OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF ? colours.Orange1 : colourProvider.Background5;

                triangles.Colour = ColourInfo.GradientVertical(diffColour.Opacity(0.25f), diffColour.Opacity(0f));
            }
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            if (ruleset.Value.OnlineID == 3)
            {
                // Account for mania differences locally for now.
                // Eventually this should be handled in a more modular way, allowing rulesets to add more information to the panel.
                ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
                int keyCount = legacyRuleset.GetKeyCount(beatmap, mods.Value);

                // 选歌快速滚动/拖动时：优先显示基础 keyCount，等异步分析完成后再替换为 scratch 文本。
                keyCountText.Text = cachedScratchText ?? $"[{keyCount}K] ";

                keyCountText.Alpha = 1;
            }
            else
                keyCountText.Alpha = 0;
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                List<MenuItem> items = new List<MenuItem>();

                if (songSelect != null)
                    items.AddRange(songSelect.GetForwardActions(beatmap));

                return items.ToArray();
            }
        }
    }
}
