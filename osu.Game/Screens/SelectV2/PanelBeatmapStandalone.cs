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
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.UserInterface;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class PanelBeatmapStandalone : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;

        private const int mania_ui_update_throttle_ms = 100;
        private const int background_load_delay_ms = 50;
        private const int metadata_text_delay_ms = 30;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        private IBindable<ManiaBeatmapAnalysisResult>? maniaAnalysisBindable;
        private CancellationTokenSource? maniaAnalysisCancellationSource;
        private bool applyNextManiaUiUpdateImmediately;
        private string? cachedScratchText;
        private EzKpsDisplay ezKpsDisplay = null!;
        private EzDisplayLineGraph ezKpsGraph = null!;
        private EzKpcDisplay ezKpcDisplay = null!;

        private EzDisplayXxySR displayXxySR = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private int cachedKpcKeyCount = -1;
        private Guid cachedKpcBeatmapId;
        private int cachedKpcRulesetId = -1;
        private int cachedKpcModsHash;

        private Dictionary<int, int>? normalizedColumnCounts;
        private Dictionary<int, int>? normalizedHoldNoteCounts;
        private int normalizedCountsKeyCount;

        private int lastKpcCountsHash;
        private EzKpcDisplay.KpcDisplayMode lastKpcMode;

        private PanelSetBackground beatmapBackground = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private ScheduledDelegate? scheduledMetadataTextUpdate;

        private ScheduledDelegate? scheduledManiaUiUpdate;
        private (double averageKps, double maxKps, List<double> kpsList) pendingKpsResult;
        private Dictionary<int, int>? pendingColumnCounts;
        private Dictionary<int, int>? pendingHoldNoteCounts;
        private bool hasPendingUiUpdate;

        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;

        private ConstrainedIconContainer difficultyIcon = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private SpreadDisplay spreadDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText keyCountText = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private Box backgroundBorder = null!;

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmapStandalone()
        {
            PanelXOffset = 20;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(12),
                Margin = new MarginPadding { Left = 4f, Right = 3f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                beatmapBackground = new PanelSetBackground(),
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
                            Padding = new MarginPadding { Bottom = 4.8f },
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2.With(typeface: Typeface.TorusAlternate, weight: FontWeight.Bold),
                                },
                                artistText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Padding = new MarginPadding { Top = -2 },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        statusPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                        },
                                        updateButton = new PanelUpdateBeatmapButton
                                        {
                                            Scale = new Vector2(0.8f),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 4f, Bottom = -1f },
                                        },
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
                                        ezKpsDisplay = new EzKpsDisplay
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
                                        Empty(),
                                        ezKpsGraph = new EzDisplayLineGraph
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
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Enabled = { BindTarget = Selected }
                                        },
                                        ezKpcDisplay = new EzKpcDisplay
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                    },
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ =>
            {
                cachedScratchText = null;
                applyNextManiaUiUpdateImmediately = true;

                computeManiaAnalysis();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                cachedScratchText = null;
                applyNextManiaUiUpdateImmediately = true;

                computeManiaAnalysis();
                updateKeyCount();
            }, true);

            Selected.BindValueChanged(s => Expanded.Value = s.NewValue, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = beatmap.BeatmapSet!;

            // Background/texture uploads are a major draw FPS limiter during fast scrolling.
            // Delay background retrieval and only load if still visible and not pooled/reused.
            beatmapBackground.Beatmap = null;
            scheduleBackgroundLoad();

            // Delay high-variance metadata text assignment to reduce glyph/atlas churn during fast scrolling.
            scheduledMetadataTextUpdate?.Cancel();
            scheduledMetadataTextUpdate = null;
            scheduledBackgroundRetrieval = Scheduler.AddDelayed(b => beatmapBackground.Beatmap = beatmaps.GetWorkingBeatmap(b), beatmap, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmap.Status;

            difficultyIcon.Icon = beatmap.Ruleset.CreateInstance().CreateIcon();
            difficultyIcon.Show();

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            cachedScratchText = null;

            resetManiaAnalysisDisplay();
            computeManiaAnalysis();
            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            updateKeyCount();
        }

        private void scheduleBackgroundLoad()
        {
            if (Item == null)
                return;

            // Only attempt to load backgrounds for currently visible panels.
            if (Item.IsVisible != true)
                return;

            if (scheduledBackgroundRetrieval != null)
                return;

            Guid scheduledBeatmapId = beatmap.ID;

            scheduledBackgroundRetrieval = Scheduler.AddDelayed(() =>
            {
                scheduledBackgroundRetrieval = null;

                if (Item == null)
                    return;

                if (Item.IsVisible != true)
                    return;

                // Guard against pooled reuse.
                if (beatmap.ID != scheduledBeatmapId)
                    return;

                beatmapBackground.Beatmap = beatmaps.GetWorkingBeatmap(beatmap);
            }, background_load_delay_ms, false);
        }

        private void computeManiaAnalysis()
        {
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            // Reset UI to avoid showing stale data from previous beatmap
            // resetManiaAnalysisDisplay();

            maniaAnalysisBindable = maniaAnalysisCache.GetBindableAnalysis(beatmap, maniaAnalysisCancellationSource.Token, computationDelay: SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            maniaAnalysisBindable.BindValueChanged(result =>
            {
                // if (isPlaceholderAnalysisResult(result.NewValue))
                //     return;

                if (!string.IsNullOrEmpty(result.NewValue.ScratchText))
                    cachedScratchText = result.NewValue.ScratchText;

                queueManiaUiUpdate((result.NewValue.AverageKps, result.NewValue.MaxKps, result.NewValue.KpsList), result.NewValue.ColumnCounts, result.NewValue.HoldNoteCounts);

                displayXxySR.Current.Value = result.NewValue.XxySr;
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
            // After a mod/ruleset change, apply the first incoming result immediately to avoid a visible blank window.
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

            // Coalesce multiple incoming analysis updates into a single UI update.
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

        private void resetManiaAnalysisDisplay()
        {
            cachedScratchText = null;
            displayXxySR.Current.Value = null;

            if (ruleset.Value.OnlineID == 3)
            {
                ezKpcDisplay.Show();
                displayXxySR.Show();
            }
            else
            {
                ezKpcDisplay.Hide();
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

            ezKpsDisplay.SetKps(averageKps, maxKps);

            // Update KPS graph with the KPS list
            if (kpsList.Count > 0)
            {
                ezKpsGraph.SetValues(kpsList);
            }

            if (columnCounts != null)
            {
                // 同 PanelBeatmap：补齐缺失列为 0，避免列号错位。
                int keyCount = getCachedKpcKeyCount();
                ensureNormalizedCounts(keyCount);

                for (int i = 0; i < keyCount; i++)
                {
                    normalizedColumnCounts![i] = columnCounts.GetValueOrDefault(i);
                    normalizedHoldNoteCounts![i] = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                }

                int countsHash = computeCountsHash(normalizedColumnCounts!, normalizedHoldNoteCounts!, keyCount);
                var mode = ezKpcDisplay.CurrentKpcDisplayMode;

                if (countsHash != lastKpcCountsHash || mode != lastKpcMode)
                {
                    lastKpcCountsHash = countsHash;
                    lastKpcMode = mode;
                    ezKpcDisplay.UpdateColumnCounts(normalizedColumnCounts!, normalizedHoldNoteCounts!);
                }
            }
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;

            scheduledMetadataTextUpdate?.Cancel();
            scheduledMetadataTextUpdate = null;

            scheduledManiaUiUpdate?.Cancel();
            scheduledManiaUiUpdate = null;
            hasPendingUiUpdate = false;
            pendingColumnCounts = null;
            pendingHoldNoteCounts = null;
            beatmapBackground.Beatmap = null;
            updateButton.BeatmapSet = null;
            localRank.Beatmap = null;
            starDifficultyBindable = null;
            spreadDisplay.Beatmap.Value = null;

            starDifficultyCancellationSource?.Cancel();
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisBindable = null;
            cachedScratchText = null;

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
            // Logger.Log($"[PanelBeatmapStandalone] computeStarRating called for beatmap {beatmap.OnlineID}/{beatmap.ID}", LoggingTarget.Runtime, LogLevel.Debug);
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                // Logger.Log($"[PanelBeatmapStandalone] starDifficulty changed for beatmap {beatmap.OnlineID}/{beatmap.ID} stars={starDifficulty.NewValue.Stars}", LoggingTarget.Runtime, LogLevel.Debug);
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                spreadDisplay.StarDifficulty.Value = starDifficulty.NewValue;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                scheduledBackgroundRetrieval?.Cancel();
                scheduledBackgroundRetrieval = null;

                scheduledMetadataTextUpdate?.Cancel();
                scheduledMetadataTextUpdate = null;

                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;

                // 离屏时取消 mania 分析（其中包含 xxy_SR），避免后台为不可见项占用计算预算。
                maniaAnalysisCancellationSource?.Cancel();
                maniaAnalysisCancellationSource = null;
            }
            else
            {
                if (beatmapBackground.Beatmap == null)
                    scheduleBackgroundLoad();

                // 重新可见时再触发一次绑定/计算。
                if (maniaAnalysisCancellationSource == null && Item != null)
                {
                    // 离屏期间我们会 cancel 掉分析（避免浪费计算预算）。
                    // 重新变为可见时，必须先清空旧显示值，否则会短暂显示上一次谱面的结果（表现为 xxySR 跳变）。
                    resetManiaAnalysisDisplay();
                    computeManiaAnalysis();
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

            AccentColour = diffColour;
            spreadDisplay.Current.Colour = diffColour;

            backgroundBorder.Colour = diffColour;
            difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
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

                keyCountText.Alpha = 1;
                keyCountText.Text = cachedScratchText ?? $"[{keyCount}K] ";
                keyCountText.Colour = Colour4.LightPink.ToLinear();
            }
            else
                keyCountText.Alpha = 0;

            computeStarRating();
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
