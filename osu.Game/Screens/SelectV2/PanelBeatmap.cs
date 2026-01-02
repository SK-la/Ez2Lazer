// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Framework.Threading;
using osuTK;

namespace osu.Game.Screens.SelectV2
{
    public partial class PanelBeatmap : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT;

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

        private ManiaKpsDisplay maniaKpsDisplay = null!;
        private ManiaKpcDisplay maniaKpcDisplay = null!;
        private EzXxySrDisplay xxySrDisplay = null!;
        private OsuSpriteText notesLabel = null!;

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

        private double? lastStarRatingStars;
        private Guid? loggedAbnormalXxySrBeatmapId;

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
                                        maniaKpsDisplay = new ManiaKpsDisplay(),
                                        // kpsGraph = new LineGraph
                                        // {
                                        //     Size = new Vector2(300, 20),
                                        //     Colour = OsuColour.Gray(0.25f),
                                        //     Anchor = Anchor.BottomLeft,
                                        //     Origin = Anchor.BottomLeft,
                                        // },
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
                                        xxySrDisplay = new EzXxySrDisplay()
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
                                        notesLabel = new OsuSpriteText
                                        {
                                            Text = "[Notes] ",
                                            Font = OsuFont.GetFont(size: 14),
                                            Colour = Colour4.GhostWhite,
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        maniaKpcDisplay = new ManiaKpcDisplay(),
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
                bindManiaAnalysis();
                resetManiaAnalysisDisplay();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                computeStarRating();
                bindManiaAnalysis();
                resetManiaAnalysisDisplay();
                updateKeyCount();
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

            lastStarRatingStars = null;
            loggedAbnormalXxySrBeatmapId = null;

            bindManiaAnalysis();
            resetManiaAnalysisDisplay();
            computeStarRating();
            updateKeyCount();
        }

        private void maybeLogLargeStarDiff()
        {
            if (Item == null)
                return;

            // 仅用于排查：无 mod 时，原版 star 与 xxy_SR 差值过大，或 xxy_SR 计算异常（null 或 0）。
            if (ruleset.Value.OnlineID != 3)
                return;

            if (mods.Value.Count != 0)
                return;

            double? star = lastStarRatingStars;
            double? xxy = xxySrDisplay.Current.Value;

            Guid beatmapId = beatmap.ID;

            // 如果已经为这个 beatmap 记录过异常，则跳过
            if (loggedAbnormalXxySrBeatmapId == beatmapId)
                return;

            // 检查 xxy_SR 是否为 null 或 0
            if (xxy == null || xxy == 0)
            {
                loggedAbnormalXxySrBeatmapId = beatmapId;

                Logger.Log(
                    XxySrDebugJson.FormatNullOrZeroSr(beatmap, xxy),
                    "xxy_sr",
                    LogLevel.Error);
                return;
            }

            if (star == null)
                return;

            double diff = Math.Abs(star.Value - xxy.Value);
            if (diff <= 3)
                return;

            loggedAbnormalXxySrBeatmapId = beatmapId;

            Logger.Log(
                XxySrDebugJson.FormatLargeDiffNoMod(beatmap, star.Value, xxy.Value),
                "xxy_sr",
                LogLevel.Error);
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

            maniaAnalysisBindable = maniaAnalysisCache.GetBindableAnalysis(beatmap, maniaAnalysisCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            maniaAnalysisBindable.BindValueChanged(result =>
            {
                // 旧 bindable 的回调（比如切换 mods / 取消重绑）直接忽略。
                if (localCancellationSource != maniaAnalysisCancellationSource)
                    return;

                // DrawablePool 回收/Item 变更期间可能仍收到旧 bindable 的回调。
                // 这时 beatmap 属性会因为 Item 为 null 而抛出异常，因此直接忽略。
                if (Item == null)
                    return;

                if (!string.IsNullOrEmpty(result.NewValue.ScratchText))
                    cachedScratchText = result.NewValue.ScratchText;

                updateUI((result.NewValue.AverageKps, result.NewValue.MaxKps, result.NewValue.KpsList), result.NewValue.ColumnCounts);

                xxySrDisplay.Current.Value = result.NewValue.XxySr;
                maybeLogLargeStarDiff();

                updateKeyCount();
            }, true);
        }

        private void resetManiaAnalysisDisplay()
        {
            cachedScratchText = null;
            maniaKpcDisplay.Clear();

            xxySrDisplay.Current.Value = null;

            if (ruleset.Value.OnlineID == 3)
            {
                maniaKpsDisplay.Show();
                maniaKpsDisplay.SetKps(0, 0);

                notesLabel.Show();
                maniaKpcDisplay.Show();
                xxySrDisplay.Show();
            }
            else
            {
                maniaKpsDisplay.Hide();

                // 非 mania：隐藏 mania 专属 UI。
                notesLabel.Hide();
                maniaKpcDisplay.Hide();
                xxySrDisplay.Hide();
            }
        }

        private void updateUI((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts)
        {
            if (Item == null)
                return;

            var (averageKps, maxKps, _) = result;

            maniaKpsDisplay.SetKps(averageKps, maxKps);

            if (columnCounts != null)
            {
                // 注意：分析结果里的 ColumnCounts 只包含“出现过的列”。
                // 当某个 mod 删除了某一列的所有 notes 时，这一列会缺失，
                // 直接显示会导致列号错位（看起来像“没有更新”）。
                // 这里把字典补齐到 0..keyCount-1，缺失列填 0。
                ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
                int keyCount = legacyRuleset.GetKeyCount(beatmap, mods.Value);

                var normalized = new Dictionary<int, int>(keyCount);
                for (int i = 0; i < keyCount; i++)
                    normalized[i] = columnCounts.GetValueOrDefault(i);

                maniaKpcDisplay.UpdateColumnCounts(normalized);
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

            xxySrDisplay.Current.Value = null;

            lastStarRatingStars = null;
            loggedAbnormalXxySrBeatmapId = null;
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

                lastStarRatingStars = starDifficulty.NewValue.Stars;
                maybeLogLargeStarDiff();
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
