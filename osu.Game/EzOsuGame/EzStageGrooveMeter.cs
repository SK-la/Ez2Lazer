// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // 满血露出整张血槽贴图。遮罩比贴图高，并且从贴图中心往下铺，若按遮罩裁会把贴图切掉一半。
    internal partial class EzStageGrooveMeter : CompositeDrawable
    {
        private readonly Drawable? gauge;
        private readonly Drawable? bright;

        private Container clip = null!;
        private float fullHeight;
        private Bindable<double>? health;

        [Resolved(CanBeNull = true)]
        private HealthProcessor? healthProcessor { get; set; }

        public EzStageGrooveMeter(EzStagePlateMeter meter, Drawable? gauge, Drawable? bright)
        {
            this.gauge = gauge;
            this.bright = bright;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Position = EzStagePlateSprite.ToOsu(meter.X, meter.Y);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
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

        private static void include(Drawable? drawable, ref float top, ref float bottom)
        {
            if (drawable == null)
                return;

            float half = drawable.Height * Math.Abs(drawable.Scale.Y) / 2f;
            top = Math.Min(top, drawable.Position.Y - half);
            bottom = Math.Max(bottom, drawable.Position.Y + half);
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
