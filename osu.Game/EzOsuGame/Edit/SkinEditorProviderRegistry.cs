// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Registry for ruleset-specific skin editor providers.
    /// Rulesets should register their provider (by ruleset ID) during static initialization.
    /// </summary>
    public static class SkinEditorProviderRegistry
    {
        private static readonly ConcurrentDictionary<int, Func<ISkinEditorVirtualProvider>> registry = new ConcurrentDictionary<int, Func<ISkinEditorVirtualProvider>>();

        public static void Register(int rulesetId, Func<ISkinEditorVirtualProvider> factory)
        {
            registry[rulesetId] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static ISkinEditorVirtualProvider? Get(int rulesetId)
        {
            if (registry.TryGetValue(rulesetId, out var factory))
                return factory();

            return null;
        }
    }
}
