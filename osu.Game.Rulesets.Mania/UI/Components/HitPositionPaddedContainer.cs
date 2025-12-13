// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.UI.Components
{
    public partial class HitPositionPaddedContainer : Container
    {
        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private Bindable<double> hitPositonBindable = new Bindable<double>();
        private Bindable<bool> globalHitPosition = new Bindable<bool>();

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            Direction.BindTo(scrollingInfo.Direction);
            Direction.BindValueChanged(_ => UpdateHitPosition(), true);

            globalHitPosition = ezSkinConfig.GetBindable<bool>(EzSkinSetting.GlobalHitPosition);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            skin.SourceChanged += onSkinChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            globalHitPosition.BindValueChanged(_ => UpdateHitPosition(), true);
            hitPositonBindable.BindValueChanged(_ => UpdateHitPosition(), true);
        }

        private void onSkinChanged() => UpdateHitPosition();

        protected virtual void UpdateHitPosition()
        {
            float hitPosition = globalHitPosition.Value
                ? (float)hitPositonBindable.Value
                : skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                      new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.HitPosition))?.Value
                  ?? (float)hitPositonBindable.Value;

            Padding = Direction.Value == ScrollingDirection.Up
                ? new MarginPadding { Top = hitPosition }
                : new MarginPadding { Bottom = hitPosition };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
