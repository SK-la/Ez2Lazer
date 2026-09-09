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
    /// Skill chip and dan container stay separate drawables.
    /// </summary>
    public partial class EzDisplaySkillsDan : FillFlowContainer
    {
        private readonly EzDisplaySkillName skillName;
        private readonly EzDisplayDan dan;
        private readonly OsuSpriteText valueText;

        public bool ShowValue { get; set; } = true;

        public bool PreferDanImage
        {
            get => dan.PreferImage;
            set => dan.PreferImage = value;
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
                dan = new EzDisplayDan
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BadgeSize = 22,
                    PreferImage = true,
                },
                valueText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Colour4.White.Opacity(0.75f),
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
            Set(verdict.DominantAxis, verdict.Label, verdict.OverallMsd, verdict.KeyCount, verdict.Side);
        }

        public void Set(EzMinaSkillAxis axis, string danLabel, double? overallMsd, int keyCount, EzDanSide side)
        {
            skillName.SetFromAxis(axis);
            dan.SetLabel(danLabel, keyCount, side);

            if (ShowValue && overallMsd is double msd && double.IsFinite(msd) && msd > 0)
            {
                valueText.Text = $"({msd.ToString("0.0", CultureInfo.InvariantCulture)})";
                valueText.Alpha = 1;
            }
            else
            {
                valueText.Text = string.Empty;
                valueText.Alpha = 0;
            }

            Alpha = 1;
        }
    }
}
