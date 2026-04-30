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
using osu.Game.EzOsuGame.HUD;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    public class Ez2Skin : Skin
    {
        public static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = Skinning.SkinInfo.EZ2_SKIN,
            Name = "[Ez] \"Ez2Circle\" (2025)",
            Creator = "SK_la",
            Protected = true,
            InstantiationInfo = typeof(Ez2Skin).GetInvariantInstantiationInfo()
        };

        protected readonly IStorageResourceProvider Resources;

        public Ez2Skin(IStorageResourceProvider resources)
            : this(CreateInfo(), resources)
        {
        }

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public Ez2Skin(SkinInfo skin, IStorageResourceProvider resources)
            : base(
                skin,
                resources
            )
        {
            Resources = resources;
        }

        public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => Textures?.Get(componentName, wrapModeS, wrapModeT);

        public override ISample GetSample(ISampleInfo sampleInfo)
        {
            string lookup = sampleInfo.LookupNames.FirstOrDefault() ?? "virtual";
            return new SampleVirtual(lookup);
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
                                var dim = c.OfType<EzHUDRadarPanel>().FirstOrDefault();

                                if (dim != null)
                                {
                                    dim.Anchor = Anchor.BottomCentre;
                                    dim.Origin = Anchor.Centre;
                                    dim.Position = new Vector2(-80, -150);
                                }
                            });

                            return songSelectComponents;

                        case GlobalSkinnableContainers.MainHUDComponents:

                            var mainHUDComponents = new DefaultSkinComponentsContainer(container =>
                            {
                                var health = container.OfType<HealthDisplay>().FirstOrDefault();
                                var score = container.OfType<EzHUDScoreCounter>().FirstOrDefault();
                                var acc = container.OfType<ArgonAccuracyCounter>().FirstOrDefault();
                                var pps = container.OfType<ArgonPerformancePointsCounter>().FirstOrDefault();
                                var songProgress = container.OfType<ArgonSongProgress>().FirstOrDefault();

                                const float x_offset = 20;
                                const float padding = 10;

                                if (health != null)
                                {
                                    health.Anchor = Anchor.BottomLeft;
                                    health.Origin = Anchor.CentreLeft;
                                    // health.BypassAutoSizeAxes = Axes.Y;
                                    health.Width = 0.5f;
                                    // health.BarHeight.Value = 0f;
                                    // health.Height = 0.4f;
                                    health.Rotation = -90;
                                    health.Position = new Vector2(0, 0);

                                    if (acc != null)
                                    {
                                        acc.Position = new Vector2(-x_offset, x_offset);
                                        acc.Anchor = Anchor.TopRight;
                                        acc.Origin = Anchor.TopRight;
                                    }

                                    if (score != null)
                                    {
                                        score.Anchor = Anchor.TopLeft;
                                        score.Origin = Anchor.TopLeft;
                                        score.Position = new Vector2(x_offset, x_offset);
                                        score.ShowLabel.Value = false;
                                        score.ThemeName.Value = EzEnumGameThemeName.Celeste_Lumiere;
                                    }

                                    var attributeTexts = container.OfType<BeatmapAttributeText>().ToArray();

                                    if (attributeTexts.Length >= 4)
                                    {
                                        var title = attributeTexts[0];
                                        var artist = attributeTexts[1];
                                        var diff = attributeTexts[2];
                                        var sr = attributeTexts[3];

                                        title.Anchor = Anchor.TopLeft;
                                        title.Origin = Anchor.TopLeft;
                                        title.Scale = new Vector2(0.65f);
                                        title.Position = new Vector2(x_offset, 50 + 10);
                                        title.Attribute.Value = BeatmapAttribute.Title;

                                        artist.Anchor = Anchor.TopLeft;
                                        artist.Origin = Anchor.TopLeft;
                                        artist.Scale = new Vector2(0.65f);
                                        artist.Position = new Vector2(x_offset, title.Y + title.DrawHeight * title.Scale.Y + 10);
                                        artist.Attribute.Value = BeatmapAttribute.Artist;

                                        diff.Anchor = Anchor.TopLeft;
                                        diff.Origin = Anchor.TopLeft;
                                        diff.Position = new Vector2(x_offset, artist.Y + artist.DrawHeight * artist.Scale.Y + 10);
                                        diff.Scale = new Vector2(0.65f);
                                        diff.Attribute.Value = BeatmapAttribute.DifficultyName;
                                        diff.Template.Value = "{Value}";

                                        sr.Anchor = Anchor.TopLeft;
                                        sr.Origin = Anchor.TopLeft;
                                        sr.Position = new Vector2(x_offset, diff.Y + diff.DrawHeight * diff.Scale.Y + 10);
                                        sr.Scale = new Vector2(0.65f);
                                        sr.Attribute.Value = BeatmapAttribute.StarRating;

                                        if (pps != null)
                                        {
                                            pps.Position = new Vector2(x_offset, sr.Y + sr.DrawHeight * sr.Scale.Y + 10);
                                            pps.Anchor = Anchor.TopLeft;
                                            pps.Origin = Anchor.TopLeft;
                                            pps.Scale = new Vector2(0.8f);
                                        }
                                    }

                                    if (songProgress != null)
                                    {
                                        songProgress.Position = new Vector2(0, -padding);
                                        songProgress.Scale = new Vector2(0.9f, 1);
                                    }
                                }
                            })
                            {
                                Children = new Drawable[]
                                {
                                    new EzHUDScoreCounter(),
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),
                                    new BeatmapAttributeText(),

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

        private static Color4 getComboColour(IHasComboColours source, int colourIndex) => source.ComboColours![colourIndex % source.ComboColours.Count];
    }
}
