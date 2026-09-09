// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Composite skill + dan tag: e.g. <c>技 7++ (20.5)</c>.
    /// Optional secondary (player) dan + value for AB overlays.
    /// </summary>
    public partial class EzDisplaySkillsDan : FillFlowContainer
    {
        private readonly EzDisplaySkillName skillName;
        private readonly EzDisplayDan chartDan;
        private readonly OsuSpriteText chartValueText;
        private readonly EzDisplayDan playerDan;
        private readonly OsuSpriteText playerValueText;

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

        public EzDisplaySkillsDan()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(4, 0);
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;

            Children = new Drawable[]
            {
                skillName = new EzDisplaySkillName
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                chartDan = new EzDisplayDan
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BadgeSize = 22,
                    PreferImage = true,
                },
                chartValueText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.White.Opacity(0.75f),
                    Alpha = 0,
                },
                playerDan = new EzDisplayDan
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BadgeSize = 20,
                    PreferImage = true,
                    Alpha = 0,
                },
                playerValueText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.FromHex("#50dc78").Opacity(0.9f),
                    Alpha = 0,
                },
            };
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

            Alpha = 1;
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
