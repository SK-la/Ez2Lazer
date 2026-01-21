// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Beatmaps.Formats;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    public class LegacySkin : Skin
    {
        protected virtual bool AllowManiaConfigLookups => true;

        /// <summary>
        /// Whether this skin can use samples with a custom bank (custom sample set in stable terminology).
        /// Added in order to match sample lookup logic from stable (in stable, only the beatmap skin could use samples with a custom sample bank).
        /// </summary>
        protected virtual bool UseCustomSampleBanks => false;

        private readonly Dictionary<int, LegacyManiaSkinConfiguration> maniaConfigurations = new Dictionary<int, LegacyManiaSkinConfiguration>();

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public LegacySkin(SkinInfo skin, IStorageResourceProvider resources)
            : this(skin, resources, null)
        {
        }

        /// <summary>
        /// Construct a new legacy skin instance.
        /// </summary>
        /// <param name="skin">The model for this skin.</param>
        /// <param name="resources">Access to raw game resources.</param>
        /// <param name="fallbackStore">An optional fallback store which will be used for file lookups that are not serviced by realm user storage.</param>
        /// <param name="configurationFilename">The user-facing filename of the configuration file to be parsed. Can accept an .osu or skin.ini file.</param>
        protected LegacySkin(SkinInfo skin, IStorageResourceProvider? resources, IResourceStore<byte[]>? fallbackStore, string configurationFilename = @"skin.ini")
            : base(skin, resources, fallbackStore, configurationFilename)
        {
        }

        protected override IResourceStore<TextureUpload> CreateTextureLoaderStore(IStorageResourceProvider resources, IResourceStore<byte[]> storage)
            => new LegacyTextureLoaderStore(base.CreateTextureLoaderStore(resources, storage));

        protected override void ParseConfigurationStream(Stream stream)
        {
            base.ParseConfigurationStream(stream);

            stream.Seek(0, SeekOrigin.Begin);

            using (LineBufferedReader reader = new LineBufferedReader(stream))
            {
                var maniaList = new LegacyManiaSkinDecoder().Decode(reader);

                foreach (var config in maniaList)
                    maniaConfigurations[config.Keys] = config;
            }
        }

        /// <summary>
        /// Gets a list of script files in the skin.
        /// </summary>
        /// <returns>A list of script file names.</returns>
        protected override IEnumerable<string> GetScriptFiles()
        {
            // Look for .lua script files in the skin
            return SkinInfo.Files.Where(f => f.Filename.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                                 .Select(f => f.Filename);
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")] // for `wasHit` assignments used in `finally` debug logic
        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            bool wasHit = true;

            try
            {
                switch (lookup)
                {
                    case GlobalSkinColours colour:
                        switch (colour)
                        {
                            case GlobalSkinColours.ComboColours:
                                var comboColours = Configuration.ComboColours;
                                if (comboColours != null)
                                    return SkinUtils.As<TValue>(new Bindable<IReadOnlyList<Color4>>(comboColours));

                                break;

                            default:
                                return SkinUtils.As<TValue>(getCustomColour(Configuration, colour.ToString()));
                        }

                        break;

                    case SkinConfiguration.LegacySetting setting:
                        switch (setting)
                        {
                            case SkinConfiguration.LegacySetting.Version:
                                return SkinUtils.As<TValue>(new Bindable<decimal>(Configuration.LegacyVersion ?? LegacySkinConfiguration.LATEST_VERSION));
                        }

                        break;

                    // handled by ruleset-specific skin classes.
                    case LegacyManiaSkinConfigurationLookup maniaLookup:
                        wasHit = false;
                        break;

                    case SkinCustomColourLookup customColour:
                        return SkinUtils.As<TValue>(getCustomColour(Configuration, customColour.Lookup.ToString()));

                    case LegacySkinHitCircleLookup legacyHitCircleLookup:
                        switch (legacyHitCircleLookup.Detail)
                        {
                            case LegacySkinHitCircleLookup.DetailType.HitCircleNormalPathTint:
                                return SkinUtils.As<TValue>(new Bindable<Color4>(Configuration.HitCircleNormalPathTint ?? Color4.White));

                            case LegacySkinHitCircleLookup.DetailType.HitCircleHoverPathTint:
                                return SkinUtils.As<TValue>(new Bindable<Color4>(Configuration.HitCircleHoverPathTint ?? Color4.White));

                            case LegacySkinHitCircleLookup.DetailType.Count:
                                wasHit = false;
                                break;
                        }

                        break;

                    case LegacySkinNoteSheetLookup legacyNoteSheetLookup:
                        return SkinUtils.As<TValue>(new Bindable<float>(Configuration.NoteBodyWidth ?? 128));

                    case SkinConfigurationLookup skinLookup:
                        return handleLegacySkinLookup(skinLookup);
                }

                wasHit = false;
                return null;
            }
            finally
            {
                LogLookupDebug(this, lookup, wasHit ? LookupDebugType.Hit : LookupDebugType.Miss);
            }
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (base.GetDrawableComponent(lookup) is Drawable d)
                return d;

            switch (lookup)
            {
                case SkinnableSprite.SpriteComponentLookup sprite:
                    return this.GetAnimation(sprite.LookupName, false, false, maxSize: sprite.MaxSize);

                case SkinComponentsContainerLookup _:
                    return null;

                case GameplaySkinComponentLookup<HitResult> resultComponent:
                    return getResult(resultComponent.Component);

                case GameplaySkinComponentLookup<BarHitErrorMeter> bhe:
                    if (Configuration.LegacyVersion < 2.2m)
                        return null;

                    break;

                case JudgementLineStyleLookup judgementLine:
                    return findProvider(nameof(JudgementLineStyleLookup.Type), judgementLine.Type);

                default:
                    return findProvider(nameof(ISkinComponentLookup.Lookup), lookup.Lookup);
            }

            return null;
        }

        private Drawable? findProvider(string lookupName, object lookupValue)
        {
            var providedType = GetType().Assembly.GetTypes()
                                        .Where(t => !t.IsInterface && !t.IsAbstract)
                                        .FirstOrDefault(t =>
                                        {
                                            var interfaces = t.GetInterfaces();

                                            return interfaces.Any(i => i.IsGenericType &&
                                                                       i.GetGenericTypeDefinition() == typeof(ILegacySkinComponentProvider<,>) &&
                                                                       i.GenericTypeArguments[1].GetProperty(lookupName)?.PropertyType == lookupValue.GetType());
                                        });

            if (providedType == null)
                return null;

            var constructor = providedType.GetConstructor(new[] { typeof(LegacySkinConfiguration), typeof(ISkin) });

            if (constructor == null)
                return null;

            var instance = constructor.Invoke(new object[] { Configuration, this });

            var interfaceType = instance.GetType().GetInterfaces()
                                        .First(i => i.IsGenericType &&
                                                    i.GetGenericTypeDefinition() == typeof(ILegacySkinComponentProvider<,>) &&
                                                    i.GenericTypeArguments[1].GetProperty(lookupName)?.PropertyType == lookupValue.GetType());

            var providerType = interfaceType.GetGenericTypeDefinition().MakeGenericType(interfaceType.GenericTypeArguments[0], lookupValue.GetType());

            var methodInfo = providerType.GetMethod(nameof(ILegacySkinComponentProvider<Drawable, object>.GetDrawableComponent),
                new[] { lookupValue.GetType() });

            var component = methodInfo?.Invoke(instance, new[] { lookupValue }) as Drawable;

            return component;
        }

        private IBindable<TValue>? handleLegacySkinLookup<TValue>(SkinConfigurationLookup lookup)
        {
            switch (lookup.Lookup)
            {
                case SkinConfiguration.SliderStyle:
                {
                    var style = Configuration.SliderStyle ?? (Configuration.Version < 2.0m ? SliderStyle.Segmented : SliderStyle.Gradient);
                    return SkinUtils.As<TValue>(new Bindable<SliderStyle>(style));
                }

                case SkinConfiguration.ScoringVisible:
                    return SkinUtils.As<TValue>(new Bindable<bool>(Configuration.ScoringVisible ?? true));

                case SkinConfiguration.ComboPerformed:
                    return SkinUtils.As<TValue>(new Bindable<bool>(Configuration.ComboPerformed ?? true));

                case SkinConfiguration.ComboTaskbarPopover:
                    return SkinUtils.As<TValue>(new Bindable<bool>(Configuration.ComboTaskbarPopover ?? true));

                case SkinConfiguration.HitErrorStyle:
                    return SkinUtils.As<TValue>(new Bindable<HitErrorStyle>(Configuration.HitErrorStyle ?? HitErrorStyle.Bottom));

                case SkinConfiguration.MainHUDLayoutMode:
                    return SkinUtils.As<TValue>(new Bindable<HUDLayoutMode>(Configuration.MainHUDLayoutMode ?? HUDLayoutMode.New));

                case SkinConfiguration.InputOverlayMode:
                    return SkinUtils.As<TValue>(new Bindable<InputOverlayMode>(Configuration.InputOverlayMode ?? InputOverlayMode.Bottom));

                case SkinConfiguration.SongMetadataView:
                    return SkinUtils.As<TValue>(new Bindable<SongMetadataView>(Configuration.SongMetadataView ?? SongMetadataView.Default));
            }

            return null;
        }

        private IBindable<Color4>? getCustomColour(LegacySkinConfiguration configuration, string lookup)
        {
            if (configuration.CustomColours != null &&
                configuration.CustomColours.TryGetValue(lookup, out Color4 col))
                return new Bindable<Color4>(col);

            return null;
        }

        [CanBeNull]
        protected virtual Drawable? getResult(HitResult result)
        {
            return null;
        }

        public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
        {
            float ratio = 2;
            var texture = Textures?.Get($"{componentName}@2x", wrapModeS, wrapModeT);

            if (texture == null)
            {
                ratio = 1;
                texture = Textures?.Get(componentName, wrapModeS, wrapModeT);
            }

            if (texture == null && !componentName.EndsWith(@"@2x", StringComparison.Ordinal))
            {
                componentName = componentName.Replace(@"@2x", string.Empty);

                string twoTimesFilename = $"{Path.ChangeExtension(componentName, null)}@2x{Path.GetExtension(componentName)}";

                texture = Textures?.Get(twoTimesFilename, wrapModeS, wrapModeT);

                if (texture != null)
                    ratio = 2;
            }

            texture ??= Textures?.Get(componentName, wrapModeS, wrapModeT);

            if (texture != null)
                texture.ScaleAdjust = ratio;

            return texture;
        }

        public override ISample? GetSample(ISampleInfo sampleInfo)
        {
            IEnumerable<string> lookupNames;

            if (sampleInfo is HitSampleInfo hitSample)
                lookupNames = getLegacyLookupNames(hitSample);
            else
            {
                lookupNames = sampleInfo.LookupNames.SelectMany(getFallbackSampleNames);
            }

            foreach (string lookup in lookupNames)
            {
                var sample = Samples?.Get(lookup);

                if (sample != null)
                {
                    return sample;
                }
            }

            return null;
        }

        private IEnumerable<string> getLegacyLookupNames(HitSampleInfo hitSample)
        {
            var lookupNames = hitSample.LookupNames.SelectMany(getFallbackSampleNames);

            if (!UseCustomSampleBanks && !string.IsNullOrEmpty(hitSample.Suffix))
            {
                // for compatibility with stable, exclude the lookup names with the custom sample bank suffix, if they are not valid for use in this skin.
                // using .EndsWith() is intentional as it ensures parity in all edge cases
                // (see LegacyTaikoSampleInfo for an example of one - prioritising the taiko prefix should still apply, but the sample bank should not).
                lookupNames = lookupNames.Where(name => !name.EndsWith(hitSample.Suffix, StringComparison.Ordinal));
            }

            foreach (string l in lookupNames)
                yield return l;

            // also for compatibility, try falling back to non-bank samples (so-called "universal" samples) as the last resort.
            // going forward specifying banks shall always be required, even for elements that wouldn't require it on stable,
            // which is why this is done locally here.
            yield return hitSample.Name;
        }

        private IEnumerable<string> getFallbackSampleNames(string name)
        {
            // May be something like "Gameplay/normal-hitnormal" from lazer.
            yield return name;

            // Fall back to using the last piece for components coming from lazer (e.g. "Gameplay/normal-hitnormal" -> "normal-hitnormal").
            yield return name.Split('/').Last();
        }
    }
}
