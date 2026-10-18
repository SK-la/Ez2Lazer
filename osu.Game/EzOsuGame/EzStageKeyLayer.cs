// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 舞台板上的按键贴图。每列一张底图，按下时在同一个槽位上叠加按下图。
    /// </summary>
    /// <remarks>
    /// 位置由 stage layout 的 <c>keys</c> 给出：整幅画布型美术各列 x 都是 0，平铺型则是各列中心。
    /// 两种形态共用这套坐标，因此这里不做按列宽的二次缩放。
    /// </remarks>
    public partial class EzStageKeyLayer : CompositeDrawable
    {
        private readonly List<(Drawable? press, IBindable<bool>? pressed)> pressPairs = new List<(Drawable?, IBindable<bool>?)>();

        public EzStageKeyLayer(IReadOnlyList<Drawable?> bases, IReadOnlyList<Drawable?> presses, IReadOnlyList<IBindable<bool>?> pressed)
        {
            RelativeSizeAxes = Axes.None;

            for (int i = 0; i < bases.Count; i++)
            {
                if (bases[i] is Drawable baseDrawable)
                    AddInternal(baseDrawable);

                Drawable? press = i < presses.Count ? presses[i] : null;

                if (press != null)
                {
                    press.Alpha = 0;
                    AddInternal(press);
                }

                pressPairs.Add((press, i < pressed.Count ? pressed[i] : null));
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var (press, pressed) in pressPairs)
            {
                if (press == null || pressed == null)
                    continue;

                pressed.BindValueChanged(v => press.Alpha = v.NewValue ? 1 : 0, true);
            }
        }

        // 本层只是坐标原点，自身包围盒为零；交给各子项按自身范围判定，避免舞台移出屏幕时整层被裁掉。
        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
    }
}
