// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One RC/LN side: aggregate dans + hub skillset chips (slots from <see cref="EzDanSkillsetBuckets"/>) + clear evidence.
    /// UI never invents per-skill dans via <c>SrToRawDan</c> — only Provider verdicts / chart labels.
    /// </summary>
    public partial class EzDanLabeledStatList : CompositeDrawable
    {
        /// <summary>Max clear-evidence cards under this side (player clears only).</summary>
        private const int clears_top_n = 15;

        private float displayScale => 1.5f;

        private readonly Colour4 accent;

        /// <summary>RC or LN column this list represents.</summary>
        public EzDanSide Side { get; }

        private OsuSpriteText sideLabel = null!;
        private EzDisplayDan chartAggregateDan = null!;
        private EzDisplayDan playerAggregateDan = null!;
        private FillFlowContainer cellsFlow = null!;
        private FillFlowContainer evidenceFlow = null!;
        private OsuSpriteText emptyEvidence = null!;
        private Container evidenceSection = null!;

        private FillDirection cellsDirection = FillDirection.Horizontal;
        private readonly LayoutValue sizeLayout = new LayoutValue(Invalidation.DrawSize);

        private readonly Dictionary<string, EzDisplaySkillsDan> skillsetChips = new Dictionary<string, EzDisplaySkillsDan>(StringComparer.Ordinal);

        public EzDanLabeledStatList(EzDanSide side, Colour4 accent)
        {
            this.Side = side;
            this.accent = accent;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            AddLayout(sizeLayout);
        }

        public void SetLayoutInset(MarginPadding padding) => Padding = padding;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            sideLabel = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                Colour = accent,
                            },
                            chartAggregateDan = new EzDisplayDan
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                BadgeSize = 20,
                                PreferImage = true,
                                Scale = new Vector2(displayScale),
                                Alpha = 0,
                            },
                            playerAggregateDan = new EzDisplayDan
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                BadgeSize = 18,
                                PreferImage = true,
                                Scale = new Vector2(displayScale),
                                Alpha = 0,
                            },
                        },
                    },
                    cellsFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = cellsDirection,
                        Spacing = new Vector2(10, 8),
                    },
                    evidenceSection = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            emptyEvidence = new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Font = OsuFont.GetFont(size: 12),
                                Colour = colours.Content2,
                                Text = EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_EMPTY,
                                Alpha = 0,
                            },
                            evidenceFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(8),
                            },
                        },
                    },
                },
            };

            sideLabel.Text = Side == EzDanSide.Ln
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC;
        }

        public void SetCellsDirection(FillDirection direction)
        {
            cellsDirection = direction;

            if (LoadState >= LoadState.Ready)
                cellsFlow.Direction = direction;
        }

        public void UpdateContent(
            int keyCount,
            string? chartAggregateLabel,
            string? playerAggregateLabel,
            IReadOnlyList<EzDanSkillsetSlot> slots,
            IReadOnlyDictionary<string, string> chartLabelsBySkillset,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, string> resolveTitle,
            Action<EzDanClearEvidenceRow>? onSelectClear,
            bool showEvidence)
        {
            if (LoadState < LoadState.Ready)
            {
                Schedule(() => UpdateContent(
                    keyCount, chartAggregateLabel, playerAggregateLabel,
                    slots, chartLabelsBySkillset, playerSkillsets,
                    clears, resolveTitle, onSelectClear, showEvidence));
                return;
            }

            cellsFlow.Direction = cellsDirection;

            sideLabel.Text = Side == EzDanSide.Ln
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC;

            setAggregate(chartAggregateDan, chartAggregateLabel, keyCount, Side);
            setAggregate(playerAggregateDan, playerAggregateLabel, keyCount, Side);

            rebuildSkillsetChips(keyCount, slots, chartLabelsBySkillset, playerSkillsets);

            if (!showEvidence)
            {
                evidenceSection.Hide();
                return;
            }

            evidenceSection.Show();
            rebuildEvidence(clears, resolveTitle, onSelectClear);
        }

        private void rebuildSkillsetChips(
            int keyCount,
            IReadOnlyList<EzDanSkillsetSlot> slots,
            IReadOnlyDictionary<string, string> chartLabelsBySkillset,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> playerSkillsets)
        {
            if (slots.Count == 0)
            {
                cellsFlow.Clear();
                skillsetChips.Clear();
                cellsFlow.Hide();
                return;
            }

            cellsFlow.Show();

            var keep = new HashSet<string>(StringComparer.Ordinal);

            foreach (var slot in slots)
            {
                keep.Add(slot.Id);

                if (!skillsetChips.TryGetValue(slot.Id, out var chip))
                {
                    chip = new EzDisplaySkillsDan
                    {
                        PreferDanImage = true,
                        ShowValue = true,
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Scale = new Vector2(displayScale),
                    };
                    skillsetChips[slot.Id] = chip;
                    cellsFlow.Add(chip);
                }

                chartLabelsBySkillset.TryGetValue(slot.Id, out string? chartLabel);

                string? playerLabel = null;
                int? playerClears = null;

                if (playerSkillsets.TryGetValue(slot.Id, out var verdict))
                {
                    playerLabel = verdict.Label;
                    playerClears = verdict.Clears;
                }

                chip.SetSkillset(slot, chartLabel, playerLabel, playerClears, keyCount, Side);
            }

            foreach (string id in skillsetChips.Keys.Where(id => !keep.Contains(id)).ToList())
            {
                cellsFlow.Remove(skillsetChips[id], true);
                skillsetChips.Remove(id);
            }

            // Keep visual order = slots order.
            for (int i = 0; i < slots.Count; i++)
            {
                var chip = skillsetChips[slots[i].Id];
                if (cellsFlow.GetLayoutPosition(chip) != i)
                    cellsFlow.SetLayoutPosition(chip, i);
            }
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

        private void rebuildEvidence(
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, string> resolveTitle,
            Action<EzDanClearEvidenceRow>? onSelectClear)
        {
            evidenceFlow.Clear();

            var top = clears.Take(clears_top_n).ToList();

            if (top.Count == 0)
            {
                emptyEvidence.Show();
                evidenceFlow.Hide();
                return;
            }

            emptyEvidence.Hide();
            evidenceFlow.Show();

            float colWidth = resolveEvidenceColumnWidth();

            foreach (var clear in top)
            {
                evidenceFlow.Add(new EzEvidenceScoreRow(
                    resolveTitle(clear.BeatmapHash),
                    EzEvidenceScoreRow.FormatDanMeta(clear.CreditedDan, clear.Accuracy, clear.Rate, clear.ScoredAt),
                    onSelectClear != null ? () => onSelectClear(clear) : null)
                {
                    Width = colWidth,
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.Y,
                });
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!sizeLayout.IsValid)
            {
                sizeLayout.Validate();
                reflowEvidenceWidths();
            }
        }

        private void reflowEvidenceWidths()
        {
            float colWidth = resolveEvidenceColumnWidth();

            foreach (var child in evidenceFlow)
                child.Width = colWidth;
        }

        private float resolveEvidenceColumnWidth()
        {
            float width = DrawWidth;
            if (width <= 0)
                width = 400;

            int cols = width switch
            {
                < 280 => 1,
                < 420 => 2,
                < 560 => 3,
                _ => 4,
            };

            const float gap = 8f;
            return Math.Max(80, (width - gap * (cols - 1)) / cols);
        }
    }
}
