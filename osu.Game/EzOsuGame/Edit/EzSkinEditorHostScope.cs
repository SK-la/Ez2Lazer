// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Tracks which skin layout editor owns HUD/skin JSON writes, and blocks writes during cross-editor transitions.
    /// </summary>
    internal static class EzSkinEditorHostScope
    {
        public enum Host
        {
            None,
            OfficialHud,
            Ez,
        }

        public static Host Active { get; private set; }

        public static bool TransitionInProgress { get; private set; }

        public static bool CanWriteSkinLayout => !TransitionInProgress && Active != Host.None;

        public static void Enter(Host host) => Active = host;

        public static void Exit(Host host)
        {
            if (Active == host)
                Active = Host.None;
        }

        public static void BeginTransition() => TransitionInProgress = true;

        public static void EndTransition() => TransitionInProgress = false;
    }
}
