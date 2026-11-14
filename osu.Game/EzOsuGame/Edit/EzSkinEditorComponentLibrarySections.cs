// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Edit.Components;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorComponentLibrarySections
    {
        public static IEnumerable<EzSkinEditorSettingsGroup> Create(bool rulesetScoped)
        {
            foreach (EzHudComponentFamily family in Enum.GetValues<EzHudComponentFamily>())
                yield return new EzSkinEditorComponentLibrarySection(rulesetScoped, family);
        }
    }
}
