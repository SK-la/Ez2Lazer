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
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;
using CommonStrings = osu.Game.Localisation.CommonStrings;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select.Carousel
{
    public partial class DrawableCarouselBeatmap : DrawableCarouselItem, IHasContextMenu
    {
        public const float CAROUSEL_BEATMAP_SPACING = 5;

        /// <summary>
        /// The height of a carousel beatmap, including vertical spacing.
        /// </summary>
        public const float HEIGHT = height + CAROUSEL_BEATMAP_SPACING;

        private const float height = MAX_HEIGHT * 0.8f;

        private readonly BeatmapInfo beatmapInfo;

        private Sprite background = null!;

        private MenuItem[]? mainMenuItems;

        private Action<BeatmapInfo>? selectRequested;
        private Action<BeatmapInfo>? hideRequested;

        private Triangles triangles = null!;

        private StarCounter starCounter = null!;
        private DifficultyIcon difficultyIcon = null!;

        private OsuSpriteText keyCountText = null!;

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

        private IBeatmap playableBeatmap = null!;
        private WorkingBeatmap working = null!;
        private FillFlowContainer columnNotes = null!;
        private LineGraph kpsGraph = null!;
        private OsuSpriteText kpsText = null!;

        private Dictionary<int, int> columnNoteCounts = new Dictionary<int, int>();
        private readonly Dictionary<string, (double averageKps, double maxKps, List<double> kpsList)> kpsCache = new Dictionary<string, (double, double, List<double>)>();
        private (double averageKps, double maxKps, List<double> kpsList) kpsResult;

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
            {
                hideRequested = b => manager.Hide(b);

                if (ruleset.Value.OnlineID == 3)
                {
                    working = manager.GetWorkingBeatmap(beatmapInfo);
                    playableBeatmap = working.GetPlayableBeatmap(ruleset.Value, mods.Value);
                }
            }

            // Schedule(() =>
            // {

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
                    ColourDark = Color4Extensions.FromHex(@"123744"),
                    Alpha = 0.8f,
                },
                kpsGraph = new LineGraph
                {
                    Size = new Vector2(600, 50),
                    Colour = OsuColour.Gray(0.25f),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new FillFlowContainer
                {
                    Padding = new MarginPadding(2),
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Padding = new MarginPadding { Left = 2 },
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                difficultyIcon = new DifficultyIcon(beatmapInfo)
                                {
                                    TooltipType = DifficultyIconTooltipType.None,
                                    Scale = new Vector2(1.8f),
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 2,
                                    Colour = Colour4.Transparent
                                },
                                new TopLocalRank(beatmapInfo)
                                {
                                    Scale = new Vector2(0.9f),
                                },
                            }
                        },
                        new FillFlowContainer
                        {
                            Padding = new MarginPadding { Left = 2 },
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
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
                                            Colour = Colour4.Pink
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
                                            Origin = Anchor.BottomLeft,
                                            Colour = Colour4.LightGoldenrodYellow
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
                                        new OsuSpriteText
                                        {
                                            Text = $"[{beatmapInfo.StarRating:F2}*] ",
                                            Font = OsuFont.GetFont(size: 18),
                                            Colour = Colour4.LightGoldenrodYellow,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                        starCounter = new StarCounter
                                        {
                                            Scale = new Vector2(0.6f),
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft
                                        },
                                        kpsText = new OsuSpriteText
                                        {
                                            Font = OsuFont.GetFont(size: 18),
                                            Colour = Colour4.CornflowerBlue,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft
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
                                        new OsuSpriteText
                                        {
                                            Text = "[Notes] ",
                                            Font = OsuFont.GetFont(size: 14),
                                            Colour = Colour4.GhostWhite,
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        columnNotes = new FillFlowContainer
                                        {
                                            Direction = FillDirection.Horizontal,
                                            AutoSizeAxes = Axes.Both,
                                        },
                                    }
                                },
                            }
                        },
                    }
                },
            };
            // });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateCalculations();
            ruleset.BindValueChanged(_ =>
            {
                updateCalculations();
                updateKeyCount();
            });
            mods.BindValueChanged(_ =>
            {
                updateCalculations();
                updateKeyCount();
            });
        }

        private void updateCalculations()
        {
            if (manager == null || ruleset.Value.OnlineID != 3)
                return;

            working = manager.GetWorkingBeatmap(beatmapInfo);

            if (mods.Value == null)
            {
                string beatmapHash = beatmapInfo.Hash;

                if (!kpsCache.TryGetValue(beatmapHash, out kpsResult))
                {
                    kpsResult = EzBeatmapCalculator.GetKps(working.Beatmap);
                    kpsCache[beatmapHash] = kpsResult;
                }
                else
                {
                    kpsResult = kpsCache[beatmapHash];
                }
            }
            else
            {
                kpsCache.Clear();

                try
                {
                    playableBeatmap = working.GetPlayableBeatmap(ruleset.Value, mods.Value);
                }
                catch (BeatmapInvalidForRulesetException)
                {
                    playableBeatmap = working.GetPlayableBeatmap(working.BeatmapInfo.Ruleset, mods.Value);
                }

                kpsResult = EzBeatmapCalculator.GetKps(playableBeatmap);
            }

            columnNoteCounts = EzBeatmapCalculator.GetColumnNoteCounts(playableBeatmap);
            var (averageKps, maxKps, kpsList) = kpsResult;
            // Schedule(() =>
            // {
            kpsGraph.Values = kpsList.Count > 0 ? kpsList.Select(kps => (float)kps).ToArray() : new[] { 0f };
            kpsText.Text = $"  KPS: {averageKps:F1} ({maxKps:F1} Max)";

            columnNotes.Clear();
            columnNotes.Children = columnNoteCounts
                                   .OrderBy(c => c.Key)
                                   .Select((c, index) => new FillFlowContainer
                                   {
                                       Direction = FillDirection.Horizontal,
                                       AutoSizeAxes = Axes.Both,
                                       Children = new Drawable[]
                                       {
                                           new OsuSpriteText
                                           {
                                               Text = $"{index + 1}/",
                                               Font = OsuFont.GetFont(size: 12),
                                               Colour = Colour4.Gray,
                                           },
                                           new OsuSpriteText
                                           {
                                               Text = $"{c.Value} ",
                                               Font = OsuFont.GetFont(size: 14),
                                               Colour = Colour4.LightCoral,
                                           }
                                       }
                                   }).ToArray();
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

            // Only compute difficulty when the item is visible.
            if (Item?.State.Value != CarouselItemState.Collapsed)
            {
                // We've potentially cancelled the computation above so a new bindable is required.
                starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmapInfo, (starDifficultyCancellationSource = new CancellationTokenSource()).Token, 200);
                starDifficultyBindable.BindValueChanged(d =>
                    {
                        starCounter.Current = (float)(d.NewValue.Stars);
                        difficultyIcon.Current.Value = d.NewValue;
                    },
                    true);

                updateCalculations();
                updateKeyCount();
            }

            base.ApplyState();
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

                int keyCount = legacyRuleset.GetKeyCount(beatmapInfo, mods.Value);

                string keyCountTextValue = EzBeatmapCalculator.GetScratch(playableBeatmap, keyCount);

                keyCountText.Alpha = 1;
                keyCountText.Text = keyCountTextValue;
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
        }
    }
}
