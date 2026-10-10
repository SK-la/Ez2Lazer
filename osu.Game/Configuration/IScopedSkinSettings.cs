// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;

namespace osu.Game.Configuration
{
    /// <summary>
    /// 皮肤设置按动效分组。编辑器在分组变化时重建当前可见的控件。
    /// </summary>
    public interface IScopedSkinSettings
    {
        void WatchSettingsScope(Action onChange);

        bool IsSettingActive(PropertyInfo property);
    }
}
