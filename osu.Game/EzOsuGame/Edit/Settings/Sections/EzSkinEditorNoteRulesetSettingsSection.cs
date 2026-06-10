// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorNoteRulesetSettingsSection : FillFlowContainer
    {
        private readonly EzSkinEditorNoteEditSession session;
        private readonly Action requestRefresh;

        private readonly BindableList<RulesetInfo> rulesets = new BindableList<RulesetInfo>();

        private FillFlowContainer rulesetSettingsContainer = null!;

        public EzSkinEditorNoteRulesetSettingsSection(EzSkinEditorNoteEditSession session, Action requestRefresh)
        {
            this.session = session;
            this.requestRefresh = requestRefresh;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rulesets.AddRange(EzSkinEditorNoteRulesetProfileRegistry.All.Select(p => p.RulesetInfo));

            if (session.Ruleset.Value is null && rulesets.Count > 0)
                session.Ruleset.Value = rulesets[0];

            Add(new SettingsDropdown<RulesetInfo>
            {
                LabelText = EzEditorStrings.NOTE_RULESET_LABEL,
                Current = session.Ruleset,
                ItemSource = rulesets,
            });

            Add(new SettingsEnumDropdown<EzSkinEditorNotePart>
            {
                LabelText = EzEditorStrings.NOTE_PART_LABEL,
                Current = session.Part,
            });

            Add(rulesetSettingsContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
            });

            session.Ruleset.BindValueChanged(_ => refreshRulesetSettings(), true);
            session.Ruleset.BindValueChanged(_ => requestRefresh(), false);
            session.Part.BindValueChanged(_ => requestRefresh(), false);
        }

        private void refreshRulesetSettings()
        {
            rulesetSettingsContainer.Clear();

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(session.Ruleset.Value);

            var rulesetSettings = profile?.CreateRulesetSettingsContent();

            if (rulesetSettings != null)
                rulesetSettingsContainer.Add(rulesetSettings);
        }
    }
}
