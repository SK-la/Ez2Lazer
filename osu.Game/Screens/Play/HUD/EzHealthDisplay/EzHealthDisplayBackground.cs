
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions;

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
