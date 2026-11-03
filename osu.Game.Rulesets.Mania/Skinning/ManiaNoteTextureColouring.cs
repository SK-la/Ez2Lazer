// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaNoteTextureColouring
    {
        public static bool ShouldUseTintBaseNoteTextures(bool colorSettingsEnabled, bool gameDivisorNoteColouringEnabled)
            => colorSettingsEnabled || gameDivisorNoteColouringEnabled;
    }
}
