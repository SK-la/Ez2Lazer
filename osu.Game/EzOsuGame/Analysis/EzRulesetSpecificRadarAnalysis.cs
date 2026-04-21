// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Analysis
{
    public readonly record struct EzRadarAxisValue<TAxis>(TAxis Axis, double Value, string Format = "0.0");

    public readonly struct EzRadarChartData<TAxis>
    {
        public const int AXIS_COUNT = 6;

        private readonly IReadOnlyList<EzRadarAxisValue<TAxis>> axes;

        public IReadOnlyList<EzRadarAxisValue<TAxis>> Axes => axes ?? Array.Empty<EzRadarAxisValue<TAxis>>();

        public int Count => Axes.Count;

        public EzRadarAxisValue<TAxis> this[int index] => Axes[index];

        public EzRadarChartData(IReadOnlyList<EzRadarAxisValue<TAxis>> axes)
        {
            ArgumentNullException.ThrowIfNull(axes);

            if (axes.Count != AXIS_COUNT)
                throw new ArgumentException($@"Radar chart data must contain exactly {AXIS_COUNT} axes.", nameof(axes));

            this.axes = axes;
        }

        public static EzRadarChartData<TAxis> Create(params EzRadarAxisValue<TAxis>[] axes) => new EzRadarChartData<TAxis>(axes);
    }
}
