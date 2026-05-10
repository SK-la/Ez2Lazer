// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Rulesets.BMS.UI.SongSelect;
using osu.Game.Screens.Menu;
using osuTK.Graphics;
using osuTK.Input;

// ReSharper disable CheckNamespace

namespace osu.Game.Rulesets.BMS.UI.MenuEntry
{
    /// <summary>
    /// Injects an extra "BMS Game" button into the main-menu PLAY sub-row
    /// without modifying any osu.Game code.
    ///
    /// The injector enters the OsuGame Drawable tree via <see cref="BmsRulesetIcon"/>
    /// (returned from <see cref="BMSRuleset.CreateIcon"/>), resolves the running
    /// <see cref="OsuGame"/>, listens to ScreenPushed and reflects into
    /// <see cref="ButtonSystem"/> private members to add a regular
    /// <see cref="MainMenuButton"/> alongside Solo / Multi / Playlists.
    ///
    /// All reflection failure paths degrade gracefully: the user can still reach
    /// the BMS song select via the existing settings entry.
    /// </summary>
    public partial class BmsMainMenuButtonInjector : Drawable
    {
        /// <summary>
        /// Factory for the screen that the injected "BMS Game" button pushes onto the screen stack.
        /// Returns a <see cref="BmsSoloSongSelect"/> — a <c>SoloSongSelect</c> derivative that reuses every
        /// official song-select sub-component (title wedge, details area, filter control, beatmap carousel)
        /// while routing gameplay launches to the BMS-owned <see cref="BMSPlayerLoader"/>.
        /// Exposed as a static factory so tests can lock the click target type without exercising the full UI.
        /// </summary>
        public static IScreen CreateBmsSongSelectScreen() => new BmsSoloSongSelect();

        // One MainMenu instance gets injected at most once. ConditionalWeakTable
        // means we don't keep MainMenus alive past their natural lifetime.
        private static readonly ConditionalWeakTable<MainMenu, object> injected_menus = new ConditionalWeakTable<MainMenu, object>();

        [Resolved(canBeNull: true)]
        private OsuGame? osuGame { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (osuGame == null)
                return;

            // ScreenStack is publicly exposed by OsuGame.
            try
            {
                osuGame.ScreenStack.ScreenPushed += onScreenPushed;
            }
            catch (Exception ex)
            {
                Logger.Log($"BMS main-menu injector failed to subscribe ScreenPushed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            // The very first MainMenu may already be on top of the stack at the
            // time we load (Toolbar is created together with MainMenu).
            tryInjectIfMainMenu(osuGame.ScreenStack.CurrentScreen);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (osuGame != null)
            {
                try
                {
                    osuGame.ScreenStack.ScreenPushed -= onScreenPushed;
                }
                catch
                {
                    // ignored — game host may already be tearing down.
                }
            }

            base.Dispose(isDisposing);
        }

        private void onScreenPushed(IScreen lastScreen, IScreen newScreen) => tryInjectIfMainMenu(newScreen);

        private void tryInjectIfMainMenu(IScreen? screen)
        {
            if (screen is not MainMenu mainMenu)
                return;

            if (injected_menus.TryGetValue(mainMenu, out _))
                return;

            // Defer until MainMenu has finished its own load (so Buttons/buttonsPlay are populated).
            // Use our own Scheduler so we don't poke at MainMenu protected APIs.
            Scheduler.Add(() => attemptInject(mainMenu));
        }

        private void attemptInject(MainMenu mainMenu)
        {
            if (injected_menus.TryGetValue(mainMenu, out _))
                return;

            if (!mainMenu.IsLoaded)
            {
                // MainMenu not ready yet — try again next frame.
                Scheduler.AddDelayed(() => attemptInject(mainMenu), 50);
                return;
            }

            if (inject(mainMenu))
                injected_menus.Add(mainMenu, new object());
        }

        private bool inject(MainMenu mainMenu)
        {
            try
            {
                ButtonSystem? buttonSystem = getButtonSystem(mainMenu);

                if (buttonSystem == null)
                {
                    Logger.Log("BMS main-menu injector: failed to access MainMenu.Buttons via reflection.", LoggingTarget.Runtime, LogLevel.Important);
                    return false;
                }

                FieldInfo? buttonsPlayField = typeof(ButtonSystem).GetField("buttonsPlay", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? buttonAreaField = typeof(ButtonSystem).GetField("buttonArea", BindingFlags.Instance | BindingFlags.NonPublic);

                if (buttonsPlayField == null || buttonAreaField == null)
                {
                    Logger.Log("BMS main-menu injector: ButtonSystem private fields renamed; aborting injection.", LoggingTarget.Runtime, LogLevel.Important);
                    return false;
                }

                if (buttonsPlayField.GetValue(buttonSystem) is not List<MainMenuButton> buttonsPlay
                    || buttonAreaField.GetValue(buttonSystem) is not ButtonArea buttonArea)
                {
                    Logger.Log("BMS main-menu injector: ButtonSystem private fields had unexpected types; aborting.", LoggingTarget.Runtime, LogLevel.Important);
                    return false;
                }

                if (buttonsPlay.Count == 0)
                {
                    // ButtonSystem.load() not yet executed; retry shortly.
                    Scheduler.AddDelayed(() => attemptInject(mainMenu), 50);
                    return false;
                }

                var bmsButton = new MainMenuButton(
                    "bms",
                    @"button-default-select",
                    FontAwesome.Solid.Music,
                    new Color4(120, 80, 200, 255),
                    onBmsClicked,
                    Key.B)
                {
                    VisibleState = ButtonSystemState.Play,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                // Insert just after Solo so the order reads "Solo / BMS / Multi / Playlists / Daily".
                int insertIndex = Math.Min(1, buttonsPlay.Count);
                buttonsPlay.Insert(insertIndex, bmsButton);

                buttonArea.Add(bmsButton);

                // Reflect current ButtonSystem state so the new button animates correctly.
                bmsButton.ButtonSystemState = buttonSystem.State;

                Logger.Log("BMS main-menu injector: BMS button installed.", LoggingTarget.Runtime, LogLevel.Verbose);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"BMS main-menu injector failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return false;
            }
        }

        private static ButtonSystem? getButtonSystem(MainMenu mainMenu)
        {
            // MainMenu.Buttons is a protected field. Walk up the type hierarchy in case of subclassing.
            for (Type? type = mainMenu.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField("Buttons", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && typeof(ButtonSystem).IsAssignableFrom(field.FieldType))
                    return field.GetValue(mainMenu) as ButtonSystem;
            }

            return null;
        }

        private void onBmsClicked(MainMenuButton _, UIEvent __)
        {
            if (osuGame == null)
                return;

            try
            {
                // Push BMS's solo song-select derivative. It reuses osu.Game's full SongSelect UI tree but
                // forces Ruleset.Value to BMS, syncs BMS catalog into Realm, and intercepts OnStart to push
                // BMSPlayerLoader instead of the standard SoloPlayer launcher.
                osuGame.PerformFromScreen(s => s.Push(CreateBmsSongSelectScreen()), new[] { typeof(MainMenu) });
            }
            catch (Exception ex)
            {
                Logger.Log($"BMS main-menu injector: failed to push BMS song select screen: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }
    }
}
