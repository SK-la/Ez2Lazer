// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests
{
    [SetUpFixture]
    public class Ez2TestSetup
    {
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            GlobalConfigStore.EnsureInitialized();
            GlobalConfigStore.UseDevelopmentEndpointsForTests = true;
        }
    }
}
