// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
        private readonly Bindable<EzDataRebuildAction?> selected = new Bindable<EzDataRebuildAction?>();

        private EzDataRebuildMaintenanceHandler handler = null!;

        private OsuSpriteText selectionText = null!;
        private OsuSpriteText statusText = null!;
        private RoundedButton backfillButton = null!;
        private RoundedButton forceButton = null!;

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

            selected.BindValueChanged(e => updateFooter(e.NewValue), true);
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut()
        {
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
                flow.Add(new RebuildNodeCard(node, selected, handler.CanExecute(node.Action), colours)
                {
                    Margin = new MarginPadding { Top = i == 0 ? 0 : EzDataRebuildCatalog.GapBefore(node) },
                    Action = () => selected.Value = node.Action,
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

        private void run(bool force)
        {
            if (selected.Value is not { } action)
                return;

            if (force)
            {
                handler.RequestForceRebuild(action);
                return;
            }

            var result = handler.RequestBackfill(action, forceAll: false);
            LocalisableString name = EzDataRebuildCatalog.Find(action)?.Name ?? action.ToString();

            statusText.Text = result == EzDataRebuildDispatchResult.Queued
                ? LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_QUEUED, name)
                : EzDataRebuildMaintenanceHandler.GetErrorMessage(result);
        }

        private void updateFooter(EzDataRebuildAction? action)
        {
            bool available = action is { } chosen && handler.CanExecute(chosen);

            backfillButton.Enabled.Value = available;
            forceButton.Enabled.Value = available;

            selectionText.Text = action is { } selectedAction
                ? LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_SELECTED, EzDataRebuildCatalog.Find(selectedAction)?.Name ?? selectedAction.ToString())
                : EzSettingsStrings.DATA_REBUILD_NOTHING_SELECTED;
        }

        /// <summary>
        /// One selectable maintenance node: name, what it feeds, and a selected-state background.
        /// </summary>
        internal partial class RebuildNodeCard : OsuClickableContainer
        {
            private readonly EzDataRebuildNode node;
            private readonly IBindable<EzDataRebuildAction?> selection;
            private readonly OsuColour colours;
            private readonly Box background;

            internal EzDataRebuildAction NodeAction => node.Action;

            public RebuildNodeCard(EzDataRebuildNode node, IBindable<EzDataRebuildAction?> selection, bool available, OsuColour colours)
            {
                this.node = node;
                this.selection = selection;
                this.colours = colours;

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
                            new OsuSpriteText
                            {
                                Text = node.Name,
                                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                Alpha = available ? 1 : 0.5f,
                            },
                            new OsuSpriteText
                            {
                                Text = node.Detail,
                                Font = OsuFont.Default.With(size: 12),
                                Colour = colours.GreySeaFoamLighter,
                                Alpha = available ? 1 : 0.5f,
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selection.BindValueChanged(e => updateBackground(e.NewValue), true);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (isDisposing)
                    selection.UnbindAll();
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (selection.Value != node.Action)
                    background.FadeColour(colours.GreySeaFoamDark, 150, Easing.OutQuint);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateBackground(selection.Value);
                base.OnHoverLost(e);
            }

            private void updateBackground(EzDataRebuildAction? current) => background.Colour = current == node.Action ? colours.Blue3 : colours.GreySeaFoamDarker;
        }
    }
}
