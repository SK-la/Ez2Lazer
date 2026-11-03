// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Localisation.Catch;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Catch
{
    [Cached]
    public partial class CatchInputManager : RulesetInputManager<CatchAction>
    {
        public CatchInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
        }

        protected override KeyBindingContainer<CatchAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new CatchKeyBindingContainer(this, ruleset, variant, unique);

        private partial class CatchKeyBindingContainer : RulesetKeyBindingContainer
        {
            private readonly CatchInputManager catchInputManager;

            public CatchKeyBindingContainer(CatchInputManager catchInputManager, RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
                this.catchInputManager = catchInputManager;
            }

            protected override bool Handle(UIEvent e)
            {
                switch (e)
                {
                    case JoystickPressEvent joystickPress when catchInputManager.ShouldSuppressJoystickAxisButton(joystickPress.Button):
                    case JoystickReleaseEvent joystickRelease when catchInputManager.ShouldSuppressJoystickAxisButton(joystickRelease.Button):
                        return false;
                }

                return base.Handle(e);
            }
        }
    }

    public enum CatchAction
    {
        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.MoveLeft))]
        MoveLeft,

        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.MoveRight))]
        MoveRight,

        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.Dash))]
        Dash,

        [LocalisableDescription(typeof(CatchEditorStrings), nameof(CatchEditorStrings.FruitTool))]
        EditorFruitTool = 10000,

        [LocalisableDescription(typeof(CatchEditorStrings), nameof(CatchEditorStrings.JuiceStreamTool))]
        EditorJuiceStreamTool,

        [LocalisableDescription(typeof(CatchEditorStrings), nameof(CatchEditorStrings.BananaShowerTool))]
        EditorBananaShowerTool,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.ToggleDistanceSnap))]
        EditorToggleDistanceSnap,
    }
}
