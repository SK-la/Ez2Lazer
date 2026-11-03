// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum EzNotificationBehaviour
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.NOTIFICATION_BEHAVIOUR_NORMAL))]
        Normal = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.NOTIFICATION_BEHAVIOUR_IN_GAME_FOCUS))]
        InGameFocus = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.NOTIFICATION_BEHAVIOUR_NEVER))]
        Never = 2,
    }
}
