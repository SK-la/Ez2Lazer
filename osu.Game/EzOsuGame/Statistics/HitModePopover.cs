// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.EzOsuGame.Statistics
{
    public partial class HitModeButton : RoundedButton, IHasPopover
    {
        private readonly Bindable<EzEnumHitMode> hitModeBindable;

        public HitModeButton(Bindable<EzEnumHitMode> hitModeBindable)
        {
            this.hitModeBindable = hitModeBindable;

            Size = new Vector2(75, 30);
            Text = hitModeBindable.Value.ToString();

            hitModeBindable.BindValueChanged(v => Text = v.NewValue.ToString());

            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new HitModePopover();

        private partial class HitModePopover : OsuPopover
        {
            private readonly Bindable<EzEnumHitMode> hitModeBindable = new Bindable<EzEnumHitMode>();

            public HitModePopover()
                : base(false)
            {
                // this.hitModeBindable = hitModeBindable;

                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };
            }

            [BackgroundDependencyLoader]
            private void load(Ez2ConfigManager ezConfig)
            {
                hitModeBindable.BindTo(ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode));
                Children = new[]
                {
                    new OsuMenu(Direction.Vertical, true)
                    {
                        Items = Enum.GetValues<EzEnumHitMode>().Select(mode => new OsuMenuItem(mode.ToString(), MenuItemType.Standard, () => ezConfig.SetValue(Ez2Setting.ManiaHitMode, mode))).ToArray(),
                        MaxHeight = 375,
                    },
                };
            }
        }
    }
}
