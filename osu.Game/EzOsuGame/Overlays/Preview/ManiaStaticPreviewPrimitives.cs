// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public readonly record struct ManiaPreviewNote(double StartTime, double EndTime, int Column);

    public readonly record struct ManiaPreviewData(
        int TotalColumns,
        double MinTime,
        double MaxTime,
        IReadOnlyList<double> BarLines,
        IReadOnlyList<ManiaPreviewNote> Notes);

    public readonly record struct PreviewQuad(float X, float Y, float Width, float Height, Color4 Colour);

    public interface IManiaStaticPreviewRenderer : IDisposable
    {
        void SetData(ManiaPreviewData data);

        void SetCurrentTime(double time);

        void SetDensity(float density);
    }
}
