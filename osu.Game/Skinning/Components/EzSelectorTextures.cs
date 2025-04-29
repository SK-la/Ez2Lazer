// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace osu.Game.Skinning.Components
{
    public partial class EzSelectorTextures : CompositeDrawable
    {
        [Resolved]
        private TextureStore textures { get; set; } = null!;

        private readonly FillFlowContainer textureContainer;
        public Bindable<string> SelectedTexture { get; } = new Bindable<string>();

        public EzSelectorTextures()
        {
            AutoSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                textureContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var resources = textures.GetAvailableResources();
            var matchingResources = resources
                .Where(r => r.StartsWith("Gameplay/EarlyOrLate", StringComparison.OrdinalIgnoreCase)
                            && Path.GetExtension(r).Equals(".png", StringComparison.OrdinalIgnoreCase));

            foreach (string? resource in matchingResources)
            {
                var texture = textures.Get(resource);

                if (texture != null)
                {
                    var sprite = new Sprite
                    {
                        Texture = texture,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Scale = new Vector2(0.5f), // 缩放纹理以适应界面
                    };

                    var clickable = new ClickableContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Child = sprite,
                        Action = () => SelectedTexture.Value = resource // 点击时更新绑定值
                    };

                    textureContainer.Add(clickable);
                }
            }
        }
    }
}
