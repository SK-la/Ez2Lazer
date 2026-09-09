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
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One RC/LN side: label + optional aggregate dans + full radar-axis <see cref="EzDisplaySkillsDan"/> + clear evidence.
    /// </summary>
    public partial class EzDanLabeledStatList : CompositeDrawable
    {
        /// <summary>Max clear-evidence cards under this side (player clears only).</summary>
        private const int clears_top_n = 15;

        private float displayScale => 1.5f;

        private readonly EzDanSide side;
        private readonly Colour4 accent;

        private OsuSpriteText sideLabel = null!;
        private EzDisplayDan chartAggregateDan = null!;
        private EzDisplayDan playerAggregateDan = null!;
        private FillFlowContainer cellsFlow = null!;
        private FillFlowContainer evidenceFlow = null!;
        private OsuSpriteText emptyEvidence = null!;
        private Container evidenceSection = null!;

        private FillDirection cellsDirection = FillDirection.Horizontal;
        private readonly LayoutValue sizeLayout = new LayoutValue(Invalidation.DrawSize);

        private readonly Dictionary<EzMinaSkillAxis, EzDisplaySkillsDan> axisChips = new Dictionary<EzMinaSkillAxis, EzDisplaySkillsDan>();

        public EzDanLabeledStatList(EzDanSide side, Colour4 accent)
        {
            this.side = side;
            this.accent = accent;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            AddLayout(sizeLayout);
        }

        public void SetLayoutInset(MarginPadding padding) => Padding = padding;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
            {
                axisChips[axis] = new EzDisplaySkillsDan
                {
                    PreferDanImage = true,
                    ShowValue = true,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Scale = new Vector2(displayScale),
                    Alpha = 0,
                };
            }

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

            foreach (var chip in axisChips.Values)
                cellsFlow.Add(chip);

            sideLabel.Text = side == EzDanSide.Ln
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
            IReadOnlyDictionary<string, double> chartAxes,
            IReadOnlyDictionary<string, double> playerAxes,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, string> resolveTitle,
            Action<EzDanClearEvidenceRow>? onSelectClear,
            bool showEvidence)
        {
            if (LoadState < LoadState.Ready)
            {
                Schedule(() => UpdateContent(
                    keyCount, chartAggregateLabel, playerAggregateLabel,
                    chartAxes, playerAxes, clears, resolveTitle, onSelectClear, showEvidence));
                return;
            }

            cellsFlow.Direction = cellsDirection;

            sideLabel.Text = side == EzDanSide.Ln
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC;

            setAggregate(chartAggregateDan, chartAggregateLabel, keyCount, side);
            setAggregate(playerAggregateDan, playerAggregateLabel, keyCount, side);

            var ladder = EzDanLadders.For(keyCount, side);

            foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
            {
                var chip = axisChips[axis];
                double chartValue = resolveAxisValue(chartAxes, axis);
                double playerValue = resolveAxisValue(playerAxes, axis);

                string? chartLabel = chartValue > 0 ? ladder.ParseLabel(EzDanLabels.SrToRawDan(chartValue, axis)) : null;
                string? playerLabel = playerValue > 0 ? ladder.ParseLabel(EzDanLabels.SrToRawDan(playerValue, axis)) : null;

                chip.Set(axis, chartLabel, chartValue > 0 ? chartValue : null, playerLabel, playerValue > 0 ? playerValue : null, keyCount, side);
            }

            if (!showEvidence)
            {
                evidenceSection.Hide();
                return;
            }

            evidenceSection.Show();
            rebuildEvidence(clears, resolveTitle, onSelectClear);
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

        private static double resolveAxisValue(IReadOnlyDictionary<string, double> values, EzMinaSkillAxis axis)
        {
            if (values.TryGetValue(axis.ToMsdSkillId(), out double msd) && msd > 0)
                return msd;
            if (values.TryGetValue(axis.ToSsrSkillId(), out double ssr) && ssr > 0)
                return ssr;
            if (values.TryGetValue(axis.ToId(), out double bare) && bare > 0)
                return bare;

            return 0;
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
