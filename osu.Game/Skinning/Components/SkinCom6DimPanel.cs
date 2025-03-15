// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Skinning.Components
{
    public partial class SkinCom6DimPanel : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBindable<StarDifficulty?>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;

        private readonly Bindable<float>[] parameters = new Bindable<float>[6];

        private readonly Hexagon hexagon;
        private readonly Hexagon parameterHexagon;

        public SkinCom6DimPanel()
        {
            // for (int i = 0; i < parameters.Length; i++)
            // {
            //     parameters[i] = new BindableFloat();
            //     parameters[i].ValueChanged += _ => updatePanel();
            // }
            Size = new Vector2(200);
            // AutoSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                hexagon = new Hexagon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.LightYellow,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(0.5f),
                },
                parameterHexagon = new Hexagon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.Gold,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(0.5f),
                    Alpha = 0.5f,
                }
            };
        }

        protected override void Update()
        {
            base.Update();
            hexagon.UpdateVertices(parameters, true);
            parameterHexagon.UpdateVertices(parameters, false);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = new BindableFloat();
                parameters[i].ValueChanged += _ => updatePanel();
            }

            beatmap.BindValueChanged(b =>
            {
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource = new CancellationTokenSource();

                difficultyBindable?.UnbindAll();
                difficultyBindable = difficultyCache.GetBindableDifficulty(b.NewValue.BeatmapInfo, difficultyCancellationSource.Token);
                difficultyBindable.BindValueChanged(d =>
                {
                    parameters[0].Value = (float)beatmap.Value.BeatmapInfo.BPM / 10;
                    parameters[1].Value = (float)beatmap.Value.BeatmapInfo.StarRating;
                    parameters[2].Value = beatmap.Value.BeatmapInfo.Difficulty.CircleSize;
                    parameters[3].Value = beatmap.Value.BeatmapInfo.Difficulty.OverallDifficulty;
                    parameters[4].Value = beatmap.Value.BeatmapInfo.Difficulty.DrainRate;
                    parameters[5].Value = beatmap.Value.BeatmapInfo.Difficulty.ApproachRate;
                    updatePanel();
                });
            }, true);

            mods.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => updatePanel()
                };

                updatePanel();
            }, true);

            ruleset.BindValueChanged(_ => updatePanel());

            updatePanel();
        }

        private void updatePanel()
        {
            hexagon.UpdateVertices(parameters, true); // 基础六边形
            parameterHexagon.UpdateVertices(parameters, false); // 参数六边形
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            difficultyCancellationSource?.Cancel();
            difficultyCancellationSource?.Dispose();
            difficultyCancellationSource = null;

            modSettingTracker?.Dispose();
        }

        public partial class Hexagon : Drawable
        {
            private readonly Vector2[] vertices = new Vector2[6];
            private Texture? whitePixel;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                whitePixel = createWhitePixelTexture(renderer);
            }

            private Texture createWhitePixelTexture(IRenderer renderer)
            {
                var texture = renderer.CreateTexture(1, 1, true);
                var image = new Image<Rgba32>(1, 1);
                image[0, 0] = new Rgba32(255, 255, 255, 255);

                var upload = new TextureUpload(image);
                texture.SetData(upload);
                return texture;
            }

            public void UpdateVertices(Bindable<float>[] parameters, bool isBaseHexagon)
            {
                const float max_value = 10;
                float radius = Math.Min(DrawSize.X, DrawSize.Y) / 2;

                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.DegreesToRadians(60 * i);
                    float value = isBaseHexagon ? max_value : parameters[i].Value / max_value;
                    vertices[i] = new Vector2(
                        radius * value * (float)Math.Cos(angle),
                        radius * value * (float)Math.Sin(angle)
                    );
                }

                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new HexagonDrawNode(this);

            private class HexagonDrawNode : DrawNode
            {
                private readonly Hexagon source;
                private readonly Vector2[] vertices = new Vector2[6];
                private Color4 color;
                private Texture? texture;

                public HexagonDrawNode(Hexagon source)
                    : base(source)
                {
                    this.source = source;
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    Array.Copy(source.vertices, vertices, 6);
                    color = source.Colour;
                    texture = source.whitePixel;

                    if (texture == null)
                    {
                        throw new InvalidOperationException("Texture is null in ApplyState.");
                    }
                }

                protected override void Draw(IRenderer renderer)
                {
                    drawFilledHexagon(renderer);
                }

                private void drawFilledHexagon(IRenderer renderer)
                {
                    for (int i = 1; i < vertices.Length - 1; i++)
                    {
                        if (texture != null)
                        {
                            renderer.DrawTriangle(
                                texture,
                                new Triangle(vertices[0], vertices[i], vertices[i + 1]),
                                color
                            );
                        }
                    }
                }
            }
        }
    }
}
//
// public partial class Hexagon : Drawable
// {
//     private Texture? whitePixel;
//
//     [BackgroundDependencyLoader]
//     private void load(TextureStore textures, IRenderer renderer)
//     {
//         whitePixel = textures.Get("white-pixel");
//
//         if (whitePixel == null)
//         {
//             Console.WriteLine("Failed to load white-pixel texture. Creating default white texture.");
//             whitePixel = createWhitePixelTexture(renderer);
//         }
//         else
//         {
//             Console.WriteLine("Successfully loaded white-pixel texture.");
//         }
//     }
//
//     private Texture createWhitePixelTexture(IRenderer renderer)
//     {
//         var texture = renderer.CreateTexture(1, 1, true);
//         var image = new Image<Rgba32>(1, 1);
//         image[0, 0] = new Rgba32(255, 255, 255, 255);
//
//         var upload = new TextureUpload(image);
//         texture.SetData(upload);
//         return texture;
//     }
// }
