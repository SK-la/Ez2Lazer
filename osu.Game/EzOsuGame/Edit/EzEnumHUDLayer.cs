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
    /// 此类型有误导性，实际是 HUD Layer。应改名为 EzSkinHudLayer，文件层级上移。
    public enum EzHUDLayer
    {
        /// <summary>Hide the whole HUD layer.</summary>
        HudOff = 0,

        /// <summary>Ez layout layer, global (non-ruleset) HUD components.</summary>
        EzHud,

        /// <summary>Ez layout layer, ruleset-specific HUD components.</summary>
        EzHudRuleset,

        /// <summary>Ez layout layer, ruleset-specific playfield components.</summary>
        EzPlayfieldRuleset,

        /// <summary>Current skin layout, global (non-ruleset) HUD components.</summary>
        SkinHud,

        /// <summary>Current skin layout, ruleset-specific HUD components.</summary>
        SkinHudRuleset,

        /// <summary>Current skin layout, ruleset-specific playfield components.</summary>
        SkinPlayfieldRuleset,
    }
}
