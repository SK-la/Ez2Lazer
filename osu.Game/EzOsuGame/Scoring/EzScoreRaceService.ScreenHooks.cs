// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Scoring
{
    public partial class EzScoreRaceService
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        private IBindable<IReadOnlyList<Mod>>? boundScreenMods;
        private OsuScreen? boundModsScreen;

        /// <summary>
        /// 当前 loading 中的 <see cref="PlayerLoader"/>。用于在其 <see cref="Player"/> 装载完成后
        /// 读取角逐 HUD 的实际注册情况，校正静态预测（见 <see cref="tryResolveDemandFromConsumers"/>）。
        /// </summary>
        private PlayerLoader? activePlayerLoader;

        private void subscribeScreenHooks()
        {
            game.ScreenStack.ScreenPushed += onScreenPushed;
            game.ScreenStack.ScreenExited += onScreenExited;

            // 服务关闭时不做任何屏幕绑定，保证 0 影响；启用后由 onServiceEnabledChanged 补绑当前屏幕。
            if (isServiceActive)
                bindModsFromScreen(game.ScreenStack.CurrentScreen as OsuScreen);
        }

        private void unsubscribeScreenHooks()
        {
            game.ScreenStack.ScreenPushed -= onScreenPushed;
            game.ScreenStack.ScreenExited -= onScreenExited;
            unbindScreenMods();
        }

        private void onScreenPushed(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive)
                return;

            if (newScreen is PlayerLoader playerLoader)
            {
                activePlayerLoader = playerLoader;
                bindModsFromScreen(playerLoader);
                beginLoaderPreparation();
                return;
            }

            bindModsFromScreen(newScreen as OsuScreen);

            // 选歌界面是预加载决策点：皮肤 / Ez 布局编辑器可能刚改过布局，
            // 而官方编辑器与 EzLayoutStore 的保存都不产生任何绑定事件，只有这里强制重扫才可达。
            // 其他屏幕（含 Player / 结算）不重扫，保证局内零额外负担。
            if (newScreen is SongSelect)
                recomputeDemand(force: true);
        }

        private void onScreenExited(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive)
                return;

            if (lastScreen is PlayerLoader)
                endLoaderPreparation(advancingToPlayer: newScreen is Player);

            bindModsFromScreen(newScreen as OsuScreen);
        }

        private void bindModsFromScreen(OsuScreen? screen)
        {
            if (boundModsScreen == screen)
                return;

            unbindScreenMods();
            boundModsScreen = screen;

            if (screen == null)
                return;

            boundScreenMods = screen.Mods.GetBoundCopy();
            boundScreenMods.BindValueChanged(_ => onQueryContextChanged(), true);
        }

        private void unbindScreenMods()
        {
            boundScreenMods?.UnbindAll();
            boundScreenMods = null;
            boundModsScreen = null;
        }

        private Mod[] getCurrentMods()
        {
            if (boundScreenMods?.Value == null || boundScreenMods.Value.Count == 0)
                return [];

            return boundScreenMods.Value.ToArray();
        }
    }
}
