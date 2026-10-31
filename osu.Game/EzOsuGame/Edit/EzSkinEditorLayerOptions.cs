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
    /// How a single <see cref="EzSkinEditorLayer"/> maps onto a layout container.
    /// </summary>
    public sealed class EzSkinEditorLayerDefinition
    {
        public required EzSkinEditorLayer Layer { get; init; }

        public required EzSkinEditorLayerSource Source { get; init; }

        /// <summary>
        /// Whether the layer only matches components scoped to the current ruleset.
        /// </summary>
        public required bool RulesetScoped { get; init; }

        /// <summary>
        /// The layout target this layer edits. Only meaningful for <see cref="EzSkinEditorLayerSource.Ez"/>.
        /// </summary>
        public required GlobalSkinnableContainers Target { get; init; }

        /// <summary>
        /// Whether this layer hides the whole HUD layer instead of selecting an editing target.
        /// </summary>
        public bool HidesEverything => Layer == EzSkinEditorLayer.HudOff;
    }

    /// <summary>
    /// Availability and metadata for <see cref="EzSkinEditorLayer"/>, per scene.
    /// Single source for the scene-bar layer dropdown and the editor HUD layer's target resolution.
    /// </summary>
    public static class EzSkinEditorLayerOptions
    {
        public static readonly EzSkinEditorLayerDefinition HudOff = new EzSkinEditorLayerDefinition
        {
            Layer = EzSkinEditorLayer.HudOff,
            Source = EzSkinEditorLayerSource.Ez,
            RulesetScoped = false,
            Target = GlobalSkinnableContainers.MainHUDComponents,
        };

        public static readonly EzSkinEditorLayerDefinition EzHud = new EzSkinEditorLayerDefinition
        {
            Layer = EzSkinEditorLayer.EzHud,
            Source = EzSkinEditorLayerSource.Ez,
            RulesetScoped = false,
            Target = GlobalSkinnableContainers.MainHUDComponents,
        };

        public static readonly EzSkinEditorLayerDefinition EzHudRuleset = new EzSkinEditorLayerDefinition
        {
            Layer = EzSkinEditorLayer.EzHudRuleset,
            Source = EzSkinEditorLayerSource.Ez,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.MainHUDComponents,
        };

        public static readonly EzSkinEditorLayerDefinition EzPlayfieldRuleset = new EzSkinEditorLayerDefinition
        {
            Layer = EzSkinEditorLayer.EzPlayfieldRuleset,
            Source = EzSkinEditorLayerSource.Ez,
            RulesetScoped = true,
            Target = GlobalSkinnableContainers.Playfield,
        };

        /// <summary>
        /// Layers offered for a scene. Song select only has the global song-select layout, so it lists
        /// the hidden state and the global Ez HUD layer. Every other scene runs inside gameplay, where
        /// the HUD is shown (even when not interactively editable), so it offers the full layer set.
        /// </summary>
        public static IReadOnlyList<EzSkinEditorLayerDefinition> For(EzSkinEditorSceneType sceneType) => sceneType switch
        {
            EzSkinEditorSceneType.SongSelect => new[] { HudOff, EzHud },
            _ => new[] { HudOff, EzHud, EzHudRuleset, EzPlayfieldRuleset },
        };

        public static EzSkinEditorLayerDefinition Get(EzSkinEditorLayer layer) => layer switch
        {
            EzSkinEditorLayer.EzHud => EzHud,
            EzSkinEditorLayer.EzHudRuleset => EzHudRuleset,
            EzSkinEditorLayer.EzPlayfieldRuleset => EzPlayfieldRuleset,
            _ => HudOff,
        };

        /// <summary>
        /// The layout lookup a layer edits in a scene, given the ruleset active in the editor.
        /// Song select only has a single non-ruleset container; gameplay layers map to their
        /// definition's target. Scenes without an editable HUD and the hidden state yield null.
        /// </summary>
        public static GlobalSkinnableContainerLookup? ResolveLookup(EzSkinEditorSceneType sceneType, EzSkinEditorLayer layer, RulesetInfo? ruleset)
        {
            if (layer == EzSkinEditorLayer.HudOff)
                return null;

            if (!EzSkinEditorSceneRegistry.SupportsHudLayer(sceneType))
                return null;

            if (sceneType == EzSkinEditorSceneType.SongSelect)
                return new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect);

            var definition = Get(layer);

            if (definition.RulesetScoped && ruleset == null)
                return null;

            return new GlobalSkinnableContainerLookup(definition.Target, definition.RulesetScoped ? ruleset : null);
        }

        /// <summary>
        /// Whether a layer can actually build an editing target right now (used for interaction gating).
        /// </summary>
        public static bool HasEditingTarget(EzSkinEditorSceneType sceneType, EzSkinEditorLayer layer, RulesetInfo? ruleset) => ResolveLookup(sceneType, layer, ruleset) != null;

        /// <summary>
        /// Localised dropdown label. Ruleset-scoped layers substitute the active ruleset name for the
        /// "(mode)" word, so the option reads like "Ez HUD (osu!mania)".
        /// </summary>
        public static LocalisableString GetDisplayName(EzSkinEditorLayer layer, RulesetInfo? ruleset)
        {
            switch (layer)
            {
                case EzSkinEditorLayer.EzHud:
                    return EzEditorStrings.LAYER_EZ_HUD;

                case EzSkinEditorLayer.EzHudRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_EZ_HUD_RULESET.ToString(), getRulesetName(ruleset));

                case EzSkinEditorLayer.EzPlayfieldRuleset:
                    return LocalisableString.Format(EzEditorStrings.LAYER_EZ_PLAYFIELD_RULESET.ToString(), getRulesetName(ruleset));

                default:
                    return EzEditorStrings.LAYER_HUD_OFF;
            }
        }

        private static string getRulesetName(RulesetInfo? ruleset) => ruleset?.Name ?? EzEditorStrings.LAYER_MODE_UNKNOWN.ToString();

        /// <summary>
        /// Clamps a requested layer to what a scene actually offers. Non-HUD scenes leave the layer
        /// untouched so a HUD scene reached later restores the user's choice instead of being stuck on
        /// <see cref="EzSkinEditorLayer.HudOff"/>. HUD scenes without the requested layer fall back to
        /// their default non-hidden layer (e.g. a ruleset layer on song select becomes the global Ez HUD).
        /// </summary>
        public static EzSkinEditorLayer Normalize(EzSkinEditorSceneType sceneType, EzSkinEditorLayer layer)
        {
            var offered = For(sceneType);

            if (offered.Any(d => d.Layer == layer))
                return layer;

            if (!EzSkinEditorSceneRegistry.SupportsHudLayer(sceneType))
                return layer;

            var fallback = offered.FirstOrDefault(d => !d.HidesEverything);
            return fallback?.Layer ?? EzSkinEditorLayer.HudOff;
        }
    }
}
