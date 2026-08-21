// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Live2D host: shows Cubism Core status and a breath-driven pulse until mesh DrawNode lands.
    /// </summary>
    public partial class EzPetLive2DHost : CompositeDrawable
    {
        private readonly Box pulse;
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
                    Colour = Colour4.FromHex("1a1520"),
                    Alpha = 0.9f,
                },
                pulse = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(120),
                    Colour = Colour4.FromHex("c9a0ff"),
                    Alpha = 0.55f,
                },
                statusText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = OsuFont.GetFont(size: 13),
                    Colour = Colour4.FromHex("e8d5ff"),
                    Text = "Live2D",
                    Margin = new MarginPadding { Bottom = 8 },
                    AllowMultiline = true,
                },
            };
        }

        public void BindPack(EzPetPack pack, string? modelEntryPath, EzPetCubismSession? session, string? cubismError)
        {
            ModelEntryPath = modelEntryPath;

            if (session?.IsReady == true)
            {
                statusText.Text = $"{pack.Name}\n{session.Status}\nstate sync + breath pulse (mesh draw TBD)";
                return;
            }

            string hint = cubismError ?? "Cubism Core not ready";
            statusText.Text = string.IsNullOrEmpty(modelEntryPath)
                ? $"Live2D authorised: {pack.Name}\n{hint}"
                : $"Live2D authorised: {pack.Name}\n{modelEntryPath}\n{hint}";
        }

        public void NotifyState(string state, string clip, EzPetCubismSession? session)
        {
            session?.NotifyState(state, clip);

            if (session?.IsReady == true)
                statusText.Text = $"{session.Status}\n{state} / {clip}\nbreath={session.BreathValue:0.00}";
        }

        public void ApplyBreath(float breath01)
        {
            float t = Math.Clamp(breath01, 0f, 1f);
            float scale = 0.85f + 0.35f * t;
            pulse.Scale = new Vector2(scale);
            pulse.Alpha = 0.35f + 0.45f * t;
        }
    }
}
