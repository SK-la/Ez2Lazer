// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 非交互式预览容器，禁用所有输入处理。
    /// </summary>
    public partial class NonHandleContainer : Container
    {
        public override bool HandlePositionalInput => false;

        public override bool HandleNonPositionalInput => false;

        public override bool PropagateNonPositionalInputSubTree => false;
    }
}
