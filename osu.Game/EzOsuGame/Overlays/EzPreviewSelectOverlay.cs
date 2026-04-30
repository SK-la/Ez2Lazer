// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 一个用于预览和选择纹理的覆盖层。它会扫描指定路径下的所有PNG文件，并将它们显示为可点击的预览图像。
    /// </summary>
    // TODO: 目前没有具体实现，过去尝试过遇到多次崩溃，需要重构和测试。
    public partial class EzPreviewSelectOverlay : OverlayContainer
    {
        private FillFlowContainer previewList = null!;
        private readonly IPreviewable source;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        public EzPreviewSelectOverlay(IPreviewable source)
        {
            this.source = source;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.8f
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(20),
                    Child = new BasicScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = previewList = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 10)
                        }
                    }
                }
            };

            loadPreviews();
        }

        private void loadPreviews()
        {
            var textureFiles = textures.GetAvailableResources()
                                       .Where(path => path.StartsWith(source.TextureBasePath, StringComparison.Ordinal))
                                       .Where(path => path.EndsWith(".png", StringComparison.Ordinal));

            foreach (string texturePath in textureFiles)
            {
                // 从完整路径中提取纹理名称
                string textureName = texturePath.Replace(source.TextureBasePath + "/", "").Replace(".png", "");
                previewList.Add(new PreviewContainer(textureName, texturePath, source));
            }
        }

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);
        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        private partial class PreviewContainer : Container
        {
            private readonly string textureName;
            private readonly string texturePath;
            private readonly IPreviewable source;

            public PreviewContainer(string textureName, string texturePath, IPreviewable source)
            {
                this.textureName = textureName;
                this.texturePath = texturePath;
                this.source = source;

                RelativeSizeAxes = Axes.X;
                Height = 120;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Masking = true;
                CornerRadius = 5;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.1f)
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = textureName,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold)
                            },
                            new Sprite
                            {
                                Texture = textures.Get(texturePath),
                                Size = new Vector2(80),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre
                            }
                        }
                    }
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                source.TextureNameBindable.Value = textureName;
                return true;
            }
        }
    }
}
