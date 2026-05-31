// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 选歌界面按住 LAlt 时，在 mod footer 上方显示快捷键提示（样式参考顶部栏 tooltip）。
    /// 作为 <see cref="FooterButtonMods"/> 子节点：tooltip 底边对齐 footer 按钮顶边上方，整段文字向上展开。
    /// </summary>
    public partial class EzModFooterAltHint : CompositeDrawable
    {
        private const float gap_above_footer_top = 8f;

        private FillFlowContainer tooltipContainer = null!;

        public EzModFooterAltHint()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.BottomLeft;
            Y = -gap_above_footer_top;
            Depth = float.MinValue;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = tooltipContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Shadow = true,
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        Text = EzSongSelectStrings.MOD_CLEAR_RESTORE_HINT,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Shadow = true,
                                Text = EzSongSelectStrings.MOD_CLEAR_RESTORE_HINT_SUB,
                            },
                            new HotkeyDisplay
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Margin = new MarginPadding { Left = 3 },
                                Hotkey = new Hotkey(new KeyCombination(InputKey.LAlt, InputKey.Number1)),
                            },
                        }
                    }
                }
            };

            EzDisplayTagAltHighlight.ActiveChanged += onAltHighlightChanged;
            onAltHighlightChanged(EzDisplayTagAltHighlight.Active);
        }

        private void onAltHighlightChanged(bool active)
        {
            if (active)
                tooltipContainer.FadeIn(200, Easing.OutQuint);
            else
                tooltipContainer.FadeOut(100, Easing.Out);
        }

        protected override void Dispose(bool isDisposing)
        {
            EzDisplayTagAltHighlight.ActiveChanged -= onAltHighlightChanged;
            base.Dispose(isDisposing);
        }
    }
}
