// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One RC/LN side as a fixed 3-column grid: label | player | chart.
    /// Skill rows stack vertically. Chart labels stay hub filing stamps (not per-axis chart dan).
    /// Clear score lists live on Local Profile, not here.
    /// </summary>
    public partial class EzDanLabeledStatList : CompositeDrawable
    {
        private const float display_scale = 1.5f;
        private const float row_height = 28f;

        private static readonly Dimension[] column_dimensions =
        {
            new Dimension(GridSizeMode.Relative, size: 0.38f),
            new Dimension(GridSizeMode.Relative, size: 0.31f),
            new Dimension(GridSizeMode.Relative, size: 0.31f),
        };

        private readonly Colour4 accent;

        /// <summary>RC or LN column this list represents.</summary>
        public EzDanSide Side { get; }

        private GridContainer grid = null!;
        private OsuSpriteText sideLabel = null!;
        private EzDisplayDan playerAggregateDan = null!;
        private EzDisplayDan chartAggregateDan = null!;

        private readonly Dictionary<string, SkillsetGridRow> rows = new Dictionary<string, SkillsetGridRow>(StringComparer.Ordinal);

        public EzDanLabeledStatList(EzDanSide side, Colour4 accent)
        {
            this.Side = side;
            this.accent = accent;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        public void SetLayoutInset(MarginPadding padding) => Padding = padding;

        [BackgroundDependencyLoader]
        private void load()
        {
            sideLabel = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                Colour = accent,
                Text = Side == EzDanSide.Ln
                    ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN
                    : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC,
            };

            // Player column before chart — DualPanel is player-primary.
            playerAggregateDan = new EzDisplayDan
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                BadgeSize = 18,
                PreferImage = true,
                Scale = new Vector2(display_scale),
                Alpha = 0,
            };

            chartAggregateDan = new EzDisplayDan
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                BadgeSize = 20,
                PreferImage = true,
                Scale = new Vector2(display_scale),
                Alpha = 0,
            };

            InternalChild = grid = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = column_dimensions,
                RowDimensions = [new Dimension(GridSizeMode.Absolute, row_height)],
                Content = new[]
                {
                    new Drawable[]
                    {
                        sideLabel,
                        playerAggregateDan,
                        chartAggregateDan,
                    },
                },
            };
        }

        public void UpdateContent(
            int keyCount,
            string? chartAggregateLabel,
            string? playerAggregateLabel,
            IReadOnlyList<EzDanSkillsetSlot> slots,
            IReadOnlyDictionary<string, string> chartLabelsBySkillset,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets,
            bool showClearCounts = false)
        {
            if (LoadState < LoadState.Ready)
            {
                Schedule(() => UpdateContent(
                    keyCount, chartAggregateLabel, playerAggregateLabel,
                    slots, chartLabelsBySkillset, playerSkillsets, showClearCounts));
                return;
            }

            sideLabel.Text = Side == EzDanSide.Ln
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC;

            setAggregate(playerAggregateDan, playerAggregateLabel, keyCount, Side);
            setAggregate(chartAggregateDan, chartAggregateLabel, keyCount, Side);

            rebuildRows(keyCount, slots, chartLabelsBySkillset, playerSkillsets, showClearCounts);
        }

        private void rebuildRows(
            int keyCount,
            IReadOnlyList<EzDanSkillsetSlot> slots,
            IReadOnlyDictionary<string, string> chartLabelsBySkillset,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets,
            bool showClearCounts)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            var content = new List<Drawable[]>
            {
                new Drawable[] { sideLabel, playerAggregateDan, chartAggregateDan },
            };
            var rowDims = new List<Dimension> { new Dimension(GridSizeMode.Absolute, row_height) };

            foreach (var slot in slots)
            {
                keep.Add(slot.Id);

                if (!rows.TryGetValue(slot.Id, out var row))
                {
                    row = new SkillsetGridRow(display_scale);
                    rows[slot.Id] = row;
                }

                chartLabelsBySkillset.TryGetValue(slot.Id, out string? chartLabel);

                string? playerLabel = null;
                int? playerClears = null;

                if (playerSkillsets.TryGetValue(slot.Id, out var verdict))
                {
                    playerLabel = verdict.Label;
                    if (showClearCounts)
                        playerClears = verdict.Clears;
                }

                row.Update(slot, playerLabel, chartLabel, playerClears, showClearCounts, keyCount, Side);
                content.Add(row.Cells);
                rowDims.Add(new Dimension(GridSizeMode.Absolute, row_height));
            }

            foreach (string id in rows.Keys.Where(id => !keep.Contains(id)).ToList())
                rows.Remove(id);

            grid.RowDimensions = rowDims.ToArray();
            grid.Content = content.ToArray();
        }

        private static void setAggregate(EzDisplayDan display, string? label, int keyCount, EzDanSide side)
        {
            if (!string.IsNullOrEmpty(label))
            {
                display.SetLabel(label, keyCount, side);
                display.Show();
            }
            else
            {
                display.Hide();
            }
        }

        /// <summary>One skillset row: name | player dan | chart dan (hub stamp).</summary>
        private partial class SkillsetGridRow
        {
            public Drawable[] Cells { get; }

            private readonly EzDisplaySkillName name;
            private readonly EzDisplayDan playerDan;
            private readonly OsuSpriteText playerClearsText;
            private readonly EzDisplayDan chartDan;
            private readonly Container playerCell;

            public SkillsetGridRow(float displayScale)
            {
                name = new EzDisplaySkillName
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                playerDan = new EzDisplayDan
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BadgeSize = 20,
                    PreferImage = true,
                    Scale = new Vector2(displayScale),
                    Alpha = 0,
                };

                playerClearsText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.FromHex("#50dc78").Opacity(0.9f),
                    Alpha = 0,
                };

                // Horizontal FillFlow: all children CentreLeft (same RelativeAnchorPosition.X).
                playerCell = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            playerDan,
                            playerClearsText,
                        },
                    },
                };

                chartDan = new EzDisplayDan
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BadgeSize = 22,
                    PreferImage = true,
                    Scale = new Vector2(displayScale),
                    Alpha = 0,
                };

                Cells = new Drawable[] { name, playerCell, chartDan };
            }

            public void Update(
                EzDanSkillsetSlot slot,
                string? playerLabel,
                string? chartLabel,
                int? playerClears,
                bool showClearCounts,
                int keyCount,
                EzDanSide side)
            {
                name.Set(slot.DisplayName, slot.AccentHex);

                if (!string.IsNullOrEmpty(playerLabel))
                {
                    playerDan.SetLabel(playerLabel, keyCount, side);
                    playerDan.Show();

                    if (showClearCounts && playerClears is int clears && clears > 0)
                    {
                        playerClearsText.Text = $"({clears.ToString(CultureInfo.InvariantCulture)})";
                        playerClearsText.Show();
                    }
                    else
                    {
                        playerClearsText.Text = string.Empty;
                        playerClearsText.Hide();
                    }
                }
                else
                {
                    playerDan.Hide();
                    playerClearsText.Text = string.Empty;
                    playerClearsText.Hide();
                }

                if (!string.IsNullOrEmpty(chartLabel))
                {
                    chartDan.SetLabel(chartLabel, keyCount, side);
                    chartDan.Show();
                }
                else
                {
                    chartDan.Hide();
                }
            }
        }
    }
}
