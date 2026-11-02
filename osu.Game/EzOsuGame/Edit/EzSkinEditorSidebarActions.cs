// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// 侧栏栏目调用的屏幕操作。屏幕创建一次并放进依赖容器，栏目在 <c>load</c> 里取得。
    /// </summary>
    public sealed class EzSkinEditorSidebarActions
    {
        public Action? CommitSkinIni { get; init; }

        public Action? CreateNoteSnapshot { get; init; }

        public Action? RestoreNoteSnapshot { get; init; }

        public Action? ExportNotePreview { get; init; }
    }
}
