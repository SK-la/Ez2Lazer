// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.EzOsuGame.Configuration
{
    public static class GlobalConfigStore
    {
        private static readonly object initialization_lock = new object();
        private static Storage? fallbackStorage;

        public static OsuConfigManager Config { get; set; } = null!;

        private static Ez2ConfigManager? ezConfig;

        public static Ez2ConfigManager EzConfig
        {
            get => ezConfig ?? EnsureInitialized();
            set => ezConfig = value;
        }

        /// <summary>
        /// When set, <see cref="OsuGameBase.CreateEndpoints"/> uses development API endpoints (dev.ppy.sh)
        /// so official tests that assume dev.ppy.sh URLs keep working despite Ez server presets.
        /// Enabled by <c>Ez2TestSetup</c> during DEBUG test runs.
        /// </summary>
        public static bool UseDevelopmentEndpointsForTests { get; set; }

        /// <summary>
        /// Ensures <see cref="EzConfig"/> exists. Used by unit tests and early gameplay code paths before <see cref="OsuGameBase"/> finishes loading.
        /// </summary>
        public static Ez2ConfigManager EnsureInitialized(Storage? storage = null)
        {
            if (ezConfig != null)
                return ezConfig;

            lock (initialization_lock)
            {
                if (ezConfig != null)
                    return ezConfig;

                storage ??= fallbackStorage ??= new TemporaryNativeStorage($"ez2-config-{Guid.NewGuid()}");
                var ezConfigNew = new Ez2ConfigManager(storage);
                return ezConfigNew;
            }
        }
    }
}
