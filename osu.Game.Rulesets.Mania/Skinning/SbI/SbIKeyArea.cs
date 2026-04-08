// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIKeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<double> hitPositonBindable = new Bindable<double>();

        private readonly Box line;

        [Resolved]
        private Column column { get; set; } = null!;

        public SbIKeyArea()
        {
            RelativeSizeAxes = Axes.X;

            InternalChild = line = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1f,
                Colour = Color4.Black,
            };
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            hitPositonBindable.BindTo(ezSkinInfo.HitPosition);
            hitPositonBindable.BindValueChanged(_ => OnConfigChanged(), true);

            // column.TopLevelContainer.Add(CreateProxy());
        }

        private void OnConfigChanged()
        {
            Y = (float)hitPositonBindable.Value;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return;

            line.FadeTo(0.8f, 50, Easing.OutQuint)
                .Then()
                .FadeOut(800, Easing.OutQuint);
        }
    }
}
