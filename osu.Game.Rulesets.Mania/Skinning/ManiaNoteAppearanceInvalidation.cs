// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// 列 note 外观失效强度：与 <see cref="IColumnNote"/> 三事件一一对应；FullLoad ⊃ Layout ⊃ Colour，合并取较大者。
    /// </summary>
    public enum ManiaNoteAppearanceInvalidation
    {
        Colour = 0,
        Layout = 1,
        FullLoad = 2,
    }

    public static class ManiaNoteAppearanceInvalidationExtensions
    {
        public static ManiaNoteAppearanceInvalidation Merge(this ManiaNoteAppearanceInvalidation current, ManiaNoteAppearanceInvalidation incoming)
            => incoming > current ? incoming : current;
    }
}
