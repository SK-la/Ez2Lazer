using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using EzLocalTextureFactory = osu.Game.LAsEzExtensions.EzLocalTextureFactory;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public abstract partial class EzHealthDisplayBase : HealthDisplay
    {
        protected EzLocalTextureFactory TextureFactory { get; private set; } = null!;

        protected string TexturePrefix { get; set; } = "health/";

        protected Container Content { get; private set; } = null!;

        protected abstract string[] TextureSuffixes { get; }

        [BackgroundDependencyLoader]
        private void load(EzLocalTextureFactory textureFactory)
        {
            TextureFactory = textureFactory;

            AutoSizeAxes = Axes.Y;

            InternalChild = Content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            LoadTextures();
        }

        protected virtual void LoadTextures()
        {
            foreach (string suffix in TextureSuffixes)
            {
                var texture = TextureFactory.CreateAnimation(TexturePrefix + suffix);

                Content.Add(texture);
            }
        }

        protected override void Update()
        {
            base.Update();
            // Implement health update logic here
        }
    }
}
