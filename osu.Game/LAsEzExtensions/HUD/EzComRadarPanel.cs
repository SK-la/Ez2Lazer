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
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Triangle = osu.Framework.Graphics.Primitives.Triangle;

namespace osu.Game.LAsEzExtensions.HUD
{
    public partial class EzComRadarPanel : CompositeDrawable, ISerialisableDrawable
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

        private IBindable<StarDifficulty>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;

        private readonly Bindable<float>[] parameters = new Bindable<float>[6];

        private Hexagon hexagon = new Hexagon();
        private Hexagon parameterHexagon = new Hexagon();

        // 定义各参数的最大值，用于归一化处理
        private const float max_bpm = 300f;
        private const float max_star = 12f;
        private const float max_cs = 10f;
        private const float max_od = 10f;
        private const float max_dr = 10f;
        private const float max_ar = 10f;

        public EzComRadarPanel()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            InternalChildren = new Drawable[]
            {
                hexagon = new Hexagon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.LightYellow,
                    Size = new Vector2(200),
                },
                parameterHexagon = new Hexagon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.Gold,
                    Size = new Vector2(200),
                    Alpha = 0.5f,
                }
            };

            // 调试用边框
            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.Both,
                BorderColour = Color4.White,
                BorderThickness = 2,
                Masking = true,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hexagon.Invalidate(Invalidation.DrawNode);
            parameterHexagon.Invalidate(Invalidation.DrawNode);

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
                    // 归一化参数到0-1 范围
                    parameters[0].Value = (float)(beatmap.Value.BeatmapInfo.BPM / max_bpm);
                    parameters[1].Value = (float)beatmap.Value.BeatmapInfo.StarRating / max_star;
                    parameters[2].Value = beatmap.Value.BeatmapInfo.Difficulty.CircleSize / max_cs;
                    parameters[3].Value = beatmap.Value.BeatmapInfo.Difficulty.OverallDifficulty / max_od;
                    parameters[4].Value = beatmap.Value.BeatmapInfo.Difficulty.DrainRate / max_dr;
                    parameters[5].Value = beatmap.Value.BeatmapInfo.Difficulty.ApproachRate / max_ar;
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
            hexagon.UpdateVertices(true); // 基础六边形（固定最大范围）
            parameterHexagon.UpdateVertices(false, parameters); // 参数六边形
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            difficultyCancellationSource?.Cancel();
            modSettingTracker?.Dispose();
        }
    }

    public partial class Hexagon : Container
    {
        private Vector2[] vertices = new Vector2[6];
        private Texture? whitePixel;
        private bool isBase;
        private Bindable<float>[]? parameters;
        private readonly string[] parameterNames = { "BPM", "Star", "CS", "OD", "DR", "AR" };

        public Hexagon()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            whitePixel = createWhitePixelTexture(renderer);
        }

        private static Texture createWhitePixelTexture(IRenderer renderer)
        {
            var texture = renderer.CreateTexture(1, 1, true);
            var image = new Image<Rgba32>(1, 1);
            image[0, 0] = new Rgba32(255, 255, 255, 255);
            texture.SetData(new TextureUpload(image));
            return texture;
        }

        public void UpdateVertices(bool isBase, Bindable<float>[]? parameters = null)
        {
            this.isBase = isBase;
            this.parameters = parameters;
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new HexagonDrawNode(this);

        private class HexagonDrawNode : DrawNode
        {
            private readonly Hexagon source;
            private readonly Vector2[] vertices = new Vector2[6];
            private Color4 color;
            private Texture? texture;

            public HexagonDrawNode(Hexagon hexagon)
                : base(hexagon)
            {
                source = hexagon;
            }

            public override void ApplyState()
            {
                base.ApplyState();
                color = source.Colour;
                texture = source.whitePixel;

                // 获取基于相对尺寸的实际绘制区域
                var drawSize = source.DrawSize;

                float radius = Math.Min(drawSize.X, drawSize.Y) / 2 * 0.8f;
                Vector2 center = drawSize * 6;

                if (source.isBase)
                {
                    // 基于本地坐标系（原点在中心）
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = MathHelper.DegreesToRadians(60 * i - 90);
                        vertices[i] = new Vector2(
                            radius * (float)Math.Cos(angle) + center.X,
                            radius * (float)Math.Sin(angle) + center.Y
                        );
                    }
                }
                else if (source.parameters != null)
                {
                    // 参数六边形（本地坐标系）
                    for (int i = 0; i < 6; i++)
                    {
                        float value = Math.Clamp(source.parameters[i].Value, 0, 1);
                        float angle = MathHelper.DegreesToRadians(60 * i - 90);
                        vertices[i] = new Vector2(
                            radius * value * (float)Math.Cos(angle) + center.X,
                            radius * value * (float)Math.Sin(angle) + center.Y
                        );
                    }
                }
            }

            protected override void Draw(IRenderer renderer)
            {
                if (texture == null) return;

                // 框架会自动应用以下变换矩阵：
                // DrawInfo.Matrix = 位置矩阵 × 缩放矩阵 × 旋转矩阵 × 锚点偏移

                // 绘制时直接使用本地坐标系顶点即可
                // 填充六边形
                for (int i = 1; i < vertices.Length - 1; i++)
                {
                    renderer.DrawTriangle(
                        texture,
                        new Triangle(
                            vertices[0],
                            vertices[i],
                            vertices[(i + 1) % vertices.Length]
                        ),
                        color
                    );
                }

                // 边线绘制
                foreach (var quad in generateLineQuads())
                {
                    renderer.DrawQuad(
                        texture,
                        quad,
                        color
                    );
                }
            }

            private IEnumerable<Quad> generateLineQuads()
            {
                const float line_thickness = 2f;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 current = vertices[i];
                    Vector2 next = vertices[(i + 1) % vertices.Length];

                    Vector2 dir = next - current;
                    float length = dir.Length;
                    if (length < float.Epsilon) continue;

                    dir.Normalize();

                    Vector2 perpendicular = new Vector2(-dir.Y, dir.X) * line_thickness / 2;

                    yield return new Quad(
                        current - perpendicular,
                        current + perpendicular,
                        next - perpendicular,
                        next + perpendicular
                    );
                }
            }
        }
    }
}
