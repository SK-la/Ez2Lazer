// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.LAsEzExtensions.Screens
{
    public interface IEzConfig
    {
        Bindable<float> NoteSize { get; }
        Bindable<double> ColumnWidth { get; }
        Bindable<double> SpecialFactor { get; }
        Bindable<double> NoteHeightScaleToWidth { get; }
    }
}
