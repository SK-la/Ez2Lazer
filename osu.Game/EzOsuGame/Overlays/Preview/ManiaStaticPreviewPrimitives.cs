// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public enum ManiaPreviewNoteKind
    {
        Tap,
        HoldHead,
        HoldBody,
        HoldTail,
    }

    public readonly record struct ManiaPreviewNote(double StartTime, double EndTime, int Column, ManiaPreviewNoteKind Kind);

    public readonly record struct ManiaPreviewData(
        int TotalColumns,
        IReadOnlyList<ManiaPreviewNote> Notes);

    public readonly record struct ManiaPreviewLayoutEntry(
        int Column,
        int Row,
        int EndRow,
        ManiaPreviewNoteKind Kind);

    public readonly record struct PreviewQuad(float X, float Y, float Width, float Height, Color4 Colour);

    public interface IManiaStaticPreviewRenderer : IDisposable
    {
        void SetData(ManiaPreviewData data);

        void SetCurrentTime(double time);

        void SetDensity(float density);
    }
}
