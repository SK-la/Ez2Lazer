// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class HitTargetInsetContainer : Container
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly IBindable<double> hitPositon = new Bindable<double>();
        private readonly IBindable<bool> enableGlobalHitPosition = new Bindable<bool>();

        protected override Container<Drawable> Content => content;
        private readonly Container content;

        private float hitPosition;
        private float skinHitPosition;

        public HitTargetInsetContainer()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = content = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            enableGlobalHitPosition.BindTo(ezConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable));
            enableGlobalHitPosition.BindValueChanged(_ => updateDrawable());

            hitPositon.BindTo(ezSkinInfo.HitPosition);
            hitPositon.BindValueChanged(_ => updateDrawable());

            skinHitPosition = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.HitPosition)?.Value ?? (float)hitPositon.Value;
            updateHitPosition();

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void updateHitPosition()
        {
            // 缓存 hitPosition 值，避免每次布局都查询 skin
            hitPosition = enableGlobalHitPosition.Value
                ? (float)hitPositon.Value
                : skinHitPosition;
        }

        private void updateDrawable()
        {
            updateHitPosition();
            onDirectionChanged(new ValueChangedEvent<ScrollingDirection>(direction.Value, direction.Value));
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            content.Padding = direction.NewValue == ScrollingDirection.Up
                ? new MarginPadding { Top = hitPosition }
                : new MarginPadding { Bottom = hitPosition };
        }
    }
}
