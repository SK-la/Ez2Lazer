// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.BMS.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.BMS.UI
{
    [Cached]
    public partial class DrawableBMSRuleset : DrawableScrollingRuleset<BMSHitObject>
    {
        private int? cachedTotalColumns;

        public int TotalColumns
        {
            get
            {
                if (cachedTotalColumns.HasValue)
                    return cachedTotalColumns.Value;

                // Calculate from hit objects
                var maxColumn = Beatmap.HitObjects.Select(h => h.Column).DefaultIfEmpty(0).Max();
                cachedTotalColumns = maxColumn + 1;
                return cachedTotalColumns.Value;
            }
        }

        public DrawableBMSRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            Direction.Value = ScrollingDirection.Down;
            TimeRange.Value = 1500;
        }

        protected override Playfield CreatePlayfield() => new BMSPlayfield(TotalColumns);

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay)
            => new BMSFramedReplayInputHandler(replay);

        public override DrawableHitObject<BMSHitObject>? CreateDrawableRepresentation(BMSHitObject hitObject)
        {
            switch (hitObject)
            {
                case BMSHoldNote holdNote:
                    return new DrawableBMSHoldNote(holdNote);

                case BMSNote note:
                    return new DrawableBMSNote(note);
            }

            return null;
        }

        protected override PassThroughInputManager CreateInputManager() =>
            new BMSInputManager(Ruleset.RulesetInfo, TotalColumns, SimultaneousBindingMode.Unique);
    }
}
