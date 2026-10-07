// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 排行榜卡片上的附加分变化量标签：正为粉、负为红、0 为灰；悬停显示三项明细。主分数仍为原分（与排序一致）。
    /// </summary>
    public partial class EzScoreBonusChip : CompositeDrawable, IHasTooltip
    {
        private readonly ScoreInfo score;
        private readonly EzScoreBonusTracker tracker;

        private OsuSpriteText text = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public LocalisableString TooltipText { get; private set; }

        public EzScoreBonusChip(ScoreInfo score)
        {
            this.score = score;
            tracker = new EzScoreBonusTracker(score);
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                tracker,
                text = new OsuSpriteText
                {
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold, fixedWidth: true),
                    UseFullGlyphHeight = false,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tracker.Current.BindValueChanged(_ => updateDisplay());
            tracker.State.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            if (tracker.Current.Value is not EzScoreBonusResult bonus)
            {
                bool pending = tracker.State.Value == EzScoreBonusState.Pending;

                text.Text = pending ? "\u2026" : "\u2014";
                text.Colour = Color4.Gray;
                TooltipText = pending ? EzSongSelectStrings.SCORE_BONUS_PENDING : EzSongSelectStrings.SCORE_BONUS_UNAVAILABLE;
                return;
            }

            text.Text = EzScoreBonusTracker.FormatSigned(bonus.Total);
            text.Colour = bonus.Total > 0 ? colours.Pink1 : bonus.Total < 0 ? colours.Red1 : Color4.Gray;

            TooltipText = LocalisableString.Format("{0} {1}\n{2} {3}\n{4} {5}",
                EzSongSelectStrings.SCORE_BONUS_JUDGE, EzScoreBonusTracker.FormatSigned(bonus.JudgeBonus),
                EzSongSelectStrings.SCORE_BONUS_MISS, EzScoreBonusTracker.FormatSigned(bonus.ErrorPenalty),
                EzSongSelectStrings.SCORE_BONUS_TOTAL, EzScoreBonusTracker.TotalWithBonus(score, bonus).ToLocalisableString("N0"));
        }
    }
}
