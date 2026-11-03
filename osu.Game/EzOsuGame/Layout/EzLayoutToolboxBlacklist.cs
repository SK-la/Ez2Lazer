// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning.Components;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Types hidden from the Ez layout editor toolbox.
    /// Official HUD components are intentionally not listed so they can be placed in the client layout.
    /// </summary>
    public sealed class EzLayoutToolboxBlacklist : ISkinComponentToolboxFilter
    {
        public static readonly EzLayoutToolboxBlacklist INSTANCE = new EzLayoutToolboxBlacklist();

        private readonly HashSet<Type> excluded = new HashSet<Type>
        {
            // BigBlackBox is a joke/debug skin component; keep it out of the client layout toolbox.
            typeof(BigBlackBox),
        };

        private EzLayoutToolboxBlacklist()
        {
        }

        public bool IsExcluded(Type type) => excluded.Contains(type);
    }
}
