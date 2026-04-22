// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
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
        private const double default_time_range = 1500;
        private const double default_scroll_speed = 25;

        private int? cachedTotalColumns;
        private readonly BindableDouble configScrollSpeed = new BindableDouble();

        protected override bool RelativeScaleBeatLengths => true;

        protected new BMSRulesetConfigManager Config => (BMSRulesetConfigManager)base.Config;

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
            TimeRange.Value = default_time_range;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Config.BindWith(BMSRulesetSetting.ScrollSpeed, configScrollSpeed);
            configScrollSpeed.BindValueChanged(speed =>
            {
                if (!AllowScrollSpeedAdjustment)
                    return;

                TimeRange.Value = computeScrollTime(speed.NewValue);
            }, true);
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

        private static double computeScrollTime(double scrollSpeed)
            => Math.Clamp(default_time_range * default_scroll_speed / Math.Max(1, scrollSpeed), 200, 20000);
    }
}
