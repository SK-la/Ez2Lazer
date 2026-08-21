// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Placeholder host for an authorised Live2D pet until Cubism Native is wired.
    /// Keeps the stage / state-machine path usable; does not load arbitrary user moc3.
    /// </summary>
    public partial class EzPetLive2DHost : CompositeDrawable
    {
        private readonly OsuSpriteText statusText;

        public string? ModelEntryPath { get; private set; }

        public EzPetLive2DHost()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("2a2030"),
                    Alpha = 0.85f,
                },
                statusText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Colour4.FromHex("e8d5ff"),
                    Text = "Live2D (Cubism pending)",
                },
            };
        }

        public void BindPack(EzPetPack pack, string? modelEntryPath)
        {
            ModelEntryPath = modelEntryPath;
            statusText.Text = string.IsNullOrEmpty(modelEntryPath)
                ? $"Live2D authorised: {pack.Name}\n(Cubism runtime not linked)"
                : $"Live2D authorised: {pack.Name}\n{modelEntryPath}\n(Cubism runtime not linked)";
        }

        /// <summary>
        /// Future hook: map pet state / clip to Cubism motion or parameter.
        /// </summary>
        public void NotifyState(string state, string clip)
        {
            // Cubism motion sync will land here.
            _ = state;
            _ = clip;
        }
    }
}
