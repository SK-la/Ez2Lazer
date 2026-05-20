// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// Drives <see cref="BmsKeysoundManager"/> background sound events from the gameplay clock.
    ///
    /// Used by the BMS DrawableRuleset on the standard play path, where <see cref="BmsPlayer"/>
    /// itself isn't necessarily instantiated and therefore wouldn't otherwise dispatch BMS BGM
    /// events. Hit object keysounds remain handled via the regular skin/sample pipeline (through
    /// <see cref="BMSExternalSampleSkin"/>), this driver only covers the non-note BGM stream.
    /// </summary>
    internal partial class BmsBackgroundSoundDriver : Drawable
    {
        private readonly string bmsFolder;
        private readonly IReadOnlyList<BmsBackgroundSoundEvent> events;

        private BmsKeysoundManager? keysoundManager;
        private IGameplayClock? gameplayClock;

        public BmsBackgroundSoundDriver(string bmsFolder, IReadOnlyList<BmsBackgroundSoundEvent> events)
        {
            this.bmsFolder = bmsFolder;
            this.events = events;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader(true)]
        private void load(AudioManager audioManager, IGameplayClock? clock)
        {
            // Make audio available to other non-Drawable BMS helpers (e.g. external sample skin).
            BmsRuntimeAudioContext.Register(audioManager);

            gameplayClock = clock;

            try
            {
                keysoundManager = new BmsKeysoundManager(audioManager, bmsFolder);
                keysoundManager.SetBackgroundSoundEvents(events);
            }
            catch (System.Exception ex)
            {
                Logger.Log($"[BMS] BGM driver failed to initialise: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                keysoundManager = null;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (keysoundManager == null || gameplayClock == null)
                return;

            if (keysoundManager.IsDisposed)
                return;

            keysoundManager.Update(gameplayClock.CurrentTime);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            keysoundManager?.Dispose();
            keysoundManager = null;
        }
    }
}
