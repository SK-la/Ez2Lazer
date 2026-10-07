// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 结算成绩卡上的附加分算式行：判定加成 / 失误罚分 / 含附加分合计（原分由上方计分器显示）。
    /// </summary>
    public partial class EzScoreBonusBreakdown : CompositeDrawable
    {
        public const float HEIGHT = 30;

        private const string unavailable_text = "\u2014";

        private readonly ScoreInfo score;
        private readonly EzScoreBonusTracker tracker;

        private OsuSpriteText judgeText = null!;
        private OsuSpriteText missText = null!;
        private OsuSpriteText totalText = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzScoreBonusBreakdown(ScoreInfo score)
        {
            this.score = score;
            tracker = new EzScoreBonusTracker(score);

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                tracker,
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension(), new Dimension(), new Dimension() },
                    Content = new[]
                    {
                        new[]
                        {
                            createCell(EzSongSelectStrings.SCORE_BONUS_JUDGE, colours.Pink1, out judgeText),
                            createCell(EzSongSelectStrings.SCORE_BONUS_ERROR, colours.Red1, out missText),
                            createCell(EzSongSelectStrings.SCORE_BONUS_TOTAL, Color4.White, out totalText),
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tracker.Tendency.BindValueChanged(_ => updateText());
            tracker.Current.BindValueChanged(_ => updateText());
            tracker.State.BindValueChanged(_ => updateText(), true);
        }

        private void updateText()
        {
            if (!tracker.Shown)
            {
                Alpha = 0;
                Height = 0;
                return;
            }

            Alpha = 1;
            Height = HEIGHT;

            if (tracker.Current.Value is not EzScoreBonusResult bonus)
            {
                string placeholder = tracker.State.Value == EzScoreBonusState.Pending ? "\u2026" : unavailable_text;
                judgeText.Text = missText.Text = totalText.Text = placeholder;
                return;
            }

            judgeText.Text = EzScoreBonusTracker.FormatSigned(bonus.JudgeBonus);
            missText.Text = EzScoreBonusTracker.FormatSigned(bonus.ErrorMalus);
            totalText.Text = EzScoreBonusTracker.TotalWithBonus(score, bonus).ToLocalisableString("N0");
        }

        private static Drawable createCell(LocalisableString label, Color4 valueColour, out OsuSpriteText valueText)
        {
            return new FillFlowContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = label,
                        Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold),
                        Alpha = 0.6f,
                    },
                    valueText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Colour = valueColour,
                        Font = OsuFont.Torus.With(size: 15, weight: FontWeight.SemiBold, fixedWidth: true),
                    },
                }
            };
        }
    }
}
