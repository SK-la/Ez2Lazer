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

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private IBeatmap? iBeatmap;

        private readonly Dictionary<string, (double averageKps, double maxKps, List<double> kpsList)> kpsCache = new Dictionary<string, (double, double, List<double>)>();
        // private LineGraph kpsGraph = null!;

        // 添加异步计算相关字段
        private CancellationTokenSource? kpsCalculationCancellationSource;

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
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        },
                                        // TODO: 在此处增加XXY_SR
                                        new OsuSpriteText
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
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                computeStarRating();
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

            updateCalculationsAsync(beatmap);
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

        private void updateCalculationsAsync(BeatmapInfo beatmapInfo)
        {
            if (ruleset.Value.OnlineID != 3)
                return;

            kpsCalculationCancellationSource?.Cancel();
            kpsCalculationCancellationSource = new CancellationTokenSource();
            var cancellationToken = kpsCalculationCancellationSource.Token;

            string cacheKey = $"{beatmapInfo.Hash}_{string.Join(",", mods.Value?.Select(m => m.Acronym) ?? Array.Empty<string>())}";

            if (kpsCache.TryGetValue(cacheKey, out var cachedResult))
            {
                updateUI(cachedResult, null);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.Value, mods.Value, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    // 使用优化后的计算器一次性获取所有数据
                    var (averageKps, maxKps, kpsList, columnCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);
                    var kpsResult = (averageKps, maxKps, kpsList);

                    cancellationToken.ThrowIfCancellationRequested();

                    kpsCache[cacheKey] = kpsResult;

                    Schedule(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            iBeatmap = playableBeatmap;
                            updateUI(kpsResult, columnCounts);
                            updateKeyCount();
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    Schedule(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            updateUI((0, 0, new List<double>()), null);
                        }
                    });
                }
            }, cancellationToken);
        }

        private void updateUI((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts)
        {
            var (averageKps, maxKps, _) = result;

            maniaKpsDisplay.SetKps(averageKps, maxKps);

            if (columnCounts != null && columnCounts.Any())
            {
                maniaKpcDisplay.UpdateColumnCounts(columnCounts);
            }
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            localRank.Beatmap = null;
            starDifficultyBindable = null;

            starDifficultyCancellationSource?.Cancel();
            kpsCalculationCancellationSource?.Cancel();
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

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
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

                // TODO: 当前机制不健全，显示内容可能不准确，怀疑被缓存影响而未能及时更新
                if (iBeatmap != null)
                {
                    string keyCountTextValue = EzBeatmapCalculator.GetScratch(iBeatmap, keyCount);
                    keyCountText.Text = keyCountTextValue;
                }
                else
                {
                    keyCountText.Text = $"[{keyCount}K] ";
                }

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
