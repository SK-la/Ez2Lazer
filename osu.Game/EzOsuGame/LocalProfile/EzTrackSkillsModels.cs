// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Ruleset-specific Track Skills data + optional mania side panel wiring.
    /// </summary>
    public interface IEzTrackSkillsPresenter
    {
        string SystemId { get; }

        bool HasSliceChips { get; }

        bool HasSidePanel { get; }

        int AlgorithmVersion => EzSkillSystems.ResolveAlgorithmVersion(SystemId);

        IReadOnlyList<int> GetSliceKeys(string username);

        EzTrackSkillsSnapshot Load(string username, int sliceKey);

        LocalisableString ResolveDisplayName(string skillId);

        void ConfigureSidePanel(EzHUDDanDualPanel danPanel, BindableInt selectedSlice, string username);
    }

    public readonly record struct EzTrackSkillsAxis(
        string SkillId,
        LocalisableString DisplayName,
        double Value,
        string AccentHex);

    public sealed class EzTrackSkillsSnapshot
    {
        public static EzTrackSkillsSnapshot Empty { get; } = new EzTrackSkillsSnapshot();

        public LocalisableString HeaderTitle { get; init; }

        public double Overall { get; init; }

        public int PlaysAnalyzed { get; init; }

        public bool Provisional { get; init; }

        public bool Stale { get; init; }

        public IReadOnlyList<EzTrackSkillsAxis> RadarAxes { get; init; } = [];

        public IReadOnlyList<EzTrackSkillsAxis> BarAxes { get; init; } = [];

        public bool IsEffectivelyProvisional =>
            Provisional || PlaysAnalyzed < EzPlayerSsrSnapshot.QUALIFYING_PLAYS;
    }

    public static class EzTrackSkillsPresenterFactory
    {
        public static IEzTrackSkillsPresenter? Create(EzSkillProfile? profile, EzSkillProvider provider)
        {
            if (profile == null || provider == null)
                return null;

            return profile.DefaultPlayerSystemId switch
            {
                EzSkillSystems.PLAYER_SSR => new ManiaTrackSkillsPresenter(provider),
                EzSkillSystems.PLAYER_PPPLUS => new OsuTrackSkillsPresenter(provider),
                _ => null,
            };
        }
    }
}
