// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Edit
{
    public static class SkinEditorProviderResolver
    {
        private const int mania_ruleset_online_id = 3;

        private const string mania_provider_type_name = "osu.Game.Rulesets.Mania.EzMania.Editor.EzSkinLNEditorProvider, osu.Game.Rulesets.Mania";

        public static ISkinEditorVirtualProvider? Resolve(IBeatmap? beatmap)
        {
            int rulesetId = beatmap?.BeatmapInfo.Ruleset.OnlineID ?? 0;

            if (rulesetId != 0)
            {
                var fromRegistry = SkinEditorProviderRegistry.Get(rulesetId);
                if (fromRegistry != null)
                    return fromRegistry;
            }

            // Default to mania for editor previews when no beatmap ruleset is available.
            return SkinEditorProviderRegistry.Get(mania_ruleset_online_id) ?? ensureManiaProviderRegistered();
        }

        private static ISkinEditorVirtualProvider? ensureManiaProviderRegistered()
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                                        .FirstOrDefault(a => a.GetName().Name == "osu.Game.Rulesets.Mania")
                               ?? Assembly.Load(new AssemblyName("osu.Game.Rulesets.Mania"));

                _ = assembly.GetType("osu.Game.Rulesets.Mania.EzMania.Editor.EzSkinLNEditorProvider", throwOnError: false);
            }
            catch
            {
                // best-effort: ruleset assembly may not be present in trimmed/test hosts
            }

            return SkinEditorProviderRegistry.Get(mania_ruleset_online_id);
        }
    }
}
