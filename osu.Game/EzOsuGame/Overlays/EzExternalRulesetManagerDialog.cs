// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.ExternalRulesets;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzExternalRulesetManagerDialog : OsuFocusedOverlayContainer
    {
        private const float row_height = 44;
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private readonly List<ManagerRow> rows = new List<ManagerRow>();

        private FillFlowContainer rowFlow = null!;
        private OsuSpriteText emptyHint = null!;
        private OsuColour colours = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        public EzExternalRulesetManagerDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.55f, 0.72f);

            Masking = true;
            CornerRadius = 10;
        }

        public void ShowManager()
        {
            if (IsLoaded)
                refreshEntries();

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
                            new Drawable[]
                            {
                                new Container
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
                                                    Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_HEADER,
                                                    Font = OsuFont.GetFont(size: 28),
                                                },
                                                new OsuSpriteText
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_BODY,
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
                                },
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Horizontal = 12 },
                                    Masking = true,
                                    CornerRadius = 10,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.GreySeaFoamDarker,
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding(10),
                                            Children = new Drawable[]
                                            {
                                                emptyHint = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_EMPTY,
                                                    Font = OsuFont.Default.With(size: 16),
                                                    Colour = colours.Gray5,
                                                    Alpha = 0,
                                                },
                                                new OsuScrollContainer
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Child = rowFlow = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(0, 8),
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(12),
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_RESTART_HINT,
                                                Font = OsuFont.Default.With(size: 13),
                                                Colour = colours.Yellow,
                                            },
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                RowDimensions = new[]
                                                {
                                                    new Dimension(GridSizeMode.AutoSize),
                                                },
                                                ColumnDimensions = new[]
                                                {
                                                    new Dimension(),
                                                    new Dimension(),
                                                    new Dimension(),
                                                },
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 40,
                                                            Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_OPEN_FOLDER,
                                                            Action = openRulesetsFolder,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 40,
                                                            Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_SAVE,
                                                            Action = save,
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
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshEntries();
        }

        protected override void PopIn()
        {
            refreshEntries();
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(1, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.95f, exit_duration, Easing.OutQuint);
        }

        private void refreshEntries()
        {
            rows.Clear();

            var discovered = EzExternalRulesetScanner.Scan(storage);
            var config = EzRulesetMappingConfig.Load(storage);
            config.EnsureDefaults(discovered);

            foreach (var ruleset in discovered)
            {
                var entry = config.GetOrAdd(ruleset.ShortName);
                bool hasExplicitId = EzExternalRulesetMapping.HasExplicitOnlineId(ruleset.InstanceOnlineId)
                                     || entry.OnlineID is int iniId && iniId >= EzExternalRulesetMapping.EXPLICIT_ONLINE_ID_MINIMUM;

                rows.Add(new ManagerRow(
                    ruleset,
                    new BindableBool(entry.Enabled),
                    hasExplicitId,
                    hasExplicitId ? entry.OnlineID ?? ruleset.InstanceOnlineId : null,
                    entry.Order ?? int.MaxValue));
            }

            rows.Sort((a, b) =>
            {
                if (a.HasExplicitId != b.HasExplicitId)
                    return a.HasExplicitId ? -1 : 1;

                int orderCompare = a.Order.CompareTo(b.Order);

                if (orderCompare != 0)
                    return orderCompare;

                return string.Compare(a.Ruleset.ShortName, b.Ruleset.ShortName, StringComparison.Ordinal);
            });

            normaliseExplicitOrder();
            rebuildRowFlow();
        }

        private void rebuildRowFlow()
        {
            rowFlow.Clear();

            foreach (var row in rows)
                rowFlow.Add(createRowDrawable(row));

            emptyHint.Alpha = rows.Count == 0 ? 1 : 0;
        }

        private Drawable createRowDrawable(ManagerRow row)
        {
            // Row shell: left label + right controls (not FillFlow left/right mix).
            var controlsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8),
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            };

            if (row.HasExplicitId)
            {
                controlsFlow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = $"ID {row.OnlineId}",
                    Font = OsuFont.Default.With(weight: FontWeight.Bold),
                });

                controlsFlow.Add(createMoveButton(FontAwesome.Solid.ArrowUp, () => moveExplicitRow(row, -1)));
                controlsFlow.Add(createMoveButton(FontAwesome.Solid.ArrowDown, () => moveExplicitRow(row, 1)));
            }
            else
            {
                controlsFlow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = EzSettingsStrings.EXTERNAL_RULESET_NO_DEFINED_ID,
                    Font = OsuFont.Default.With(size: 14),
                    Colour = colours.Gray5,
                });
            }

            controlsFlow.Add(new OsuCheckbox
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                LabelText = EzSettingsStrings.EXTERNAL_RULESET_ENABLED,
                Current = { BindTarget = row.Enabled },
            });

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = row_height,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = $"{row.Ruleset.Name} ({row.Ruleset.ShortName})",
                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.Medium),
                    },
                    controlsFlow,
                },
            };
        }

        private IconButton createMoveButton(IconUsage icon, Action action)
        {
            return new IconButton
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Icon = icon,
                Scale = new Vector2(0.75f),
                Action = action,
            };
        }

        private void moveExplicitRow(ManagerRow row, int delta)
        {
            var explicitRows = rows.Where(r => r.HasExplicitId).ToList();
            int index = explicitRows.IndexOf(row);

            if (index < 0)
                return;

            int newIndex = index + delta;

            if (newIndex < 0 || newIndex >= explicitRows.Count)
                return;

            var other = explicitRows[newIndex];
            (row.Order, other.Order) = (other.Order, row.Order);

            rows.Sort((a, b) =>
            {
                if (a.HasExplicitId != b.HasExplicitId)
                    return a.HasExplicitId ? -1 : 1;

                return a.Order.CompareTo(b.Order);
            });

            rebuildRowFlow();
        }

        private void normaliseExplicitOrder()
        {
            int order = 0;

            foreach (var row in rows.Where(r => r.HasExplicitId))
                row.Order = order++;
        }

        private void openRulesetsFolder()
        {
            string path = storage.GetStorageForDirectory(@"rulesets").GetFullPath(@".");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_OPEN_FOLDER_FAILED,
                });
            }
        }

        private void save()
        {
            var config = EzRulesetMappingConfig.Load(storage);

            int explicitOrder = 0;

            foreach (var row in rows)
            {
                var entry = config.GetOrAdd(row.Ruleset.ShortName);
                entry.Enabled = row.Enabled.Value;

                if (row.HasExplicitId)
                {
                    entry.Order = explicitOrder++;
                    entry.OnlineID = row.OnlineId;
                }
                else
                {
                    entry.Order = null;
                    entry.OnlineID = null;
                }
            }

            config.Save(storage);
            notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_SAVED });
            Hide();
        }

        private sealed class ManagerRow
        {
            public DiscoveredExternalRuleset Ruleset { get; }
            public BindableBool Enabled { get; }
            public bool HasExplicitId { get; }
            public int? OnlineId { get; }
            public int Order { get; set; }

            public ManagerRow(DiscoveredExternalRuleset ruleset, BindableBool enabled, bool hasExplicitId, int? onlineId, int order)
            {
                Ruleset = ruleset;
                Enabled = enabled;
                HasExplicitId = hasExplicitId;
                OnlineId = onlineId;
                Order = order;
            }
        }
    }
}
