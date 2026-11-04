// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Edit.Tools;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Read-only mania compose preview for the Ez skin editor (no toolboxes, no beatmap edits).
    /// </summary>
    public partial class EzSkinEditorPreviewManiaHitObjectComposer : ManiaHitObjectComposer
    {
        public EzSkinEditorPreviewManiaHitObjectComposer(Ruleset ruleset)
            : base(ruleset)
        {
        }

        protected override bool IncludeComposeToolboxes => false;

        protected override bool CentreBeatSnapGridOnEditorClock => true;

        protected override IReadOnlyList<CompositionTool<ManiaAction>> CompositionTools => [];

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e) => false;

        protected override bool Handle(UIEvent e) => e is not ScrollEvent;
    }
}
