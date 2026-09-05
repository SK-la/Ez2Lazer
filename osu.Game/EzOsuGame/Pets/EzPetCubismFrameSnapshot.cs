// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Live2DCSharpSDK.Framework.Rendering;
using osuTK;

namespace osu.Game.EzOsuGame.Pets
{
    public sealed class EzPetCubismFrameSnapshot
    {
        public float CanvasWidth { get; init; }
        public float CanvasHeight { get; init; }
        public EzPetCubismMeshPart[] Parts { get; init; } = [];
    }

    public sealed class EzPetCubismMeshPart
    {
        public int TextureIndex { get; init; }
        public float Opacity { get; init; }
        public CubismBlendMode BlendMode { get; init; }
        public Vector2[] Positions { get; init; } = [];
        public Vector2[] UVs { get; init; } = [];
        public ushort[] Indices { get; init; } = [];
    }
}
