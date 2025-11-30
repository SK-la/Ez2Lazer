
using osu.Framework.Graphics.Containers;
using EzLocalTextureFactory = osu.Game.LAsEzExtensions.EzLocalTextureFactory;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public partial class EzHealthDisplayBackground : Container
    {
        public EzHealthDisplayBackground(EzLocalTextureFactory textureFactory, string textureName)
        {
            var textureAnimation = textureFactory.CreateAnimation(textureName);

            Add(textureAnimation);
        }
    }
}
