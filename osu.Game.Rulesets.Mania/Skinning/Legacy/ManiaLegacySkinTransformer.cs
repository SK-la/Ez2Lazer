// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public class ManiaLegacySkinTransformer : LegacySkinTransformer
    {
        public override bool IsProvidingLegacyResources => base.IsProvidingLegacyResources || hasKeyTexture.Value;

        /// <summary>
        /// Mapping of <see cref="HitResult"/> to their corresponding
        /// <see cref="LegacyManiaSkinConfigurationLookups"/> value.
        /// </summary>
        private static readonly IReadOnlyDictionary<HitResult, LegacyManiaSkinConfigurationLookups> hit_result_mapping
            = new Dictionary<HitResult, LegacyManiaSkinConfigurationLookups>
            {
                { HitResult.Perfect, LegacyManiaSkinConfigurationLookups.Hit300g },
                { HitResult.Great, LegacyManiaSkinConfigurationLookups.Hit300 },
                { HitResult.Good, LegacyManiaSkinConfigurationLookups.Hit200 },
                { HitResult.Ok, LegacyManiaSkinConfigurationLookups.Hit100 },
                { HitResult.Meh, LegacyManiaSkinConfigurationLookups.Hit50 },
                { HitResult.Miss, LegacyManiaSkinConfigurationLookups.Hit0 }
            };

        /// <summary>
        /// Mapping of <see cref="HitResult"/> to their corresponding
        /// default filenames.
        /// </summary>
        private static readonly IReadOnlyDictionary<HitResult, string> default_hit_result_skin_filenames
            = new Dictionary<HitResult, string>
            {
                { HitResult.Perfect, "mania-hit300g" },
                { HitResult.Great, "mania-hit300" },
                { HitResult.Good, "mania-hit200" },
                { HitResult.Ok, "mania-hit100" },
                { HitResult.Meh, "mania-hit50" },
                { HitResult.Miss, "mania-hit0" }
            };

        private readonly Lazy<bool> isLegacySkin;

        /// <summary>
        /// Whether texture for the keys exists.
        /// Used to determine if the mania ruleset is skinned.
        /// </summary>
        private readonly Lazy<bool> hasKeyTexture;

        private readonly ManiaBeatmap beatmap;
        private readonly bool isBeatmapConverted;
        private readonly Lazy<bool> noteTimingColourTextureGroupsRegistered;

        public ManiaLegacySkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;
            isBeatmapConverted = !beatmap.BeatmapInfo.Ruleset.Equals(new ManiaRuleset().RulesetInfo);

            isLegacySkin = new Lazy<bool>(() => GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version) != null);
            hasKeyTexture = new Lazy<bool>(() =>
            {
                string keyImage = this.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.KeyImage, 0)?.Value ?? "mania-key1";
                return this.GetAnimation(keyImage, true, true) != null;
            });

            noteTimingColourTextureGroupsRegistered = new Lazy<bool>(() =>
            {
                registerNoteTimingColourTextureGroups();
                return true;
            });
        }

        public override Drawable GetDrawableComponent(ISkinComponentLookup lookup)
        {
            switch (lookup)
            {
                case GlobalSkinnableContainerLookup containerLookup:
                    // Modifications for global components.
                    if (containerLookup.Ruleset == null)
                        return base.GetDrawableComponent(lookup);

                    // we don't have enough assets to display these components (this is especially the case on a "beatmap" skin).
                    if (!IsProvidingLegacyResources)
                        return null;

                    switch (containerLookup.Lookup)
                    {
                        case GlobalSkinnableContainers.MainHUDComponents:
                            return new DefaultSkinComponentsContainer(container =>
                            {
                                var combo = container.ChildrenOfType<LegacyManiaComboCounter>().FirstOrDefault();
                                var spectatorList = container.OfType<SpectatorList>().FirstOrDefault();
                                var leaderboard = container.OfType<DrawableGameplayLeaderboard>().FirstOrDefault();

                                if (combo != null)
                                {
                                    combo.Anchor = Anchor.TopCentre;
                                    combo.Origin = Anchor.Centre;
                                    combo.Y = this.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.ComboPosition)?.Value ?? 0;
                                }

                                if (spectatorList != null)
                                {
                                    spectatorList.Anchor = Anchor.BottomLeft;
                                    spectatorList.Origin = Anchor.BottomLeft;
                                    spectatorList.Position = new Vector2(10, -10);
                                }

                                if (leaderboard != null)
                                {
                                    leaderboard.Anchor = Anchor.CentreLeft;
                                    leaderboard.Origin = Anchor.CentreLeft;
                                    leaderboard.X = 10;
                                }

                                foreach (var d in container.OfType<ISerialisableDrawable>())
                                    d.UsesFixedAnchor = true;
                            })
                            {
                                new LegacyManiaComboCounter(),
                                new SpectatorList(),
                                new DrawableGameplayLeaderboard(),
                            };
                    }

                    return null;

                case SkinComponentLookup<HitResult> resultComponent:
                    return getResult(resultComponent.Component);

                case ManiaSkinComponentLookup maniaComponent:
                    if (!isLegacySkin.Value || !hasKeyTexture.Value)
                        return null;

                    _ = noteTimingColourTextureGroupsRegistered.Value;

                    switch (maniaComponent.Component)
                    {
                        case ManiaSkinComponents.ColumnBackground:
                            return new LegacyColumnBackground();

                        case ManiaSkinComponents.HitTarget:
                            // Legacy skins sandwich the hit target between the column background and the column light.
                            // To preserve this ordering, it's created manually inside LegacyStageBackground.
                            return Drawable.Empty();

                        case ManiaSkinComponents.KeyArea:
                            return new LegacyKeyArea();

                        case ManiaSkinComponents.Note:
                            return new LegacyNotePiece();

                        case ManiaSkinComponents.HoldNoteHead:
                            return new LegacyHoldNoteHeadPiece();

                        case ManiaSkinComponents.HoldNoteTail:
                            return new LegacyHoldNoteTailPiece();

                        case ManiaSkinComponents.HoldNoteBody:
                            return new LegacyBodyPiece();

                        case ManiaSkinComponents.HitExplosion:
                            return new LegacyHitExplosion();

                        case ManiaSkinComponents.StageBackground:
                            return new LegacyStageBackground();

                        case ManiaSkinComponents.StageForeground:
                            return new LegacyStageForeground();

                        case ManiaSkinComponents.BarLine:
                            return new LegacyBarLine();

                        default:
                            throw new UnsupportedSkinComponentException(lookup);
                    }
            }

            return base.GetDrawableComponent(lookup);
        }

        private Drawable getResult(HitResult result)
        {
            if (!hit_result_mapping.TryGetValue(result, out var value))
                return null;

            string filename = this.GetManiaSkinConfig<string>(value)?.Value
                              ?? default_hit_result_skin_filenames[result];

            var animation = this.GetAnimation(filename, true, true, frameLength: 1000 / 20d);
            return animation == null ? null : new LegacyManiaJudgementPiece(result, animation);
        }

        public override ISample GetSample(ISampleInfo sampleInfo)
        {
            // layered hit sounds never play in mania-native beatmaps (but do play on converts)
            if (sampleInfo is ConvertHitObjectParser.LegacyHitSampleInfo legacySample && legacySample.IsLayered && !isBeatmapConverted)
                return new SampleVirtual();

            return base.GetSample(sampleInfo);
        }

        public override IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                return base.GetConfig<LegacyManiaSkinConfigurationLookup, TValue>(new LegacyManiaSkinConfigurationLookup(beatmap.TotalColumns, maniaLookup.Lookup, maniaLookup.ColumnIndex));
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }

        private void registerNoteTimingColourTextureGroups()
        {
            if (Skin is not LegacySkin legacySkin)
                return;

            registerNoteTimingColourTextureGroup(LegacyNotePiece.CreateTimingColourTextureGroupName(beatmap.TotalColumns, LegacyManiaSkinConfigurationLookups.NoteImage), LegacyManiaSkinConfigurationLookups.NoteImage);
            registerNoteTimingColourTextureGroup(LegacyNotePiece.CreateTimingColourTextureGroupName(beatmap.TotalColumns, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage), LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, LegacyManiaSkinConfigurationLookups.NoteImage);

            void registerNoteTimingColourTextureGroup(string groupName, params LegacyManiaSkinConfigurationLookups[] lookupFallbacks)
                => legacySkin.RegisterManiaTimingColourTextureGroup(groupName, getTextureNameFallbacksForLookups(lookupFallbacks));
        }

        private IEnumerable<IEnumerable<string>> getTextureNameFallbacksForLookups(params LegacyManiaSkinConfigurationLookups[] lookupFallbacks)
        {
            for (int column = 0; column < beatmap.TotalColumns; column++)
                yield return lookupFallbacks.Select(lookup => getTextureNameForLookup(column, lookup));
        }

        private string getTextureNameForLookup(int column, LegacyManiaSkinConfigurationLookups lookup)
        {
            string suffix = lookup switch
            {
                LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage => "H",
                LegacyManiaSkinConfigurationLookups.HoldNoteTailImage => "T",
                _ => string.Empty
            };

            string fallbackColumnIndex;
            var stage = beatmap.GetStageForColumnIndex(column);

            if (stage.IsSpecialColumn(column % stage.Columns))
                fallbackColumnIndex = "S";
            else
            {
                int columnInStage = column % stage.Columns;
                int distanceToEdge = Math.Min(columnInStage, stage.Columns - 1 - columnInStage);
                fallbackColumnIndex = distanceToEdge % 2 == 0 ? "1" : "2";
            }

            return this.GetManiaSkinConfig<string>(lookup, column)?.Value
                   ?? $"mania-note{fallbackColumnIndex}{suffix}";
        }
    }
}
