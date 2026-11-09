// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Maintenance surface: one dependency chain per store, selected item actions in the footer.
    /// </summary>
    /// <remarks>
    /// The layout is driven entirely by <see cref="EzDataRebuildCatalog"/>: nodes are rendered in declared order and
    /// the gap above each node is the catalog's answer for whether it consumes the node above. That keeps the
    /// visual reading ("tight = dependent, wide = independent") from drifting away from the dispatch graph, and a new
    /// action shows up here without touching this class.
    /// </remarks>
    public partial class EzDataRebuildDialog : OsuFocusedOverlayContainer
    {
        private readonly BindableList<EzDataRebuildAction> selected = new BindableList<EzDataRebuildAction>();
        private readonly Bindable<EzDataRebuildAction?> hovered = new Bindable<EzDataRebuildAction?>();

        private EzDataRebuildMaintenanceHandler handler = null!;

        private OsuSpriteText selectionText = null!;
        private OsuSpriteText statusText = null!;
        private RoundedButton backfillButton = null!;
        private RoundedButton forceButton = null!;

        internal IReadOnlyList<EzDataRebuildAction> SelectedActions => selected;

        [Resolved(canBeNull: true)]
        private BackgroundDataStoreProcessor? backgroundDataStoreProcessor { get; set; }

        [Resolved(canBeNull: true)]
        private EzAnalysisWarmupProcessor? analysisWarmupProcessor { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        public EzDataRebuildDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.66f, 0.82f);

            Masking = true;
            CornerRadius = EzUIStyleV1.CORNER_RADIUS_WEDGE;
        }

        public void ShowMaintenance() => Show();

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            handler = new EzDataRebuildMaintenanceHandler(backgroundDataStoreProcessor, analysisWarmupProcessor, dialogOverlay, notifications);

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.GreySeaFoamDark,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[] { createHeader(colours) },
                            new Drawable[] { createChains(colours) },
                            new Drawable[] { createStatus(colours) },
                            new Drawable[] { createFooter(colours) },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selected.BindCollectionChanged((_, _) => updateFooter());
            updateFooter();
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            hovered.Value = null;
            this.FadeOut(150, Easing.OutQuint);
        }

        private Drawable createHeader(OsuColour colours)
        {
            var header = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Vertical = 12, Horizontal = 20 },
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = EzSettingsStrings.DATA_REBUILD_DIALOG_HEADER,
                        Font = OsuFont.GetFont(size: 28),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = EzSettingsStrings.DATA_REBUILD_DIALOG_SUBTITLE,
                        Font = OsuFont.Default.With(size: 13),
                        Colour = colours.Yellow,
                    },
                },
            };

            var container = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    header,
                    new IconButton
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Icon = FontAwesome.Solid.Times,
                        Colour = colours.GreySeaFoamDarker,
                        Scale = new Vector2(0.8f),
                        Margin = new MarginPadding { Top = 10, Right = 10 },
                        Action = Hide,
                    },
                },
            };

            return container;
        }

        private Drawable createChains(OsuColour colours) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Horizontal = 12 },
            Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 12),
                        new Dimension(),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            createChain(EzSettingsStrings.DATA_REBUILD_SIDE_REALM, EzDataRebuildCatalog.Realm, colours),
                            new Container(),
                            createChain(EzSettingsStrings.DATA_REBUILD_SIDE_SQLITE, EzDataRebuildCatalog.Sqlite, colours),
                        },
                    },
                },
            },
        };

        private Drawable createChain(LocalisableString title, IReadOnlyList<EzDataRebuildNode> nodes, OsuColour colours)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = Vector2.Zero,
            };

            flow.Add(new OsuSpriteText
            {
                Text = title,
                Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                Colour = colours.GreySeaFoamLighter,
                Margin = new MarginPadding { Bottom = 8 },
            });

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                // The first node needs no leading break; every other one is spaced by its declared dependency.
                flow.Add(new RebuildNodeCard(node, selected, hovered, handler.CanExecute(node.Action), colours)
                {
                    Margin = new MarginPadding { Top = i == 0 ? 0 : EzDataRebuildCatalog.GapBefore(node) },
                    Action = () => toggle(node.Action),
                });
            }

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Vertical = 12, Horizontal = 6 },
                Child = flow,
            };
        }

        private Drawable createStatus(OsuColour colours) => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Horizontal = 20, Bottom = 6 },
            Child = statusText = new OsuSpriteText
            {
                Font = OsuFont.Default.With(size: 13),
                Colour = colours.GreySeaFoamLighter,
                Text = string.Empty,
            },
        };

        private Drawable createFooter(OsuColour colours) => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Horizontal = 20, Vertical = 12 },
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        selectionText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.Default.With(size: 15),
                            Colour = colours.GreySeaFoamLighter,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Children = new Drawable[]
                            {
                                backfillButton = new RoundedButton
                                {
                                    Text = EzSettingsStrings.DATA_REBUILD_BACKFILL,
                                    Width = 120,
                                    Action = () => run(force: false),
                                },
                                forceButton = new DangerousRoundedButton
                                {
                                    Text = EzSettingsStrings.DATA_REBUILD_DIALOG_FORCE,
                                    Width = 140,
                                    Action = () => run(force: true),
                                },
                                new RoundedButton
                                {
                                    Text = EzSettingsStrings.DATA_REBUILD_CLOSE,
                                    Width = 90,
                                    Action = Hide,
                                },
                            },
                        },
                    },
                },
            },
        };

        private void toggle(EzDataRebuildAction action)
        {
            if (!selected.Remove(action))
                selected.Add(action);
        }

        private void run(bool force)
        {
            var actions = selected.ToArray();

            if (actions.Length == 0)
                return;

            if (force)
            {
                handler.RequestForceRebuild(actions);
                return;
            }

            var outcome = handler.RequestBackfill(actions, forceAll: false);

            statusText.Text = outcome.Failures.Count == 0
                ? LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_QUEUED, EzDataRebuildCatalog.DisplayNames(outcome.Queued))
                : EzDataRebuildMaintenanceHandler.GetErrorMessage(outcome.FirstFailure);
        }

        private void updateFooter()
        {
            var actions = selected.ToArray();
            bool available = actions.Any(handler.CanExecute);

            backfillButton.Enabled.Value = available;
            forceButton.Enabled.Value = available;

            if (actions.Length == 0)
                selectionText.Text = EzSettingsStrings.DATA_REBUILD_NOTHING_SELECTED;
            else if (actions.Length == 1)
                selectionText.Text = LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_SELECTED, nameOf(actions[0]));
            else
                selectionText.Text = LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_SELECTED_MULTIPLE, actions.Length, EzDataRebuildCatalog.DisplayNames(actions));
        }

        private static LocalisableString nameOf(EzDataRebuildAction action) =>
            EzDataRebuildCatalog.Find(action)?.Name ?? action.ToString();

        /// <summary>
        /// One selectable maintenance node: name, what it feeds, and a state background. Selection is a toggle, so
        /// the card also serves as the deselection target.
        /// </summary>
        internal partial class RebuildNodeCard : OsuClickableContainer
        {
            private const double transition_duration = 120;

            private readonly EzDataRebuildNode node;
            private readonly IBindableList<EzDataRebuildAction> selection;
            private readonly Bindable<EzDataRebuildAction?> hovered;
            private readonly OsuColour colours;
            private readonly Box background;
            private readonly OsuSpriteText title;
            private readonly OsuSpriteText detail;

            internal EzDataRebuildAction NodeAction => node.Action;

            public RebuildNodeCard(EzDataRebuildNode node, IBindableList<EzDataRebuildAction> selection, Bindable<EzDataRebuildAction?> hovered, bool available,
                                   OsuColour colours)
            {
                this.node = node;
                this.selection = selection;
                this.hovered = hovered;
                this.colours = colours;

                var dependencies = EzDataRebuildCatalog.DependenciesOf(node.Action);

                if (dependencies.Count > 0)
                {
                    TooltipText = LocalisableString.Format(
                        EzSettingsStrings.DATA_REBUILD_DEPENDS_ON,
                        node.Name,
                        EzDataRebuildCatalog.DisplayNames(dependencies.Select(d => d.Action)));
                }

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 6;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.GreySeaFoamDarker,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            title = new OsuSpriteText
                            {
                                Text = node.Name,
                                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                Alpha = available ? 1 : 0.5f,
                            },
                            detail = new OsuSpriteText
                            {
                                Text = node.Detail,
                                Font = OsuFont.Default.With(size: 12),
                                Alpha = available ? 1 : 0.5f,
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selection.CollectionChanged += onSelectionChanged;
                hovered.ValueChanged += onHoveredChanged;
                updateAppearance();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (isDisposing)
                {
                    selection.CollectionChanged -= onSelectionChanged;
                    hovered.ValueChanged -= onHoveredChanged;
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                hovered.Value = node.Action;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (hovered.Value == node.Action)
                    hovered.Value = null;

                base.OnHoverLost(e);
            }

            private void onSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => updateAppearance();

            private void onHoveredChanged(ValueChangedEvent<EzDataRebuildAction?> e) => updateAppearance();

            /// <summary>
            /// All visual states flow through here: hover is no longer a separate fade that a selection refresh can
            /// overwrite, and dependency hints survive while the dependent item stays hovered or selected. A plain
            /// selection keeps the accent colour; a selection whose inputs are not selected too gets the warning
            /// colour, and those inputs get a neutral grey lift instead.
            /// </summary>
            private void updateAppearance()
            {
                bool isSelected = selection.Contains(node.Action);
                bool isHovered = hovered.Value == node.Action;
                bool isDependency = !isSelected && !isHovered && isDependencyOfHoverOrSelection();

                Color4 backgroundColour;
                Color4 titleColour;
                Color4 detailColour;

                if (isSelected)
                {
                    bool warns = hasUnselectedDependency(node.Action);

                    if (warns)
                        backgroundColour = isHovered ? colours.YellowLight : colours.Yellow;
                    else
                        backgroundColour = isHovered ? colours.Blue2 : colours.Blue3;

                    titleColour = colours.GreySeaFoamDarker;
                    detailColour = colours.GreySeaFoamDarker;
                }
                else if (isHovered)
                {
                    backgroundColour = colours.GreySeaFoamLight;
                    titleColour = Color4.White;
                    detailColour = colours.GreySeaFoamLighter;
                }
                else if (isDependency)
                {
                    backgroundColour = colours.GreySeaFoam;
                    titleColour = colours.GreySeaFoamLighter;
                    detailColour = colours.GreySeaFoamLighter;
                }
                else
                {
                    backgroundColour = colours.GreySeaFoamDarker;
                    titleColour = Color4.White;
                    detailColour = colours.GreySeaFoamLighter;
                }

                background.FadeColour(backgroundColour, transition_duration, Easing.OutQuint);
                title.FadeColour(titleColour, transition_duration, Easing.OutQuint);
                detail.FadeColour(detailColour, transition_duration, Easing.OutQuint);
            }

            private bool hasUnselectedDependency(EzDataRebuildAction action)
            {
                foreach (var dependency in EzDataRebuildCatalog.DependenciesOf(action))
                {
                    if (!selection.Contains(dependency.Action))
                        return true;
                }

                return false;
            }

            private bool isDependencyOfHoverOrSelection()
            {
                if (isDependencyOf(hovered.Value))
                    return true;

                foreach (var action in selection)
                {
                    if (isDependencyOf(action))
                        return true;
                }

                return false;
            }

            private bool isDependencyOf(EzDataRebuildAction? action)
            {
                if (action is not { } target || target == node.Action)
                    return false;

                foreach (var dependency in EzDataRebuildCatalog.DependenciesOf(target))
                {
                    if (dependency.Action == node.Action)
                        return true;
                }

                return false;
            }
        }
    }
}
