// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 舞台板上的按键贴图。每列一张底图，按下时在同一个槽位上叠加按下图。
    /// </summary>
    /// <remarks>
    /// 位置由 stage layout 的 <c>keys</c> 给出：整幅画布型美术各列 x 都是 0，平铺型则是各列中心。
    /// 声明了 <c>keysColumnWidth</c> 的键数由 <see cref="SetColumnWidths"/> 用列宽覆盖宽度，高度保持源图。
    /// </remarks>
    public partial class EzStageKeyLayer : CompositeDrawable
    {
        private readonly List<(Drawable? press, IBindable<bool>? pressed)> pressPairs = new List<(Drawable?, IBindable<bool>?)>();

        // 每列底图与按下图，以及它们创建时的源图高度（改宽度时高度沿用）。
        private readonly List<(int column, Drawable? drawable, float sourceHeight)> sizedDrawables = new List<(int, Drawable?, float)>();

        private readonly bool useColumnWidth;

        /// <summary>
        /// 该键数是否声明了按列宽缩放。为 <c>false</c> 时 <see cref="SetColumnWidths"/> 不起作用。
        /// </summary>
        public bool UsesColumnWidth => useColumnWidth;

        public EzStageKeyLayer(IReadOnlyList<Drawable?> bases, IReadOnlyList<Drawable?> presses, IReadOnlyList<IBindable<bool>?> pressed, bool useColumnWidth = false)
        {
            this.useColumnWidth = useColumnWidth;

            RelativeSizeAxes = Axes.None;

            for (int i = 0; i < bases.Count; i++)
            {
                if (bases[i] is Drawable baseDrawable)
                {
                    AddInternal(baseDrawable);
                    sizedDrawables.Add((i, baseDrawable, baseDrawable.Size.Y));
                }

                Drawable? press = i < presses.Count ? presses[i] : null;

                if (press != null)
                {
                    press.Alpha = 0;
                    AddInternal(press);
                    sizedDrawables.Add((i, press, press.Size.Y));
                }

                pressPairs.Add((press, i < pressed.Count ? pressed[i] : null));
            }
        }

        /// <summary>
        /// 按每列在舞台坐标下的宽度改写按键图宽度。高度保持源图，因此按键与其它键数同高。
        /// </summary>
        /// <param name="widths">按列序的宽度，单位与 layout 坐标一致；非正数表示该列不缩放。</param>
        public void SetColumnWidths(IReadOnlyList<float>? widths)
        {
            if (!useColumnWidth || widths == null)
                return;

            foreach (var (column, drawable, sourceHeight) in sizedDrawables)
            {
                if (drawable == null || column >= widths.Count)
                    continue;

                float width = widths[column];

                if (width > 0)
                    drawable.Size = new Vector2(width, sourceHeight);
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
