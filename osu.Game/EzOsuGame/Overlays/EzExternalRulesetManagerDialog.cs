// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.ExternalRulesets;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzExternalRulesetManagerDialog : PopupDialog
    {
        private const float content_width = 520;
        private const float row_height = 40;

        private readonly List<ManagerRow> rows = new List<ManagerRow>();
        private FillFlowContainer rowFlow = null!;
        private OsuColour colours = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzExternalRulesetManagerDialog(Action onSaved)
        {
            Icon = FontAwesome.Solid.PuzzlePiece;
            HeaderText = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_HEADER;
            BodyText = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_BODY;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_SAVE,
                    Action = () => save(onSaved),
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsStrings.CANCEL_BUTTON,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour loadedColours)
        {
            colours = loadedColours;
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

            rowFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            rebuildRowFlow();

            MainContent.Child = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 12 },
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = content_width,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = Math.Min(280, Math.Max(row_height, rows.Count * (row_height + 6))),
                        Child = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = rowFlow,
                        },
                    },
                    new OsuSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_RESTART_HINT,
                        Font = OsuFont.Default.With(size: 14),
                        Colour = colours.Yellow,
                    },
                },
            };
        }

        private void rebuildRowFlow()
        {
            rowFlow.Clear();

            foreach (var row in rows)
                rowFlow.Add(createRowDrawable(row));
        }

        private Drawable createRowDrawable(ManagerRow row)
        {
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
                    Text = EzSettingsStrings.EXTERNAL_RULESET_NO_DEFINED_ID,
                    Font = OsuFont.Default.With(size: 14),
                    Colour = colours.Gray5,
                });
            }

            controlsFlow.Add(new OsuCheckbox
            {
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

        private void save(Action onSaved)
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
            onSaved();
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
