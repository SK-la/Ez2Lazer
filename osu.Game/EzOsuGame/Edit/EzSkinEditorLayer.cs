// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Editor editing layer selected in the scene-bar dropdown. Controls which layout
    /// container the editor HUD layer builds blueprints against, and whether the whole
    /// HUD layer is hidden. Visibility itself is all-or-nothing (see
    /// <see cref="EzSkinEditorLayerOptions"/>): only <see cref="HudOff"/> hides everything.
    /// </summary>
    public enum EzSkinEditorLayer
    {
        /// <summary>Hide the whole HUD layer.</summary>
        HudOff = 0,

        /// <summary>Ez layout layer, global (non-ruleset) HUD components.</summary>
        EzHud,

        /// <summary>Ez layout layer, ruleset-specific HUD components.</summary>
        EzHudRuleset,

        /// <summary>Ez layout layer, ruleset-specific playfield components.</summary>
        EzPlayfieldRuleset,

        // Skin layers are reserved for a later phase. The options are intentionally left out of
        // the enum until the skin-layer editing path exists, so no unused members are declared.
        // SkinHud,
        // SkinHudRuleset,
        // SkinPlayfieldRuleset,
    }

    /// <summary>
    /// Which storage the layer's container belongs to. Only <see cref="Ez"/> layers are editable
    /// in the current phase; <see cref="Skin"/> is reserved for the follow-up skin-layer phase.
    /// </summary>
    public enum EzSkinEditorLayerSource
    {
        Ez,
        Skin,
    }
}
