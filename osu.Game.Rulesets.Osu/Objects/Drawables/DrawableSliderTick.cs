// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableSliderTick : DrawableOsuHitObject
    {
        public const double ANIM_DURATION = 150;

        public const float DEFAULT_TICK_SIZE = 16;

        protected DrawableSlider DrawableSlider => (DrawableSlider)ParentHitObject;

        private SkinnableDrawable scaleContainer;

        public DrawableSliderTick()
            : base(null)
        {
        }

        public DrawableSliderTick(SliderTick sliderTick)
            : base(sliderTick)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Size = OsuHitObject.OBJECT_DIMENSIONS;
            Origin = Anchor.Centre;

            ScaleBindable.BindValueChanged(scale => scaleContainer?.Scale = new Vector2(scale.NewValue));
        }

        /// <summary>
        /// 创建视觉，除非 stable 不绘制该点。
        /// </summary>
        /// <remarks>
        /// 在首次应用时构建而非加载时：池化 drawable 加载时没有 hitobject，决定权在 hitobject。
        /// </remarks>
        private void ensureVisuals()
        {
            if (scaleContainer != null)
                return;

            if (HitObject is IStableUndrawnScoringPoint { UndrawnUnderStableRules: true })
                return;

            AddInternal(scaleContainer = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.SliderScorePoint), _ => new CircularContainer
            {
                Masking = true,
                Origin = Anchor.Centre,
                Size = new Vector2(DEFAULT_TICK_SIZE),
                BorderThickness = DEFAULT_TICK_SIZE / 4,
                BorderColour = Color4.White,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = AccentColour.Value,
                    Alpha = 0.3f,
                }
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });

            scaleContainer.Scale = new Vector2(ScaleBindable.Value);
        }

        protected override void OnApply()
        {
            base.OnApply();

            ensureVisuals();

            Vector2 alongPath = HitObject.Position - DrawableSlider.HitObject.Position;

            if (!StableViewportBody.CanPlaceFieldPosition(alongPath))
            {
                Position = Vector2.Zero;
                Alpha = 0;
                return;
            }

            Position = alongPath;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (JudgementKernelDriven)
                return;

            DrawableSlider.SliderInputManager.TryJudgeNestedObject(this);
        }

        protected override void UpdateInitialTransforms()
        {
            this.FadeOut().FadeIn(ANIM_DURATION);
            this.ScaleTo(0.5f).ScaleTo(1f, ANIM_DURATION * 4, Easing.OutElasticHalf);
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            switch (state)
            {
                case ArmedState.Idle:
                    this.Delay(HitObject.TimePreempt).FadeOut();
                    break;

                case ArmedState.Miss:
                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    break;

                case ArmedState.Hit:
                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    this.ScaleTo(Scale * 1.5f, ANIM_DURATION, Easing.Out);
                    break;
            }
        }
    }
}
