// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// How a single <see cref="EzHUDLayer"/> maps onto a layout container.
    /// </summary>
    public sealed class EzSkinEditorLayerDefinition
    {
        public required EzHUDLayer Layer { get; init; }

        /// <summary>
        /// Whether the layer only matches components scoped to the current ruleset.
        /// </summary>
        public required bool RulesetScoped { get; init; }

        /// <summary>
        /// The layout target this layer edits.
        /// </summary>
        public required GlobalSkinnableContainers Target { get; init; }

        /// <summary>
        /// Whether layout changes persist through <see cref="Layout.EzConfigLayoutStore"/> (Ez layers)
        /// instead of the active skin.
        /// </summary>
        public required bool UsesEzLayoutStore { get; init; }
    }

    /// <summary>
    /// Availability and metadata for <see cref="EzHUDLayer"/>, per scene.
    /// Single source for the scene-bar layer dropdown and the editor HUD layer's target resolution.
    /// </summary>
    public static class EzSkinEditorLayerOptions
    {
        public static readonly EzSkinEditorLayerDefinition HUD_OFF = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.HudOff,
            RulesetScoped = false,
            Target = GlobalSkinnableContainers.MainHUDComponents,
            UsesEzLayoutStore = false,
        };

        public static readonly EzSkinEditorLayerDefinition EZ_HUD = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.EzHud,
            RulesetScoped = false,
            Target = GlobalSkinnableContainers.MainHUDComponents,
            UsesEzLayoutStore = true,
        };

        public static readonly EzSkinEditorLayerDefinition EZ_HUD_RULESET = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.EzHudRuleset,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.MainHUDComponents,
            UsesEzLayoutStore = true,
        };

        public static readonly EzSkinEditorLayerDefinition EZ_PLAYFIELD_RULESET = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.EzPlayfieldRuleset,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.Playfield,
            UsesEzLayoutStore = true,
        };

        public static readonly EzSkinEditorLayerDefinition SKIN_HUD = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.SkinHud,
            RulesetScoped = false,
            Target = GlobalSkinnableContainers.MainHUDComponents,
            UsesEzLayoutStore = false,
        };

        public static readonly EzSkinEditorLayerDefinition SKIN_HUD_RULESET = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.SkinHudRuleset,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.MainHUDComponents,
            UsesEzLayoutStore = false,
        };

        public static readonly EzSkinEditorLayerDefinition SKIN_PLAYFIELD_RULESET = new EzSkinEditorLayerDefinition
        {
            Layer = EzHUDLayer.SkinPlayfieldRuleset,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.Playfield,
            UsesEzLayoutStore = false,
        };

        private static readonly EzSkinEditorLayerDefinition[] gameplay_layers =
        {
            HUD_OFF, EZ_HUD, EZ_HUD_RULESET, EZ_PLAYFIELD_RULESET,
            SKIN_HUD, SKIN_HUD_RULESET, SKIN_PLAYFIELD_RULESET,
        };

        private static readonly EzSkinEditorLayerDefinition[] ez_only_layers =
        {
            HUD_OFF, EZ_HUD,
        };

        /// <summary>
        /// Layers offered for a scene. Song select only has the global song-select layout, so it lists
        /// the hidden state and the global Ez / skin HUD layers. Results and music player are Ez JSON
        /// only. HUD-capable gameplay scenes offer the full layer set.
        /// </summary>
        public static IReadOnlyList<EzSkinEditorLayerDefinition> For(EzEditorSceneType sceneType) => sceneType switch
        {
            EzEditorSceneType.SongSelectHud => new[] { HUD_OFF, EZ_HUD, SKIN_HUD },
            EzEditorSceneType.ResultsHud or EzEditorSceneType.MusicPlayerHud => ez_only_layers,
            _ => gameplay_layers,
        };

        public static EzSkinEditorLayerDefinition Get(EzHUDLayer layer) => layer switch
        {
            EzHUDLayer.EzHud => EZ_HUD,
            EzHUDLayer.EzHudRuleset => EZ_HUD_RULESET,
            EzHUDLayer.EzPlayfieldRuleset => EZ_PLAYFIELD_RULESET,
            EzHUDLayer.SkinHud => SKIN_HUD,
            EzHUDLayer.SkinHudRuleset => SKIN_HUD_RULESET,
            EzHUDLayer.SkinPlayfieldRuleset => SKIN_PLAYFIELD_RULESET,
            _ => HUD_OFF,
        };

        public static bool UsesEzLayoutStore(EzHUDLayer layer) => Get(layer).UsesEzLayoutStore;

        /// <summary>
        /// The layout lookup a layer edits in a scene, given the ruleset active in the editor.
        /// Song select only has a single non-ruleset container; gameplay layers map to their
        /// definition's target. Scenes without an editable HUD and the hidden state yield null.
        /// </summary>
        public static GlobalSkinnableContainerLookup? ResolveLookup(EzEditorSceneType sceneType, EzHUDLayer layer, RulesetInfo? ruleset)
        {
            if (layer == EzHUDLayer.HudOff)
                return null;

            if (!EzSkinEditorSceneRegistry.SupportsHudLayer(sceneType))
                return null;

            if (sceneType == EzEditorSceneType.SongSelectHud)
                return new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect);

            if (sceneType == EzEditorSceneType.ResultsHud)
                return new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results);

            if (sceneType == EzEditorSceneType.MusicPlayerHud)
                return new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MusicPlayer);

            var definition = Get(layer);

            if (definition.RulesetScoped && ruleset == null)
                return null;

            return new GlobalSkinnableContainerLookup(definition.Target, definition.RulesetScoped ? ruleset : null);
        }

        /// <summary>
        /// Whether a layer can actually build an editing target right now (used for interaction gating).
        /// </summary>
        public static bool HasEditingTarget(EzEditorSceneType sceneType, EzHUDLayer layer, RulesetInfo? ruleset) => ResolveLookup(sceneType, layer, ruleset) != null;

        /// <summary>
        /// Localised dropdown label. Ruleset-scoped layers substitute the active ruleset name for the
        /// "(mode)" word, so the option reads like "Ez HUD (osu!mania)".
        /// </summary>
        public static LocalisableString GetDisplayName(EzHUDLayer layer, RulesetInfo? ruleset)
        {
            switch (layer)
            {
                case EzHUDLayer.EzHud:
                    return EzEditorStrings.LAYER_EZ_HUD;

                case EzHUDLayer.EzHudRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_EZ_HUD_RULESET.ToString(), getRulesetName(ruleset));

                case EzHUDLayer.EzPlayfieldRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_EZ_PLAYFIELD_RULESET.ToString(), getRulesetName(ruleset));

                case EzHUDLayer.SkinHud:
                    return EzEditorStrings.LAYER_SKIN_HUD;

                case EzHUDLayer.SkinHudRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_SKIN_HUD_RULESET.ToString(), getRulesetName(ruleset));

                case EzHUDLayer.SkinPlayfieldRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_SKIN_PLAYFIELD_RULESET.ToString(), getRulesetName(ruleset));

                default:
                    return EzEditorStrings.LAYER_HUD_OFF;
            }
        }

        private static string getRulesetName(RulesetInfo? ruleset) => ruleset?.Name ?? EzEditorStrings.LAYER_MODE_UNKNOWN.ToString();

        /// <summary>
        /// Clamps a requested layer to what a scene actually offers. Non-HUD scenes leave the layer
        /// untouched so a HUD scene reached later restores the user's choice instead of being stuck on
        /// <see cref="EzHUDLayer.HudOff"/>. HUD scenes without the requested layer fall back to
        /// their default non-hidden layer (e.g. a ruleset layer on song select becomes the global Ez HUD).
        /// </summary>
        public static EzHUDLayer Normalize(EzEditorSceneType sceneType, EzHUDLayer layer)
        {
            var offered = For(sceneType);

            if (offered.Any(d => d.Layer == layer))
                return layer;

            if (!EzSkinEditorSceneRegistry.SupportsHudLayer(sceneType))
                return layer;

            var fallback = offered.FirstOrDefault(d => d.Layer != EzHUDLayer.HudOff);
            return fallback?.Layer ?? EzHUDLayer.HudOff;
        }
    }
}