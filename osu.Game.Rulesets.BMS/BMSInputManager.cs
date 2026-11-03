// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.BMS
{
    public partial class BMSInputManager : RulesetInputManager<BMSAction>
    {
        public BMSInputManager(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            : base(ruleset, variant, unique)
        {
        }
    }
}
