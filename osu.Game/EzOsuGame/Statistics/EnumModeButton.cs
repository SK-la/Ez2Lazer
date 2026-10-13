// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Footer;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 结算界面底部用于切换 <typeparamref name="TEnum"/> 模式的 footer 按钮，点击后弹出全部枚举值。
    /// </summary>
    public partial class EnumModeButton<TEnum> : ScreenFooterButton, IHasPopover
        where TEnum : struct, Enum
    {
        private readonly Bindable<TEnum> current;
        private readonly Action<TEnum> setValue;
        private readonly Action<ValueChangedEvent<TEnum>> updateText;

        public EnumModeButton(Bindable<TEnum> current, IconUsage icon, LocalisableString description, Action<TEnum>? setValue = null)
        {
            this.current = current;
            this.setValue = setValue ?? (value => current.Value = value);

            Icon = icon;
            TooltipText = description;

            updateText = v => Text = v.NewValue.ToString();
            current.BindValueChanged(updateText, true);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AccentColour = colours.Purple1;
            Action = this.ShowPopover;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            current.ValueChanged -= updateText;
        }

        public Popover GetPopover() => new EnumModePopover(setValue);

        private partial class EnumModePopover : OsuPopover
        {
            public EnumModePopover(Action<TEnum> setValue)
                : base(false)
            {
                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };

                Children = new[]
                {
                    new OsuMenu(Direction.Vertical, true)
                    {
                        Items = Enum.GetValues<TEnum>()
                                    .Select(mode => new OsuMenuItem(mode.ToString(), MenuItemType.Standard, () => setValue(mode)))
                                    .ToArray(),
                        MaxHeight = 375,
                    },
                };
            }
        }
    }
}
