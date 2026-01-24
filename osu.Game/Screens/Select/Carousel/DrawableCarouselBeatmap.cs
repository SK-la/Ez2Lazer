// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.UserInterface;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.SelectV2;
using osuTK;
using osuTK.Graphics;
using CommonStrings = osu.Game.Localisation.CommonStrings;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select.Carousel
{
    public partial class DrawableCarouselBeatmap : DrawableCarouselItem, IHasContextMenu
    {
        public const float CAROUSEL_BEATMAP_SPACING = 5;
        private const int mania_ui_update_throttle_ms = 15;

        /// <summary>
        /// The height of a carousel beatmap, including vertical spacing.
        /// </summary>
        public const float HEIGHT = height + CAROUSEL_BEATMAP_SPACING;

        private const float height = MAX_HEIGHT * 0.9f;

        private readonly BeatmapInfo beatmapInfo;

        private Sprite background = null!;

        private MenuItem[]? mainMenuItems;

        private Action<BeatmapInfo>? selectRequested;
        private Action<BeatmapInfo>? hideRequested;

        private Triangles triangles = null!;

        private StarCounter starCounter = null!;
        private DifficultyIcon difficultyIcon = null!;

        private OsuSpriteText keyCountText = null!;

        private EzDisplayKpsGraph ezDisplayKpsGraph = null!;
        private EzKpsDisplay ezKpsDisplay = null!;
        private EzKpcDisplay ezKpcDisplay = null!;
        private EzDisplayXxySR displayXxySR = null!;

        private Bindable<bool> xxySrFilterSetting = null!;

        private int keyCount;

        private IBindable<ManiaBeatmapAnalysisResult>? maniaAnalysisBindable;
        private CancellationTokenSource? maniaAnalysisCancellationSource;
        private string? cachedScratchText;

        private Dictionary<int, int>? normalizedColumnCounts;
        private Dictionary<int, int>? normalizedHoldNoteCounts;
        private int normalizedCountsKeyCount;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapManager? manager { get; set; }

        private IBindable<StarDifficulty> starDifficultyBindable = null!;
        private CancellationTokenSource? starDifficultyCancellationSource;

        public DrawableCarouselBeatmap(CarouselBeatmap panel)
        {
            beatmapInfo = panel.BeatmapInfo;
            Item = panel;
        }

        [BackgroundDependencyLoader]
        private void load(SongSelect? songSelect)
        {
            Header.Height = height;

            if (songSelect != null)
            {
                mainMenuItems = songSelect.CreateForwardNavigationMenuItemsForBeatmap(() => beatmapInfo);
                selectRequested = b => songSelect.FinaliseSelection(b);
            }

            if (manager != null)
                hideRequested = b => manager.Hide(b);

            Header.Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new Triangles
                {
                    TriangleScale = 2,
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = Color4Extensions.FromHex(@"3a7285"),
                    ColourDark = Color4Extensions.FromHex(@"123744")
                },
                new FillFlowContainer
                {
                    Padding = new MarginPadding(5),
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        difficultyIcon = new DifficultyIcon(beatmapInfo)
                        {
                            TooltipType = DifficultyIconTooltipType.None,
                            Scale = new Vector2(1.8f),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft
                        },
                        new FillFlowContainer
                        {
                            Padding = new MarginPadding { Left = 5 },
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        ezDisplayKpsGraph = new EzDisplayKpsGraph
                                        {
                                            Size = new Vector2(300, 20),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4, 0),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.GetFont(size: 20),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = beatmapInfo.DifficultyName,
                                            Font = OsuFont.GetFont(size: 20),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmapInfo.Metadata.Author.Username),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        ezKpsDisplay = new EzKpsDisplay
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4, 0),
                                    Scale = new Vector2(0.8f),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new TopLocalRank(beatmapInfo),
                                        starCounter = new StarCounter(),
                                        displayXxySR = new EzDisplayXxySR
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        ezKpcDisplay = new EzKpcDisplay
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        }
                                    }
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
                resetManiaAnalysisDisplay();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
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
            }, true);
        }

        protected override void Selected()
        {
            base.Selected();

            MovementContainer.MoveToX(-50, 500, Easing.OutExpo);

            background.Colour = ColourInfo.GradientVertical(
                new Color4(20, 43, 51, 255),
                new Color4(40, 86, 102, 255));

            triangles.Colour = Color4.White;
        }

        protected override void Deselected()
        {
            base.Deselected();

            MovementContainer.MoveToX(0, 500, Easing.OutExpo);

            background.Colour = new Color4(20, 43, 51, 255);
            triangles.Colour = OsuColour.Gray(0.5f);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Item?.State.Value == CarouselItemState.Selected)
                selectRequested?.Invoke(beatmapInfo);

            return base.OnClick(e);
        }

        protected override void ApplyState()
        {
            if (Item?.State.Value != CarouselItemState.Collapsed && Alpha == 0)
                starCounter.ReplayAnimation();

            starDifficultyCancellationSource?.Cancel();
            maniaAnalysisCancellationSource?.Cancel();

            // Only compute difficulty when the item is visible.
            if (Item?.State.Value != CarouselItemState.Collapsed)
            {
                // We've potentially cancelled the computation above so a new bindable is required.
                starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmapInfo, (starDifficultyCancellationSource = new CancellationTokenSource()).Token, 200);
                starDifficultyBindable.BindValueChanged(d =>
                {
                    starCounter.Current = (float)(d.NewValue.Stars);
                    difficultyIcon.Current.Value = d.NewValue;
                }, true);

                updateKeyCount();
            }

            // Start/refresh mania analysis binding when visible
            computeManiaAnalysis();

            base.ApplyState();
        }

        private void resetManiaAnalysisDisplay()
        {
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

        private void ensureNormalizedCounts(int keyCount)
        {
            // Defensive: ensure keyCount is non-negative. Some legacy paths may
            // return -1 when unimplemented, which would crash when used as a
            // Dictionary capacity.
            if (keyCount < 0)
                keyCount = 0;

            if (normalizedColumnCounts != null && normalizedHoldNoteCounts != null && normalizedCountsKeyCount == keyCount)
                return;

            normalizedCountsKeyCount = keyCount;
            normalizedColumnCounts = new Dictionary<int, int>(Math.Max(0, keyCount));
            normalizedHoldNoteCounts = new Dictionary<int, int>(Math.Max(0, keyCount));

            for (int i = 0; i < keyCount; i++)
            {
                normalizedColumnCounts[i] = 0;
                normalizedHoldNoteCounts[i] = 0;
            }
        }

        private void updateKPs((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts)
        {
            if (Item == null)
                return;

            if (Item?.State.Value == CarouselItemState.Collapsed)
                return;

            var (averageKps, maxKps, kpsList) = result;

            ezKpsDisplay.SetKps(averageKps, maxKps);

            if (kpsList.Count > 0)
            {
                ezDisplayKpsGraph.SetPoints(kpsList);
            }

            if (columnCounts != null)
            {
                ezKpcDisplay.UpdateColumnCounts(columnCounts, holdNoteCounts, keyCount);
            }
        }

        private void computeManiaAnalysis()
        {
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            if (Item?.State.Value == CarouselItemState.Collapsed)
                return;

            maniaAnalysisBindable = maniaAnalysisCache.GetBindableAnalysis(beatmapInfo, maniaAnalysisCancellationSource.Token, computationDelay: 100);
            maniaAnalysisBindable.BindValueChanged(result =>
            {
                cachedScratchText = result.NewValue.ScratchText;
                Schedule(updateKeyCount);
                updateKPs((result.NewValue.AverageKps, result.NewValue.MaxKps, result.NewValue.KpsList), result.NewValue.ColumnCounts, result.NewValue.HoldNoteCounts);
                displayXxySR.Current.Value = result.NewValue.XxySr;
            }, true);
        }

        private void updateKeyCount()
        {
            if (Item?.State.Value == CarouselItemState.Collapsed)
                return;

            if (ruleset.Value.OnlineID == 3)
            {
                // Account for mania differences locally for now.
                // Eventually this should be handled in a more modular way, allowing rulesets to add more information to the panel.
                ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();

                keyCount = legacyRuleset.GetKeyCount(beatmapInfo, mods.Value);
                keyCountText.Alpha = 1;
                keyCountText.Text = cachedScratchText ?? $"[{keyCount}K] ";
                keyCountText.Colour = Colour4.LightPink.ToLinear();
            }
            else
                keyCountText.Alpha = 0;
        }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                List<MenuItem> items = new List<MenuItem>();

                if (mainMenuItems != null)
                    items.AddRange(mainMenuItems);

                if (beatmapInfo.OnlineID > 0 && beatmapOverlay != null)
                    items.Add(new OsuMenuItem("Details...", MenuItemType.Standard, () => beatmapOverlay.FetchAndShowBeatmap(beatmapInfo.OnlineID)));

                var collectionItems = realm.Realm.All<BeatmapCollection>()
                                           .OrderBy(c => c.Name)
                                           .AsEnumerable()
                                           .Select(c => new CollectionToggleMenuItem(c.ToLive(realm), beatmapInfo)).Cast<OsuMenuItem>().ToList();

                if (manageCollectionsDialog != null)
                    collectionItems.Add(new OsuMenuItem("Manage...", MenuItemType.Standard, manageCollectionsDialog.Show));

                items.Add(new OsuMenuItem("Collections") { Items = collectionItems });

                if (beatmapInfo.GetOnlineURL(api, ruleset.Value) is string url)
                    items.Add(new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url)));

                if (manager != null)
                    items.Add(new OsuMenuItem("Mark as played", MenuItemType.Standard, () => manager.MarkPlayed(beatmapInfo)));

                if (hideRequested != null)
                    items.Add(new OsuMenuItem(WebCommonStrings.ButtonsHide.ToSentence(), MenuItemType.Destructive, () => hideRequested(beatmapInfo)));

                return items.ToArray();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            starDifficultyCancellationSource?.Cancel();
            maniaAnalysisCancellationSource?.Cancel();
            starDifficultyBindable.UnbindAll();
            starDifficultyBindable = null!;

            maniaAnalysisBindable?.UnbindAll();
            maniaAnalysisBindable = null;
        }
    }
}
