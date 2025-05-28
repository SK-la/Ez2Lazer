// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzTestNote : CompositeDrawable
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public EzTestNote()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var storage = host.Storage.GetStorageForDirectory(path);
            var fileStore = new StorageBackedResourceStore(storage);
            var textureLoaderStore = new TextureLoaderStore(fileStore);

            var textureStore = new TextureStore(renderer);
            textureStore.AddTextureSource(textureLoaderStore);
            Texture texture = textureStore.Get("000.png");

            if (texture != null)
            {
                var sprite = new Sprite
                {
                    Texture = texture,
                    RelativeSizeAxes = Axes.X, // 只适配宽度
                    FillMode = FillMode.Fit
                };
                AddInternal(sprite);
            }
        }

        private string path => "F:\\MUG OSU\\EZ2OSU-lazer\\EzResources\\note\\air\\bluenote";

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     // Height = DrawWidth;
        // }

        // protected override void LoadComplete()
        // {
        //     base.LoadComplete();
        // }
    }
}
