using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComHitTiming : CompositeDrawable, ISerialisableDrawable
    {
        private readonly Bindable<double> hitOffset = new Bindable<double>();
        private readonly Bindable<HitResult> hitResult = new Bindable<HitResult>();

        [Resolved]
        protected HitWindows HitWindows { get; private set; }

        [Resolved]
        private ScoreProcessor processor { get; set; }

        [Resolved]
        private OsuColour colours { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayClockContainer gameplayClockContainer { get; set; }

        [Resolved]
        private DrawableRuleset drawableRuleset { get; set; }

        private OsuSpriteText offsetText;
        private Box backgroundBox;

        public bool UsesFixedAnchor { get; set; }

        public EzComHitTiming()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            HitWindows = drawableRuleset?.FirstAvailableHitWindows ?? HitWindows.Empty;

            hitOffset.BindValueChanged(offset =>
            {
                // 更新显示的偏差值
                var container = (Container)InternalChildren[1];
                ((SpriteText)container.Child).Text = offset.NewValue.ToString("0.00");
            }, true);

            hitResult.BindValueChanged(result =>
            {
                // 根据判定结果更新颜色
                ((Box)InternalChildren[0]).Colour = colours.ForHitResult(result.NewValue);
            }, true);
            InternalChildren = new Drawable[]
            {
                backgroundBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.5f
                },
                offsetText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = hitOffset.GetBoundCopy().ToString()
                }
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            processor.NewJudgement -= OnNewJudgement;
        }

        public void OnNewJudgement(JudgementResult judgement)
        {
            if (judgement.IsHit)
            {
                hitOffset.Value = judgement.TimeOffset;
                hitResult.Value = judgement.Type;
            }
        }

        private Color4 getColorForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return colours.Green;

                case HitResult.Good:
                    return colours.Yellow;

                case HitResult.Ok:
                case HitResult.Meh:
                    return colours.Purple;

                case HitResult.Miss:
                default:
                    return colours.Red;
            }
        }
    }
}
