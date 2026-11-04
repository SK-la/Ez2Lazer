// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaDivisorNoteColouring
    {
        public static bool IsEnabled(bool inEditorCompose, bool gameDivisorNoteColouringEnabled, bool editorDivisorNoteColouringEnabled)
            => inEditorCompose ? editorDivisorNoteColouringEnabled : gameDivisorNoteColouringEnabled;
    }
}
