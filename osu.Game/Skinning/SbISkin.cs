// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Beatmaps.Formats;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    public class SbISkin : Skin
    {
        public static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = Skinning.SkinInfo.SBI_SKIN,
            Name = "LA's \"StrongBox \" for arisu(2025)",
            Creator = "SK_la",
            Protected = true,
            InstantiationInfo = typeof(SbISkin).GetInvariantInstantiationInfo()
        };

        protected readonly IStorageResourceProvider Resources;

        public SbISkin(IStorageResourceProvider resources)
            : this(CreateInfo(), resources)
        {
        }

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public SbISkin(SkinInfo skin, IStorageResourceProvider resources)
            : base(
                skin,
                resources
            )
        {
            Resources = resources;
        }

        public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => Textures?.Get(componentName, wrapModeS, wrapModeT);

        public override ISample? GetSample(ISampleInfo sampleInfo)
        {
            var sample = Samples?.Get("Gameplay/Ez/nil.wav");
            return sample;
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            // Temporary until default skin has a valid hit lighting.
            if ((lookup as SkinnableSprite.SpriteComponentLookup)?.LookupName == @"lighting") return Drawable.Empty();

            switch (lookup)
            {
                case GlobalSkinnableContainerLookup containerLookup:
                    switch (containerLookup.Lookup)
                    {
                        case GlobalSkinnableContainers.SongSelect:
                            var songSelectComponents = new DefaultSkinComponentsContainer(c =>
                            {
                                // var dim = c.OfType<LAsSkinCom6DimPanel>().FirstOrDefault();
                                //
                                // if (dim != null)
                                // {
                                //     dim.Anchor = Anchor.Centre;
                                //     dim.Origin = Anchor.Centre;
                                // }
                            })
                            {
                                // Children = new Drawable[]
                                // {
                                //     new LAsSkinCom6DimPanel(),
                                // }
                            };

                            return songSelectComponents;

                        case GlobalSkinnableContainers.MainHUDComponents:

                            var mainHUDComponents = new DefaultSkinComponentsContainer(container =>
                            {
                                var health = container.OfType<HealthDisplay>().FirstOrDefault();
                                var score = container.OfType<ArgonScoreCounter>().FirstOrDefault();
                                var accuracy = container.OfType<ArgonAccuracyCounter>().FirstOrDefault();
                                var performancePoints = container.OfType<ArgonPerformancePointsCounter>().FirstOrDefault();
                                var songProgress = container.OfType<ArgonSongProgress>().FirstOrDefault();

                                const float x_offset = 20;

                                if (health != null)
                                {
                                    health.Anchor = Anchor.BottomLeft;
                                    health.Origin = Anchor.CentreLeft;
                                    // health.RelativeSizeAxes = Axes.Y;
                                    health.Width = 0.5f;
                                    // health.BarHeight.Value = 0f;
                                    // health.Height = 0.4f;
                                    health.Rotation = -90;
                                    health.Position = new Vector2(0, 0);

                                    if (score != null)
                                    {
                                        score.Origin = Anchor.TopLeft;
                                        score.Position = new Vector2(x_offset, 20);
                                    }

                                    if (accuracy != null)
                                    {
                                        accuracy.Position = new Vector2(-x_offset, 20);
                                        accuracy.Anchor = Anchor.TopRight;
                                        accuracy.Origin = Anchor.TopRight;
                                    }

                                    if (performancePoints != null && accuracy != null)
                                    {
                                        performancePoints.Position = new Vector2(accuracy.X, accuracy.Y + accuracy.DrawHeight + 10);
                                        performancePoints.Anchor = Anchor.TopRight;
                                        performancePoints.Origin = Anchor.TopRight;
                                        performancePoints.Scale = new Vector2(0.8f);
                                    }

                                    if (songProgress != null)
                                    {
                                        const float padding = 10;
                                        songProgress.Position = new Vector2(0, -padding);
                                        songProgress.Scale = new Vector2(0.9f, 1);
                                    }

                                    var attributeTexts = container.OfType<BeatmapAttributeText>().ToArray();

                                    if (attributeTexts.Length >= 4)
                                    {
                                        var attributeText = attributeTexts[0];
                                        var attributeText2 = attributeTexts[1];
                                        var attributeText3 = attributeTexts[2];
                                        var attributeText4 = attributeTexts[3];

                                        if (performancePoints != null)
                                        {
                                            attributeText.Anchor = Anchor.TopRight;
                                            attributeText.Origin = Anchor.TopRight;
                                            attributeText.Position = new Vector2(-x_offset, performancePoints.Y + performancePoints.DrawHeight * 0.8f + 10);
                                            attributeText.Scale = new Vector2(0.65f);
                                            attributeText.Attribute.Value = BeatmapAttribute.StarRating;
                                        }

                                        attributeText2.Anchor = Anchor.TopRight;
                                        attributeText2.Origin = Anchor.TopRight;
                                        attributeText2.Position = new Vector2(-x_offset, attributeText.Y + attributeText.DrawHeight * 0.65f + 10);
                                        attributeText2.Scale = new Vector2(0.65f);
                                        attributeText2.Attribute.Value = BeatmapAttribute.DifficultyName;
                                        attributeText2.Template.Value = "{Value}";

                                        if (score != null)
                                        {
                                            attributeText3.Anchor = Anchor.TopLeft;
                                            attributeText3.Origin = Anchor.TopLeft;
                                            attributeText3.Scale = new Vector2(0.65f);
                                            attributeText3.Position = new Vector2(x_offset, score.Y + score.DrawHeight + 10);
                                            attributeText3.Attribute.Value = BeatmapAttribute.Artist;
                                        }

                                        attributeText4.Anchor = Anchor.TopLeft;
                                        attributeText4.Origin = Anchor.TopLeft;
                                        attributeText4.Scale = new Vector2(0.65f);
                                        attributeText4.Position = new Vector2(x_offset, attributeText3.Y + attributeText3.DrawHeight * 0.65f + 10);
                                        attributeText4.Attribute.Value = BeatmapAttribute.Title;
                                    }
                                }
                            })
                            {
                                Children = new Drawable[]
                                {
                                    new ArgonScoreCounter
                                    {
                                        ShowLabel = { Value = false },
                                        WireframeOpacity = { Value = 0 },
                                    },
                                    new DefaultHealthDisplay(),
                                    new ArgonAccuracyCounter
                                    {
                                        WireframeOpacity = { Value = 0 },
                                    },
                                    new ArgonPerformancePointsCounter
                                    {
                                        WireframeOpacity = { Value = 0 },
                                    },
                                    new ArgonSongProgress(),
                                    new JudgementCounterDisplay
                                    {
                                        FillMode = FillMode.Fill,
                                        FlowDirection = { Value = Direction.Vertical },
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Position = new Vector2(20, 0),
                                    },
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),
                                }
                            };

                            return mainHUDComponents;
                    }

                    return null;
            }

            return base.GetDrawableComponent(lookup);
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            // todo: this code is pulled from LegacySkin and should not exist.
            // will likely change based on how databased storage of skin configuration goes.
            switch (lookup)
            {
                case GlobalSkinColours global:
                    switch (global)
                    {
                        case GlobalSkinColours.ComboColours:
                        {
                            LogLookupDebug(this, lookup, LookupDebugType.Hit);
                            return SkinUtils.As<TValue>(new Bindable<IReadOnlyList<Color4>?>(Configuration.ComboColours));
                        }
                    }

                    break;

                case SkinComboColourLookup comboColour:
                    LogLookupDebug(this, lookup, LookupDebugType.Hit);
                    return SkinUtils.As<TValue>(new Bindable<Color4>(getComboColour(Configuration, comboColour.ColourIndex)));
            }

            LogLookupDebug(this, lookup, LookupDebugType.Miss);
            return null;
        }

        private static Color4 getComboColour(IHasComboColours source, int colourIndex)
            => source.ComboColours![colourIndex % source.ComboColours.Count];
    }
}
