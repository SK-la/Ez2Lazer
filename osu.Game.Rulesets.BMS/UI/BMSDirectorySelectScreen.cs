// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// Screen for selecting BMS root directory.
    /// </summary>
    public partial class BMSDirectorySelectScreen : OsuScreen
    {
        private readonly Bindable<string> libraryPathsBindable;
        private readonly Bindable<string> legacyRootPathBindable;
        private readonly List<string> stagedPaths;
        private readonly Action<IReadOnlyList<string>>? applyAction;
        private OsuDirectorySelector directorySelector = null!;
        private FillFlowContainer pathList = null!;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public BMSDirectorySelectScreen(Bindable<string> libraryPathsBindable, Bindable<string> legacyRootPathBindable, Action<IReadOnlyList<string>>? applyAction = null)
        {
            this.libraryPathsBindable = libraryPathsBindable;
            this.legacyRootPathBindable = legacyRootPathBindable;
            this.applyAction = applyAction;
            stagedPaths = BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value).ToList();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            string? initialPath = stagedPaths.LastOrDefault();

            InternalChild = new Container
            {
                Masking = true,
                CornerRadius = 10,
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(0.7f, 0.8f),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                    Margin = new MarginPadding(10),
                                    Children = new Drawable[]
                                    {
                                        new TooltipTextFlowContainer
                                        {
                                            Text = BmsStrings.PATH_WIZARD_TITLE,
                                            TextAnchor = Anchor.TopCentre,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            TooltipText = BmsStrings.PATH_WIZARD_TOOLTIP,
                                        },
                                        new TooltipTextFlowContainer
                                        {
                                            Text = BmsStrings.PATH_WIZARD_INTRO,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            TooltipText = BmsStrings.PATH_WIZARD_INTRO_TOOLTIP,
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, 260),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            directorySelector = new OsuDirectorySelector(initialPath)
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                            }
                                        },
                                        new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                                Children = new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(10, 0),
                                                        Children = new Drawable[]
                                                        {
                                                            new RoundedButton
                                                            {
                                                                Width = 180,
                                                                Text = BmsStrings.PATH_WIZARD_ADD_CURRENT_PATH,
                                                                Action = addSelectedPath,
                                                            },
                                                            new RoundedButton
                                                            {
                                                                Width = 140,
                                                                Text = BmsStrings.PATH_WIZARD_CLEAR_LIST,
                                                                Action = () =>
                                                                {
                                                                    stagedPaths.Clear();
                                                                    refreshPathList();
                                                                },
                                                            },
                                                        },
                                                    },
                                                    new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 16))
                                                    {
                                                        Text = BmsStrings.PATH_WIZARD_ADDED_PATHS_HEADER,
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                    },
                                                    new OsuScrollContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Child = pathList = new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(0, 2),
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10),
                                    Padding = new MarginPadding(10),
                                    Children = new Drawable[]
                                    {
                                        new RoundedButton
                                        {
                                            Width = 200,
                                            Text = BmsStrings.PATH_WIZARD_CONFIRM,
                                            Action = this.Exit,
                                        },
                                        new RoundedButton
                                        {
                                            Width = 200,
                                            Text = BmsStrings.PATH_WIZARD_APPLY,
                                            Action = applyPaths,
                                        },
                                    }
                                }
                            }
                        }
                    }
                }
            };

            refreshPathList();
        }

        private void addSelectedPath()
        {
            string? selectedPath = directorySelector.CurrentPath.Value?.FullName;

            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
                return;

            if (stagedPaths.Any(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase)))
                return;

            stagedPaths.Add(selectedPath);
            refreshPathList();
        }

        private void applyPaths()
        {
            libraryPathsBindable.Value = BMSRulesetConfigManager.SerialiseLibraryPaths(stagedPaths);
            legacyRootPathBindable.Value = stagedPaths.FirstOrDefault() ?? string.Empty;
            applyAction?.Invoke(stagedPaths.ToArray());
        }

        private void refreshPathList()
        {
            pathList.Clear();

            if (stagedPaths.Count == 0)
            {
                pathList.Add(new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 14))
                {
                    Text = BmsStrings.PATH_WIZARD_NO_PATHS_YET,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                });
                return;
            }

            foreach (string path in stagedPaths)
            {
                pathList.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 10, Right = 6, Top = 6, Bottom = 6 },
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 92),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = path,
                                        Font = OsuFont.Default.With(size: 16),
                                    },
                                    new RoundedButton
                                    {
                                        Width = 86,
                                        Height = 24,
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Text = BmsStrings.PATH_WIZARD_REMOVE,
                                        Action = () => requestRemovePath(path),
                                    },
                                },
                            },
                        },
                    },
                });
            }
        }

        private void requestRemovePath(string path)
        {
            dialogOverlay?.Push(new RemovePathDialog(path, () =>
            {
                stagedPaths.Remove(path);
                refreshPathList();
            }));
        }

        private partial class RemovePathDialog : DangerousActionDialog
        {
            public RemovePathDialog(string path, Action onConfirm)
            {
                HeaderText = BmsStrings.PATH_WIZARD_REMOVE_DIALOG_HEADER;
                BodyText = BmsStrings.PathWizard_RemoveDialogBody(path);
                DangerousAction = onConfirm;
            }
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            this.FadeOut(250);
        }

        /// <summary>
        /// A text flow container that shows a tooltip on hover.
        /// </summary>
        private partial class TooltipTextFlowContainer : OsuTextFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; } = string.Empty;
        }
    }
}
