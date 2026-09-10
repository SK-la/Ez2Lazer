// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Composite skill + dan tag: e.g. <c>技 7++ (20.5)</c>.
    /// Optional secondary (player) dan + value for AB overlays.
    /// Supports <see cref="LayoutDirection"/> horizontal (default) or vertical (skill → dan → value).
    /// </summary>
    public partial class EzDisplaySkillsDan : FillFlowContainer
    {
        private readonly EzDisplaySkillName skillName;
        private readonly EzDisplayDan chartDan;
        private readonly OsuSpriteText chartValueText;
        private readonly EzDisplayDan playerDan;
        private readonly OsuSpriteText playerValueText;

        private FillDirection layoutDirection = FillDirection.Horizontal;
        private BeatmapInfo? beatmap;
        private bool beatmapBound;
        private int chartDanRequestId;

        public bool ShowValue { get; set; } = true;

        public bool PreferDanImage
        {
            get => chartDan.PreferImage;
            set
            {
                chartDan.PreferImage = value;
                playerDan.PreferImage = value;
            }
        }

        /// <summary>
        /// Horizontal: skill | dan | (value). Vertical: skill, dan, (value) stacked (dan in the middle).
        /// </summary>
        public FillDirection LayoutDirection
        {
            get => layoutDirection;
            set
            {
                if (layoutDirection == value)
                    return;

                layoutDirection = value;
                applyLayout();
            }
        }

        /// <summary>
        /// When set, loads chart dan via <see cref="EzSkillProvider"/> (cached then async).
        /// Manual <see cref="Set(EzMinaSkillAxis, string?, double?, string?, double?, int, EzDanSide)"/> / <see cref="SetFrom"/> / <see cref="SetSkillset"/> still work without this.
        /// </summary>
        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap != null && beatmap.Equals(value))
                    return;

                beatmap = value;
                chartDanRequestId++;

                if (value != null)
                {
                    beatmapBound = true;
                    scheduleBeatmapUpdate();
                    return;
                }

                // Clearing an active Beatmap binding (panel FreeAfterUse). Manual Set chips stay untouched.
                if (beatmapBound)
                {
                    Clear();
                    Hide();
                    BypassAutoSizeAxes = Axes.Both;
                    beatmapBound = false;
                }
            }
        }

        [Resolved(canBeNull: true)]
        private EzSkillProvider? skillProvider { get; set; }

        public EzDisplaySkillsDan()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;

            Children = new Drawable[]
            {
                skillName = new EzDisplaySkillName(),
                chartDan = new EzDisplayDan
                {
                    BadgeSize = 22,
                    PreferImage = true,
                },
                chartValueText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.White.Opacity(0.75f),
                    Alpha = 0,
                },
                playerDan = new EzDisplayDan
                {
                    BadgeSize = 20,
                    PreferImage = true,
                    Alpha = 0,
                },
                playerValueText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.FromHex("#50dc78").Opacity(0.9f),
                    Alpha = 0,
                },
            };

            applyLayout();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only refresh when Beatmap binding is in use — wedge chips use Set/SetSkillset only.
            if (beatmap != null)
                scheduleBeatmapUpdate();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                chartDanRequestId++;
                beatmap = null;
                beatmapBound = false;
            }
        }

        public new void Clear()
        {
            Alpha = 0;
        }

        public void SetFrom(EzChartDanVerdict verdict)
        {
            // Aggregate chart verdict keeps its RC/LN side for badge art.
            Set(verdict.DominantAxis, verdict.Label, verdict.OverallMsd, null, null, verdict.KeyCount, verdict.Side);
        }

        public void Set(EzMinaSkillAxis axis, string danLabel, double? overallMsd, int keyCount, EzDanSide side)
            => Set(axis, danLabel, overallMsd, null, null, keyCount, side);

        /// <summary>
        /// Chart (primary) and optional player (secondary) values — Skill-radar AB style.
        /// For Mina per-skill dans, pass <see cref="EzDanSide.Rc"/> so badge art uses reform / keymode RC ladders (hub parseDan), not LN 1–17.
        /// </summary>
        public void Set(
            EzMinaSkillAxis axis,
            string? chartLabel,
            double? chartValue,
            string? playerLabel,
            double? playerValue,
            int keyCount,
            EzDanSide side)
        {
            skillName.SetFromAxis(axis);

            bool hasChart = !string.IsNullOrEmpty(chartLabel) && chartValue is > 0;
            bool hasPlayer = !string.IsNullOrEmpty(playerLabel) && playerValue is > 0;

            if (!hasChart && !hasPlayer)
            {
                Alpha = 0;
                return;
            }

            if (hasChart)
            {
                chartDan.SetLabel(chartLabel!, keyCount, side);
                chartDan.Show();
                setValueText(chartValueText, chartValue);
            }
            else
            {
                chartDan.Hide();
                chartValueText.Alpha = 0;
            }

            if (hasPlayer)
            {
                playerDan.SetLabel(playerLabel!, keyCount, side);
                playerDan.Show();
                setValueText(playerValueText, playerValue);
            }
            else
            {
                playerDan.Hide();
                playerValueText.Alpha = 0;
            }

            BypassAutoSizeAxes = Axes.None;
            Alpha = 1;
        }

        /// <summary>
        /// Hub skillset tile: always shows the slot name; dan badges only when labels are present (no SrToRawDan).
        /// </summary>
        public void SetSkillset(
            EzDanSkillsetSlot slot,
            string? chartLabel,
            string? playerLabel,
            int? playerClears,
            int keyCount,
            EzDanSide side)
        {
            skillName.Set(slot.DisplayName, slot.AccentHex);

            if (!string.IsNullOrEmpty(chartLabel))
            {
                chartDan.SetLabel(chartLabel, keyCount, side);
                chartDan.Show();
            }
            else
            {
                chartDan.Hide();
            }

            chartValueText.Alpha = 0;
            chartValueText.Text = string.Empty;

            if (!string.IsNullOrEmpty(playerLabel))
            {
                playerDan.SetLabel(playerLabel, keyCount, side);
                playerDan.Show();

                if (ShowValue && playerClears is int clears && clears > 0)
                {
                    playerValueText.Text = $"({clears.ToString(CultureInfo.InvariantCulture)})";
                    playerValueText.Alpha = 1;
                }
                else
                {
                    playerValueText.Text = string.Empty;
                    playerValueText.Alpha = 0;
                }
            }
            else
            {
                playerDan.Hide();
                playerValueText.Text = string.Empty;
                playerValueText.Alpha = 0;
            }

            BypassAutoSizeAxes = Axes.None;
            Alpha = 1;
        }

        private void applyLayout()
        {
            Direction = layoutDirection;

            bool vertical = layoutDirection == FillDirection.Vertical;
            Spacing = vertical ? new Vector2(0, 2) : new Vector2(4, 0);

            // FillFlow requires all children to share the same RelativeAnchorPosition on the flow axis.
            var childAnchor = vertical ? Anchor.TopCentre : Anchor.CentreLeft;

            foreach (var child in Children)
            {
                child.Anchor = childAnchor;
                child.Origin = childAnchor;
            }
        }

        private void scheduleBeatmapUpdate()
        {
            if (!IsLoaded)
                return;

            updateFromBeatmap();
        }

        private void updateFromBeatmap()
        {
            if (beatmap == null)
                return;

            var cached = skillProvider?.TryGetCachedChartDan(beatmap);

            if (cached != null)
            {
                showFromVerdict(cached);
            }
            else if (skillProvider != null)
            {
                Clear();
                Hide();
                BypassAutoSizeAxes = Axes.Both;
                requestChartDanCompute(beatmap);
            }
            else
            {
                Clear();
                Hide();
                BypassAutoSizeAxes = Axes.Both;
            }
        }

        private void showFromVerdict(EzChartDanVerdict verdict)
        {
            BypassAutoSizeAxes = Axes.None;
            SetFrom(verdict);
            Show();
        }

        private void requestChartDanCompute(BeatmapInfo target)
        {
            int requestId = ++chartDanRequestId;
            var provider = skillProvider;
            // Detach so MinaCalc can run off the update thread without touching live Realm.
            var detached = target.Detach();

            Task.Run(() =>
            {
                try
                {
                    return provider?.TryGetChartDan(detached);
                }
                catch
                {
                    return null;
                }
            }).ContinueWith(t => Schedule(() =>
            {
                if (requestId != chartDanRequestId || beatmap == null || !string.Equals(beatmap.Hash, detached.Hash, StringComparison.Ordinal))
                    return;

                if (t.Status == TaskStatus.RanToCompletion && t.GetResultSafely() is EzChartDanVerdict verdict)
                    showFromVerdict(verdict);
                else
                {
                    Clear();
                    Hide();
                    BypassAutoSizeAxes = Axes.Both;
                }
            }));
        }

        private void setValueText(OsuSpriteText text, double? value)
        {
            if (ShowValue && value is double v && double.IsFinite(v) && v > 0)
            {
                text.Text = $"({v.ToString("0.0", CultureInfo.InvariantCulture)})";
                text.Alpha = 1;
            }
            else
            {
                text.Text = string.Empty;
                text.Alpha = 0;
            }
        }
    }
}
