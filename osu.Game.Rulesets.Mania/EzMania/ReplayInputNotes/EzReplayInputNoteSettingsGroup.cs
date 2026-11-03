// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Screens.Play.PlayerSettings;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// Replay sidebar section for the input note overlay.
    /// </summary>
    /// <remarks>
    /// Both switches are per-playback: they are bound straight to the model and are deliberately not written to the
    /// ruleset configuration, so a replay always starts in the default mode.
    /// </remarks>
    public partial class EzReplayInputNoteSettingsGroup : PlayerSettingsGroup
    {
        private readonly EzReplayInputNoteModel model;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_ENABLED_LABEL), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_ENABLED_DESCRIPTION),
            SettingControlType = typeof(PlayerCheckbox))]
        public BindableBool ShowOverlay => model.ShowOverlay;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_COLOUR_LABEL), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_COLOUR_DESCRIPTION),
            SettingControlType = typeof(PlayerCheckbox))]
        public BindableBool ColourByHitResult => model.ColourByHitResult;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_ONLY_JUDGED_LABEL), nameof(EzHUDStrings.REPLAY_INPUT_NOTE_ONLY_JUDGED_DESCRIPTION),
            SettingControlType = typeof(PlayerCheckbox))]
        public BindableBool OnlyJudgedNotes => model.OnlyJudgedNotes;

        public EzReplayInputNoteSettingsGroup(EzReplayInputNoteModel model)
            : base(EzHUDStrings.REPLAY_INPUT_NOTE_TITLE)
        {
            this.model = model;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(this.CreateSettingsControls());
        }
    }
}
