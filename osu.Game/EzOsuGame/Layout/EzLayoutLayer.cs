// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Rulesets;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Game-level host for Ez layout containers. Shown on song select and player; not mixed into skins.
    /// </summary>
    public partial class EzLayoutLayer : CompositeDrawable
    {
        [Resolved]
        private EzConfigLayoutStore layoutStore { get; set; } = null!;

        public EzConfigLayoutStore Store => layoutStore;

        public EzLayoutContainer? SongSelectContainer { get; private set; }

        /// <summary>
        /// Fired after song-select / gameplay containers are shown, hidden, or recreated.
        /// </summary>
        public event Action? LayoutTargetsChanged;

        private EzLayoutContainer? gameplayContainer;
        private EzLayoutContainer? rulesetGameplayContainer;
        private EzLayoutContainer? rulesetPlayfieldContainer;
        private Player? pendingPlayer;

        private bool onSongSelect;
        private bool songSelectLayoutSuppressed;
        private bool editorHidden;

        /// <summary>
        /// When set, hides the song-select layout container even while song select is the current screen.
        /// Used while the beatmap preview overlay covers the screen in fullscreen focus.
        /// </summary>
        public bool SongSelectLayoutSuppressed
        {
            get => songSelectLayoutSuppressed;
            set
            {
                if (songSelectLayoutSuppressed == value)
                    return;

                songSelectLayoutSuppressed = value;
                updateSongSelectContainerVisibility();
            }
        }

        [Resolved]
        private OsuGame game { get; set; } = null!;

        /// <summary>
        /// When set, hides every layout container regardless of the current screen. Owned by the Ez
        /// skin editor's HUD layer toggle, which temporarily hides the whole HUD layer while editing.
        /// </summary>
        public bool EditorHidden
        {
            get => editorHidden;
            set
            {
                if (editorHidden == value)
                    return;

                editorHidden = value;
                updateSongSelectContainerVisibility();
                updateGameplayContainerVisibility();
            }
        }

        public EzLayoutLayer()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = -0.5f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = SongSelectContainer = new EzLayoutContainer(Store, EzLayoutFiles.SONG_SELECT, null);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            layoutStore.SetChanged += reloadThemeLayouts;
            game.ScreenStack.ScreenPushed += onScreenChanged;
            game.ScreenStack.ScreenExited += onScreenChanged;
            applyScreen(game.ScreenStack.CurrentScreen);
        }

        private void reloadThemeLayouts()
        {
            // 只换主题 Store 上的容器。客户端容器持有另一份 Store，换主题不能拆掉再用主题 Store 重建。
            if (SongSelectContainer?.Uses(layoutStore) == true)
            {
                RemoveInternal(SongSelectContainer, true);
                SongSelectContainer = new EzLayoutContainer(layoutStore, EzLayoutFiles.SONG_SELECT, null);
                AddInternal(SongSelectContainer);
                updateSongSelectContainerVisibility();
            }

            if (game.ScreenStack.CurrentScreen is Player player && canAttachGameplayLayout(player))
                recreateGameplayContainers(player);
            else if (gameplayContainer != null || rulesetGameplayContainer != null)
                expireGameplayContainers();

            notifyTargetsChanged();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            unbindPendingPlayer();

            // 订阅发生在 LoadComplete。
            if (!IsLoaded)
                return;

            layoutStore.SetChanged -= reloadThemeLayouts;
            game.ScreenStack.ScreenPushed -= onScreenChanged;
            game.ScreenStack.ScreenExited -= onScreenChanged;
        }

        public IEnumerable<EzLayoutContainer> LayoutContainers => InternalChildren.OfType<EzLayoutContainer>();

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            InternalChildren.Any(c => c.IsPresent && c.ReceivePositionalInputAt(screenSpacePos));

        private void onScreenChanged(IScreen last, IScreen next) => Schedule(() => applyScreen(next));

        private void applyScreen(IScreen? screen)
        {
            unbindPendingPlayer();

            onSongSelect = screen is SongSelect;
            bool onPlayer = screen is Player;

            updateSongSelectContainerVisibility();

            if (onPlayer && screen is Player player)
            {
                if (canAttachGameplayLayout(player))
                    recreateGameplayContainers(player);
                else if (!player.IsLoaded)
                {
                    pendingPlayer = player;
                    pendingPlayer.OnLoadComplete += onPendingPlayerLoaded;
                }
                else
                    expireGameplayContainers();

                notifyTargetsChanged();
            }
            else if (gameplayContainer != null || rulesetGameplayContainer != null)
            {
                expireGameplayContainers();
                notifyTargetsChanged();
            }
            else
            {
                notifyTargetsChanged();
            }
        }

        private void onPendingPlayerLoaded(Drawable loaded)
        {
            unbindPendingPlayer();

            Schedule(() =>
            {
                if (game.ScreenStack.CurrentScreen == loaded && loaded is Player player && canAttachGameplayLayout(player))
                    recreateGameplayContainers(player);
            });
        }

        private void unbindPendingPlayer()
        {
            if (pendingPlayer == null)
                return;

            pendingPlayer.OnLoadComplete -= onPendingPlayerLoaded;
            pendingPlayer = null;
        }

        private void recreateGameplayContainers(Player player)
        {
            expireGameplayContainers();

            gameplayContainer = addGameplayContainer(player, EzLayoutFiles.MAIN_HUD, null);

            var ruleset = player.Ruleset.Value;

            if (ruleset != null)
            {
                rulesetGameplayContainer = addGameplayContainer(player, EzLayoutFiles.MAIN_HUD, ruleset);
                rulesetPlayfieldContainer = addGameplayContainer(player, EzLayoutFiles.PLAYFIELD, ruleset);
            }

            updateGameplayContainerVisibility();
            notifyTargetsChanged();
        }

        private static bool canAttachGameplayLayout(Player player) =>
            player.IsLoaded && player.Dependencies != null;

        private EzLayoutContainer addGameplayContainer(Player player, string layoutFile, RulesetInfo? ruleset)
        {
            var container = new EzLayoutContainer(Store, layoutFile, ruleset);

            if (canAttachGameplayLayout(player))
            {
                var donor = player.Dependencies!.Get(typeof(HUDOverlay)) as CompositeDrawable ?? player;
                container.SetDependencyDonor(donor);
            }

            AddInternal(container);
            return container;
        }

        private void expireGameplayContainers()
        {
            gameplayContainer?.Expire();
            gameplayContainer = null;
            rulesetGameplayContainer?.Expire();
            rulesetGameplayContainer = null;
            rulesetPlayfieldContainer?.Expire();
            rulesetPlayfieldContainer = null;
        }

        private void updateSongSelectContainerVisibility()
        {
            SongSelectContainer?.Alpha = onSongSelect && !songSelectLayoutSuppressed && !editorHidden ? 1 : 0;
        }

        private void updateGameplayContainerVisibility()
        {
            float alpha = editorHidden ? 0 : 1;

            gameplayContainer?.Alpha = alpha;
            rulesetGameplayContainer?.Alpha = alpha;
            rulesetPlayfieldContainer?.Alpha = alpha;
        }

        private void notifyTargetsChanged() => LayoutTargetsChanged?.Invoke();
    }
}
