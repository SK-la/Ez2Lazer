// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzJudgementLine : CompositeDrawable
    {
        private readonly IBindable<double> hitPositonBindable = new BindableDouble();
        private readonly IBindable<string> noteSetName = new Bindable<string>();

        private Container sprite = null!;
        private TextureAnimation? container;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                sprite = new Container
                {
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -ezSkinConfig.DefaultHitPosition,
                }
            };

            noteSetName.BindTo(ezSkinInfo.NoteSetName);
            noteSetName.BindValueChanged(_ => OnDrawableChanged(), true);

            hitPositonBindable.BindTo(ezSkinInfo.HitPosition);
            hitPositonBindable.BindValueChanged(_ => updateSizes(), true);
        }

        protected void OnDrawableChanged()
        {
            sprite.Clear();
            container?.ClearFrames();
            container = factory.CreateAnimation("JudgementLine");
            sprite.Add(container);

            Scheduler.AddOnce(updateSizes);
        }

        private void updateSizes()
        {
            float actualPanelWidth = DrawWidth; //ezSkinConfig.GetTotalWidth(cs);
            float scale = actualPanelWidth / 412.0f;

            sprite.Scale = new Vector2(scale);
            sprite.Y = 384f + ezSkinConfig.DefaultHitPosition - (float)hitPositonBindable.Value;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                noteSetName.UnbindBindings();
                hitPositonBindable.UnbindBindings();
            }

            base.Dispose(isDisposing);
        }
    }
}
