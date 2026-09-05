// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Game-level host for Ez layout containers. Shown on song select and player; not mixed into skins.
    /// </summary>
    public partial class EzLayoutLayer : CompositeDrawable
    {
        public EzLayoutStore Store { get; private set; } = null!;

        public EzLayoutContainer SongSelectContainer { get; private set; } = null!;

        /// <summary>
        /// Fired after song-select / gameplay containers are shown, hidden, or recreated.
        /// </summary>
        public event Action? LayoutTargetsChanged;

        private EzLayoutContainer? gameplayContainer;
        private EzLayoutContainer? rulesetGameplayContainer;
        private Player? pendingPlayer;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        public EzLayoutLayer()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = -0.5f;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            Store = new EzLayoutStore(storage);

            InternalChild = SongSelectContainer = new EzLayoutContainer(
                Store,
                new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            game.ScreenStack.ScreenPushed += onScreenChanged;
            game.ScreenStack.ScreenExited += onScreenChanged;
            applyScreen(game.ScreenStack.CurrentScreen);
        }

        protected override void Dispose(bool isDisposing)
        {
            unbindPendingPlayer();

            if (game.ScreenStack != null)
            {
                game.ScreenStack.ScreenPushed -= onScreenChanged;
                game.ScreenStack.ScreenExited -= onScreenChanged;
            }

            base.Dispose(isDisposing);
        }

        public IEnumerable<EzLayoutContainer> LayoutContainers => InternalChildren.OfType<EzLayoutContainer>();

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            InternalChildren.Any(c => c.IsPresent && c.ReceivePositionalInputAt(screenSpacePos));

        private void onScreenChanged(IScreen last, IScreen next) => Schedule(() => applyScreen(next));

        private void applyScreen(IScreen? screen)
        {
            unbindPendingPlayer();

            bool onSongSelect = screen is SongSelect;
            bool onPlayer = screen is Player;

            SongSelectContainer.Alpha = onSongSelect ? 1 : 0;

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

            gameplayContainer = addGameplayContainer(player, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

            var ruleset = player.Ruleset.Value;
            if (ruleset != null)
                rulesetGameplayContainer = addGameplayContainer(player, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset));

            notifyTargetsChanged();
        }

        private static bool canAttachGameplayLayout(Player player) =>
            player.IsLoaded && player.Dependencies != null;

        private EzLayoutContainer addGameplayContainer(Player player, GlobalSkinnableContainerLookup lookup)
        {
            var container = new EzLayoutContainer(Store, lookup);

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
        }

        private void notifyTargetsChanged() => LayoutTargetsChanged?.Invoke();
    }
}
