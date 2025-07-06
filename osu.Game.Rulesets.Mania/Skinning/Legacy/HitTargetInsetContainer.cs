// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class HitTargetInsetContainer : Container
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private Bindable<double> hitPositonBindable = new Bindable<double>();
        protected override Container<Drawable> Content => content;
        private readonly Container content;

        private float hitPosition;

        public HitTargetInsetContainer()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = content = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, EzSkinSettingsManager ezSkinConfig, IScrollingInfo scrollingInfo)
        {
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            hitPositonBindable.BindValueChanged(_ => UpdateHitPosition(), true);
            hitPosition = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.HitPosition)?.Value ?? (float)hitPositonBindable.Value;

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        protected virtual void UpdateHitPosition()
        {
            hitPosition = (float)hitPositonBindable.Value;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            content.Padding = direction.NewValue == ScrollingDirection.Up
                ? new MarginPadding { Top = hitPosition }
                : new MarginPadding { Bottom = hitPosition };
        }
    }
}
