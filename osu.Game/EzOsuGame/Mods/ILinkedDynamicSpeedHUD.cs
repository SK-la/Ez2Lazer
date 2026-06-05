// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    public interface ILinkedDynamicSpeedHUD : IApplicableToHUD
    {
        BindableBool LinkSpeedHUD { get; }

        BindableBool ShowSpeedText { get; }

        BindableBool ShowSpeedLine { get; }

        BindableNumber<double> SpeedChange { get; }
    }
}
