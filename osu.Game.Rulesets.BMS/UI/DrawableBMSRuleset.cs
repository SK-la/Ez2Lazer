// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.BMS.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
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

        private BMSStageLayout? layoutCache;

        /// <summary>
        /// Stage layout for this beatmap (mods applied). Safe from <see cref="CreateInputManager"/> onward once <see cref="Beatmap"/> is set.
        /// </summary>
        private BMSStageLayout layout => layoutCache ??= BMSStageLayout.FromBeatmap(Beatmap);

        private readonly BindableDouble maniaScrollSpeed = new BindableDouble();
        private readonly BindableDouble maniaBaseSpeed = new BindableDouble();
        private readonly BindableDouble maniaTimePerSpeed = new BindableDouble();
        private readonly Bindable<ManiaScrollingDirection> maniaDirection = new Bindable<ManiaScrollingDirection>();

        protected override bool RelativeScaleBeatLengths => true;

        public int TotalColumns => layout.TotalColumns;

        public DrawableBMSRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            Direction.Value = ScrollingDirection.Down;
            TimeRange.Value = default_time_range;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(layout);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(IRulesetConfigCache rulesetConfigCache)
        {
            var maniaConfig = rulesetConfigCache.GetConfigFor(new ManiaRuleset()) as ManiaRulesetConfigManager;

            if (maniaConfig == null)
                return;

            maniaConfig.BindWith(ManiaRulesetSetting.ScrollDirection, maniaDirection);
            maniaDirection.BindValueChanged(direction => Direction.Value = (ScrollingDirection)direction.NewValue, true);

            maniaConfig.BindWith(ManiaRulesetSetting.ScrollBaseSpeed, maniaBaseSpeed);
            maniaConfig.BindWith(ManiaRulesetSetting.ScrollTimePerSpeed, maniaTimePerSpeed);
            maniaConfig.BindWith(ManiaRulesetSetting.ScrollSpeed, maniaScrollSpeed);

            maniaScrollSpeed.BindValueChanged(speed =>
            {
                if (!AllowScrollSpeedAdjustment)
                    return;

                TimeRange.Value = DrawableManiaRuleset.ComputeScrollTime(speed.NewValue, maniaBaseSpeed.Value, maniaTimePerSpeed.Value);
            });

            TimeRange.Value = DrawableManiaRuleset.ComputeScrollTime(maniaScrollSpeed.Value, maniaBaseSpeed.Value, maniaTimePerSpeed.Value);
        }

        protected override Playfield CreatePlayfield() => new BMSPlayfield(layout);

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
            new BMSInputManager(Ruleset.RulesetInfo, layout.TotalColumns, SimultaneousBindingMode.Unique);

        protected override void AdjustScrollSpeed(int amount) => maniaScrollSpeed.Value += amount;
    }
}
