// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Supplies Appearance 「皮肤专用」 content for one skin kind.
    /// Register implementations in <see cref="EzSkinEditorSkinScopedGroupRegistry"/>.
    /// </summary>
    public interface IEzSkinEditorSkinScopedGroupProvider
    {
        EzSkinEditorSkinKind Kind { get; }

        bool IsApplicable(EzSkinEditorSceneContext context);

        IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateGroups(EzSkinEditorSceneContext context);

        Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context) => null;
    }
}
