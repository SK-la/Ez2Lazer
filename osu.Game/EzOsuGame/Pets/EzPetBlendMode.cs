// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Frame blending for pet packs. <see cref="BlackKey"/> removes pure-black backgrounds without alpha mattes.
    /// </summary>
    public enum EzPetBlendMode
    {
        Normal,
        Additive,
        BlackKey,
    }

    public static class EzPetBlendModeExtensions
    {
        /// <summary>
        /// SrcColor additive blend: black pixels contribute nothing; non-black pixels add over the scene.
        /// </summary>
        public static readonly BlendingParameters BLACK_KEY_BLENDING = new BlendingParameters
        {
            Source = BlendingType.SrcColor,
            Destination = BlendingType.One,
            SourceAlpha = BlendingType.One,
            DestinationAlpha = BlendingType.One,
            RGBEquation = BlendingEquation.Add,
            AlphaEquation = BlendingEquation.Add,
        };

        public static EzPetBlendMode Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return EzPetBlendMode.Normal;

            return value.Trim().ToLowerInvariant() switch
            {
                "normal" or "mixture" or "default" => EzPetBlendMode.Normal,
                "additive" or "add" => EzPetBlendMode.Additive,
                "blackkey" or "black_key" or "black" => EzPetBlendMode.BlackKey,
                _ => EzPetBlendMode.Normal,
            };
        }

        public static EzPetBlendMode Resolve(string? packMode, string? clipMode)
        {
            if (!string.IsNullOrWhiteSpace(clipMode))
                return Parse(clipMode);

            return Parse(packMode);
        }

        public static BlendingParameters ToBlendingParameters(this EzPetBlendMode mode) => mode switch
        {
            EzPetBlendMode.Additive => BlendingParameters.Additive,
            EzPetBlendMode.BlackKey => BLACK_KEY_BLENDING,
            _ => BlendingParameters.Mixture,
        };
    }
}
