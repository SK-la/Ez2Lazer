// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 按键输入入口。按键贴图由舞台板绘制（见 EzStageKeyLayer），这里只把按下状态写进
    /// <see cref="EzStageKeyState"/>，因此视觉与 Body、血条共用同一套舞台坐标和缩放。
    /// </summary>
    public partial class EzKeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private Bindable<bool>? pressed;
        private int releaseVersion;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private EzStageKeyState? keyState { get; set; }

        public EzKeyArea()
        {
            // 该组件不绘制任何内容，但仍需参与输入：IsPresent 依赖 Alpha > 0，
            // 因此不能把 Alpha 设为 0，否则按键事件会被直接跳过。
            RelativeSizeAxes = Axes.None;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            pressed = keyState?.GetPressed(column.Index) as Bindable<bool>;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (pressed != null && e.Action == column.Action.Value)
            {
                // 取消上一次释放留下的延时回落，避免快速重按时被旧回调熄掉。
                releaseVersion++;
                pressed.Value = true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value)
                return;

            // 与旧实现一致：底图延迟一点恢复，按下图先淡出。
            int version = ++releaseVersion;

            Scheduler.AddDelayed(() =>
            {
                if (pressed != null && version == releaseVersion)
                    pressed.Value = false;
            }, LegacyHitExplosion.FADE_IN_DURATION);
        }
    }
}
