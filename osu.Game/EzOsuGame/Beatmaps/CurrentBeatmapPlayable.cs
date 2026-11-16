// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Beatmaps
{
    public readonly struct CurrentBeatmapSelection
    {
        public WorkingBeatmap Working { get; init; }
        public RulesetInfo Ruleset { get; init; }
        public IReadOnlyList<Mod> Mods { get; init; }
    }

    public interface ICurrentBeatmapPlayable
    {
        CurrentBeatmapSelection Snapshot { get; }

        IWorkingBeatmap Working { get; }

        IBeatmap GetPlayable(CancellationToken cancellationToken = default);

        IBeatmap GetMutable(Action<IBeatmap> postConvert = null, CancellationToken cancellationToken = default);
    }

    public class CurrentBeatmapPlayable : ICurrentBeatmapPlayable
    {
        private readonly IBindable<WorkingBeatmap> beatmap;
        private readonly IBindable<RulesetInfo> ruleset;
        private readonly IBindable<IReadOnlyList<Mod>> mods;

        public CurrentBeatmapPlayable(IBindable<WorkingBeatmap> beatmap, IBindable<RulesetInfo> ruleset, IBindable<IReadOnlyList<Mod>> mods)
        {
            this.beatmap = beatmap;
            this.ruleset = ruleset;
            this.mods = mods;
        }

        public CurrentBeatmapSelection Snapshot => new CurrentBeatmapSelection
        {
            Working = beatmap.Value,
            Ruleset = ruleset.Value,
            Mods = mods.Value,
        };

        public IWorkingBeatmap Working => beatmap.Value;

        public IBeatmap GetPlayable(CancellationToken cancellationToken = default)
            => beatmap.Value.GetPlayableBeatmap(ruleset.Value, mods.Value, cancellationToken);

        public IBeatmap GetMutable(Action<IBeatmap> postConvert = null, CancellationToken cancellationToken = default)
            => beatmap.Value.GetMutablePlayableBeatmap(ruleset.Value, mods.Value, cancellationToken, postConvert);
    }
}