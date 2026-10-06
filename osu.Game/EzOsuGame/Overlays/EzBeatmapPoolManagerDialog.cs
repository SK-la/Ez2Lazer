// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.BeatmapPools;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzBeatmapPoolManagerDialog : OsuFocusedOverlayContainer
    {
        private static readonly string[] managed_short_names = { "osu", "mania", "taiko", "catch" };

        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private OsuColour colours = null!;
        private FillFlowContainer rulesetFlow = null!;
        private FillFlowContainer pathFlow = null!;
        private OsuTextBox pathInput = null!;
        private OsuSpriteText emptyHint = null!;
        private RoundedButton rebuildButton = null!;
        private Container providerHost = null!;

        private RulesetInfo? selectedRuleset;
        private RulesetBeatmapPoolProvider? activeProvider;
        private CancellationTokenSource? rebuildCancellation;

        [Resolved]
        private EzBeatmapPoolStore poolStore { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        public EzBeatmapPoolManagerDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.62f, 0.76f);

            Masking = true;
            CornerRadius = 10;
        }

        public void ShowManager()
        {
            if (IsLoaded)
                ensureSelection();

            Show();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour loadedColours)
        {
            colours = loadedColours;

            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
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
                        },
                        Content = new[]
                        {
                            new Drawable[] { createHeader() },
                            new Drawable[] { createBody() },
                            new Drawable[] { createFooter() },
                        },
                    },
                },
                providerHost = new Container { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            populateRulesets();
            ensureSelection();
        }

        protected override void PopIn()
        {
            ensureSelection();
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(1, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            rebuildCancellation?.Cancel();
            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.95f, exit_duration, Easing.OutQuint);
        }

        private Drawable createHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new FillFlowContainer
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
                            Text = EzSettingsStrings.BEATMAP_POOL_MANAGER_HEADER,
                            Font = OsuFont.GetFont(size: 28),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = EzSettingsStrings.BEATMAP_POOL_MANAGER_BODY,
                            Font = OsuFont.Default.With(size: 14),
                            Colour = colours.Yellow,
                        },
                    },
                },
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

        private Drawable createBody() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Horizontal = 12 },
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 160),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        createPanel(new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = rulesetFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 6),
                            },
                        }),
                        createPanel(new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(10),
                            Children = new Drawable[]
                            {
                                emptyHint = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = EzSettingsStrings.BEATMAP_POOL_NO_PATHS,
                                    Font = OsuFont.Default.With(size: 16),
                                    Colour = colours.Gray5,
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new OsuScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Child = pathFlow = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(0, 6),
                                                },
                                            },
                                        },
                                        new Drawable[]
                                        {
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                ColumnDimensions = new[]
                                                {
                                                    new Dimension(),
                                                    new Dimension(GridSizeMode.AutoSize),
                                                },
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        pathInput = new OsuTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 40,
                                                            PlaceholderText = EzSettingsStrings.BEATMAP_POOL_PATH_PLACEHOLDER,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 110,
                                                            Height = 40,
                                                            Margin = new MarginPadding { Left = 8 },
                                                            Text = EzSettingsStrings.BEATMAP_POOL_ADD_PATH,
                                                            Action = addPathFromInput,
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        }),
                    },
                },
            },
        };

        private Drawable createFooter() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding(12),
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        rebuildButton = new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Text = EzSettingsStrings.BEATMAP_POOL_REBUILD,
                            Action = startRebuild,
                        },
                        new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Text = EzSettingsStrings.CANCEL_BUTTON,
                            Action = Hide,
                        },
                    },
                },
            },
        };

        private Drawable createPanel(Drawable child) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 10,
            Margin = new MarginPadding { Horizontal = 4 },
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.GreySeaFoamDarker,
                },
                child,
            },
        };

        private void populateRulesets()
        {
            rulesetFlow.Clear();

            foreach (string shortName in managed_short_names)
            {
                var info = rulesetStore.AvailableRulesets.FirstOrDefault(r => string.Equals(r.ShortName, shortName, StringComparison.OrdinalIgnoreCase));

                if (info == null)
                    continue;

                string captured = shortName;
                rulesetFlow.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    Text = info.Name,
                    Action = () => selectRuleset(captured),
                });
            }
        }

        private void ensureSelection()
        {
            if (selectedRuleset != null)
            {
                refreshPaths();
                return;
            }

            selectRuleset(managed_short_names[0]);
        }

        private void selectRuleset(string shortName)
        {
            selectedRuleset = rulesetStore.AvailableRulesets.FirstOrDefault(r => string.Equals(r.ShortName, shortName, StringComparison.OrdinalIgnoreCase));

            if (activeProvider != null)
            {
                providerHost.Remove(activeProvider, true);
                activeProvider = null;
            }

            if (selectedRuleset != null)
            {
                try
                {
                    activeProvider = selectedRuleset.CreateInstance().CreateBeatmapPoolProvider();
                    providerHost.Add(activeProvider);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to create beatmap pool provider for {shortName}");
                    activeProvider = null;
                }
            }

            rebuildButton.Enabled.Value = activeProvider?.CanApply == true;
            refreshPaths();
        }

        private void refreshPaths()
        {
            pathFlow.Clear();

            if (selectedRuleset == null)
            {
                emptyHint.Alpha = 1;
                return;
            }

            IReadOnlyList<EzBeatmapPoolPath> paths = poolStore.GetPaths(selectedRuleset.ShortName);

            emptyHint.Alpha = paths.Count == 0 ? 1 : 0;

            foreach (EzBeatmapPoolPath path in paths)
            {
                string capturedPath = path.Path;
                var enabledBindable = new BindableBool(path.Enabled);
                enabledBindable.BindValueChanged(e => poolStore.SetEnabled(selectedRuleset.ShortName, 0, capturedPath, e.NewValue));

                pathFlow.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.GreySeaFoamDark.Opacity(0.5f),
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Horizontal = 8 },
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Absolute, 90),
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 40),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new OsuCheckbox
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.None,
                                        Width = 90,
                                        LabelText = EzSettingsStrings.EXTERNAL_RULESET_ENABLED,
                                        Current = { BindTarget = enabledBindable },
                                    },
                                    new TruncatingSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Text = capturedPath,
                                        Font = OsuFont.Default.With(size: 14),
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Icon = FontAwesome.Solid.Trash,
                                        Action = () =>
                                        {
                                            poolStore.RemovePath(selectedRuleset.ShortName, 0, capturedPath);
                                            refreshPaths();
                                        },
                                    },
                                },
                            },
                        },
                    },
                });
            }
        }

        private void addPathFromInput()
        {
            if (selectedRuleset == null)
                return;

            string path = pathInput.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(path))
                return;

            if (poolStore.AddPath(selectedRuleset.ShortName, 0, path))
            {
                pathInput.Text = string.Empty;
                refreshPaths();
            }
        }

        private void startRebuild()
        {
            if (selectedRuleset == null || activeProvider is not { CanApply: true } provider)
                return;

            rebuildCancellation?.Cancel();
            rebuildCancellation = new CancellationTokenSource();
            CancellationToken token = rebuildCancellation.Token;

            IReadOnlyList<string> configuredEnabled = poolStore.GetEnabledDirectories(selectedRuleset.ShortName, requireExisting: false);
            IReadOnlyList<string> existing = configuredEnabled.Where(Directory.Exists).ToArray();

            if (configuredEnabled.Count > 0 && existing.Count == 0)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = "请先添加至少一个存在的外部路径，或禁用全部路径后再清空。",
                });
                return;
            }

            var progress = new ProgressNotification
            {
                Text = EzSettingsStrings.BEATMAP_POOL_REBUILDING,
                State = ProgressNotificationState.Active,
                Progress = 0,
            };
            notifications?.Post(progress);

            rebuildButton.Enabled.Value = false;

            Task.Run(async () =>
            {
                try
                {
                    // Empty existing list clears all external sets for this ruleset.
                    await provider.ApplyAsync(0, existing, token).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        progress.Text = EzSettingsStrings.BEATMAP_POOL_REBUILD_DONE;
                        progress.State = ProgressNotificationState.Completed;
                        rebuildButton.Enabled.Value = true;
                    });
                }
                catch (OperationCanceledException)
                {
                    Schedule(() =>
                    {
                        progress.State = ProgressNotificationState.Cancelled;
                        rebuildButton.Enabled.Value = true;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "External beatmap pool rebuild failed");
                    Schedule(() =>
                    {
                        progress.Text = EzSettingsStrings.BEATMAP_POOL_REBUILD_FAILED;
                        progress.State = ProgressNotificationState.Cancelled;
                        rebuildButton.Enabled.Value = true;
                    });
                }
            }, token);
        }
    }
}
