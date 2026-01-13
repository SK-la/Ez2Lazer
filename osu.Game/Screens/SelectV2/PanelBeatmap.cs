// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
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

        private EzDisplayLineGraph ezKpsGraph = null!;
        private EzKpsDisplay ezKpsDisplay = null!;
        private EzKpcDisplay ezKpcDisplay = null!;
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
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        },
                                        ezKpcDisplay = new EzKpcDisplay()
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
                computeManiaAnalysis();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                computeStarRating();
                computeManiaAnalysis();
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
                ezKpcDisplay.CurrentKpcDisplayMode = mode.NewValue;
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

            resetManiaAnalysisDisplay();
            computeStarRating();
            computeManiaAnalysis();
            updateKeyCount();
        }

        private Drawable getRulesetIcon(RulesetInfo rulesetInfo)
        {
            var rulesetInstance = rulesets.GetRuleset(rulesetInfo.ShortName)?.CreateInstance();

            if (rulesetInstance is null)
                return new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };

            return rulesetInstance.CreateIcon();
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
                updateKPs(pendingKpsResult, pendingColumnCounts, pendingHoldNoteCounts);
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

        private void updateKPs((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts)
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
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                starCounter.Current = (float)starDifficulty.NewValue.Stars;
            }, true);
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

                if (result.NewValue.XxySr != null)
                    displayXxySR.Current.Value = result.NewValue.XxySr;
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
                // 重新可见时再触发一次计算
                if (maniaAnalysisCancellationSource == null && Item != null)
                {
                    computeManiaAnalysis();
                }

                // 如果离屏期间收到过分析结果（或刚好在离屏时更新被跳过），这里补一次 UI 应用。
                // if (hasPendingUiUpdate && scheduledManiaUiUpdate == null)
                // {
                //     scheduledManiaUiUpdate = Scheduler.AddDelayed(() =>
                //     {
                //         scheduledManiaUiUpdate = null;
                //
                //         if (!hasPendingUiUpdate)
                //             return;
                //
                //         hasPendingUiUpdate = false;
                //         updateKPs(pendingKpsResult, pendingColumnCounts, pendingHoldNoteCounts);
                //     }, 0, false);
                // }
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

                triangles.Colour = ColourInfo.GradientVertical(diffColour.Opacity(0.25f), diffColour.Opacity(0f));
            }

            if (difficultyIcon.Colour != starRatingDisplay.DisplayedDifficultyTextColour)
            {
                difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
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

                keyCountText.Alpha = 1;
                keyCountText.Text = cachedScratchText ?? $"[{keyCount}K] ";
                keyCountText.Colour = Colour4.LightPink.ToLinear();
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
