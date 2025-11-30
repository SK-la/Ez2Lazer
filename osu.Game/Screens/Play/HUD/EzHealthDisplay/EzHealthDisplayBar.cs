
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using osuTK.Graphics;
using EzLocalTextureFactory = osu.Game.LAsEzExtensions.EzLocalTextureFactory;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public partial class EzHealthDisplayBar : Container
    {
        private Vector2 progressRange = new Vector2(0f, 1f);

        public Vector2 ProgressRange
        {
            get => progressRange;
            set
            {
                if (progressRange == value)
                    return;

                progressRange = value;
                updateMasking();
            }
        }

        private float pathRadius = 10f;

        public float PathRadius
        {
            get => pathRadius;
            set
            {
                if (pathRadius == value)
                    return;

                pathRadius = value;
                updateMasking();
            }
        }

        public EzHealthDisplayBar(EzLocalTextureFactory textureFactory, string textureName)
        {
            var textureAnimation1 = textureFactory.CreateAnimation(textureName);

            Add(textureAnimation1);

            Masking = true;
        }

        private void updateMasking()
        {
            // Implement masking based on progressRange and pathRadius
            // For simplicity, use a simple rectangle mask for now
            // In a real implementation, you might need custom shaders or more complex masking
            float progress = progressRange.Y - progressRange.X;
            Width = progress * DrawWidth;
        }

        public void Flash()
        {
            // Implement flash effect, e.g., change colour or alpha temporarily
            this.FadeTo(0.5f, 30).Then().FadeTo(1f, 300);
        }

        public void SetDamageColour()
        {
            // Set to red for damage
            Colour = Color4.Red;
        }

        public void ResetColour()
        {
            // Reset to default
            Colour = Color4.White;
        }
    }
}
