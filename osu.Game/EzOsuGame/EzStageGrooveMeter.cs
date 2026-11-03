// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // 满血露出整张血槽贴图。遮罩比贴图高，并且从贴图中心往下铺，若按遮罩裁会把贴图切掉一半。
    // 复合体落在判定线原点；gauge/bright 为 meter 组内坐标，无旋转时再平移到舞台绝对坐标，贴图各自参与裁剪。
    internal partial class EzStageGrooveMeter : CompositeDrawable
    {
        private readonly EzStagePlateMeter meter;
        private readonly Drawable? gauge;
        private readonly Drawable? bright;

        private Container clip = null!;
        private float fullHeight;
        private Bindable<double>? health;

        [Resolved(CanBeNull = true)]
        private HealthProcessor? healthProcessor { get; set; }

        public EzStageGrooveMeter(EzStagePlateMeter meter, Drawable? gauge, Drawable? bright)
        {
            this.meter = meter;
            this.gauge = gauge;
            this.bright = bright;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 有旋转时由外层 applyRotation 负责组原点；此处只补 meter 组到舞台绝对坐标的平移。
            if (!meter.HasRotation)
                translateToStageSpace(gauge, bright);

            float top = float.MaxValue;
            float bottom = float.MinValue;
            include(gauge, ref top, ref bottom);
            include(bright, ref top, ref bottom);

            fullHeight = bottom > top ? bottom - top : 0;

            clip = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.BottomCentre,
                Position = new Vector2(0, bottom),
                Width = 4096,
                Height = fullHeight,
                Masking = fullHeight > 0,
            };

            if (gauge != null)
                clip.Add(place(gauge, bottom));

            if (bright != null)
                clip.Add(place(bright, bottom));

            InternalChild = clip;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (healthProcessor == null || fullHeight <= 0)
                return;

            health = healthProcessor.Health.GetBoundCopy();
            health.BindValueChanged(onHealthChanged, true);
        }

        private void onHealthChanged(ValueChangedEvent<double> change)
            => clip.Height = fullHeight * (float)Math.Clamp(change.NewValue, 0, 1);

        private void translateToStageSpace(Drawable? gaugeDrawable, Drawable? brightDrawable)
        {
            Vector2 offset = meter.ToOsuPosition();

            if (gaugeDrawable != null)
                gaugeDrawable.Position += offset;

            if (brightDrawable != null)
                brightDrawable.Position += offset;
        }

        private static void include(Drawable? drawable, ref float top, ref float bottom)
        {
            if (drawable == null)
                return;

            float half = verticalHalfExtent(drawable);
            top = Math.Min(top, drawable.Position.Y - half);
            bottom = Math.Max(bottom, drawable.Position.Y + half);
        }

        private static float verticalHalfExtent(Drawable drawable)
        {
            float height = drawable.DrawHeight;

            if (height <= 0 && drawable is Sprite sprite && sprite.Texture != null)
                height = sprite.Texture.DisplayHeight;

            return height * Math.Abs(drawable.Scale.Y) / 2f;
        }

        private static Drawable place(Drawable drawable, float bottomY)
        {
            Vector2 at = drawable.Position;
            drawable.Anchor = Anchor.BottomCentre;
            drawable.Origin = Anchor.Centre;
            drawable.Position = new Vector2(at.X, at.Y - bottomY);
            return drawable;
        }

        protected override void Dispose(bool isDisposing)
        {
            health?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
