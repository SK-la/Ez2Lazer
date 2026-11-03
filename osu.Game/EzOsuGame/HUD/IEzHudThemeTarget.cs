// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 皮肤编辑器按槽位改主题时，要一起写入的 HUD 组件。
    /// </summary>
    public interface IEzHudThemeTarget
    {
        EzHudThemeSlot Slot { get; }

        Bindable<string> Theme { get; }
    }

    public enum EzHudThemeSlot
    {
        Judgement,
        Combo,
        EarlyLate,
    }
}
