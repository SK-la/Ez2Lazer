// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Localisation.Mania;
#if DEBUG
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects.Drawables;
#endif
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania
{
    [Cached] // Used for touch input, see Column.OnTouchDown/OnTouchUp.
    public partial class ManiaInputManager : RulesetInputManager<ManiaAction>
    {
        private readonly int variant;

        public ManiaInputManager(RulesetInfo ruleset, int variant)
            : base(ruleset, variant, SimultaneousBindingMode.Unique)
        {
            this.variant = variant;
        }

        protected override KeyBindingContainer<ManiaAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new ManiaKeyBindingContainer(this, ruleset, variant, unique);

        /// <summary>
        /// COLUMN-INPUT：列级 <see cref="Column.OnPressed"/> 优先于列内 drawable，避免每键 N 路冒泡。
        /// </summary>
        /// <remarks>
        /// 基类每次读 <c>base.KeyBindingInputQueue</c> 都会清空并重建整棵 mania 子树的输入队列，因此这里：
        /// 一次枚举内只读基类一次（不能逐项 <c>yield</c>，那会读两遍、重建两次），按帧缓存重建结果，
        /// 并返回可复用列表本身 —— 调用方 <c>AddRange</c> 走 <c>ICollection</c> 快路径拷贝，而不是逐项枚举迭代器。
        /// 输入事件在本帧子树更新开始前一次性派发完毕，note 的生成 / 回收只发生在其后的 Update。
        /// </remarks>
        private partial class ManiaKeyBindingContainer : RulesetKeyBindingContainer
        {
            private readonly ManiaInputManager maniaInputManager;

            /// <summary>本帧物化结果：<see cref="Column"/> 在前，其余保持基类顺序。</summary>
            private readonly List<Drawable> columnFirstQueue = new List<Drawable>();

            /// <summary>物化时的暂存区（非列项），只在本类内部使用。</summary>
            private readonly List<Drawable> nonColumnQueue = new List<Drawable>();

            private bool inputQueueCached;

            public ManiaKeyBindingContainer(ManiaInputManager maniaInputManager, RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
                this.maniaInputManager = maniaInputManager;
            }

            protected override void Update()
            {
                // 下一帧重新物化：note 的生成/回收都发生在本容器的 Update 之后，此处失效不会让快照落后于树结构。
                inputQueueCached = false;

                base.Update();
            }

            protected override bool Handle(UIEvent e)
            {
                // 转盘轴启用时：屏蔽「轴正负虚拟键」进键位，改由 ScratchAxis 算法注入（对齐 beatoraja）
                switch (e)
                {
                    case JoystickPressEvent joystickPress when maniaInputManager.ShouldSuppressJoystickAxisButton(joystickPress.Button):
                    case JoystickReleaseEvent joystickRelease when maniaInputManager.ShouldSuppressJoystickAxisButton(joystickRelease.Button):
                        return false;
                }

                return base.Handle(e);
            }

            protected override IEnumerable<Drawable> KeyBindingInputQueue
            {
                get
                {
                    if (inputQueueCached)
                    {
#if DEBUG
                        ManiaJudgeHotPathTrace.RecordInputQueueReuse(columnFirstQueue.Count);
#endif
                        return columnFirstQueue;
                    }

                    columnFirstQueue.Clear();
                    nonColumnQueue.Clear();

#if DEBUG
                    int holdEnds = 0;
#endif

                    // 只读一次基类队列：读两次就是两次整棵子树重建。
                    foreach (var drawable in base.KeyBindingInputQueue)
                    {
                        if (drawable is Column)
                            columnFirstQueue.Add(drawable);
                        else
                            nonColumnQueue.Add(drawable);

#if DEBUG
                        if (drawable is DrawableHoldNoteHead or DrawableHoldNoteTail)
                            holdEnds++;
#endif
                    }

                    columnFirstQueue.AddRange(nonColumnQueue);

                    inputQueueCached = true;
#if DEBUG
                    ManiaJudgeHotPathTrace.RecordInputQueueRebuild(columnFirstQueue.Count, holdEnds);
#endif

                    return columnFirstQueue;
                }
            }
        }
    }

    public enum ManiaAction
    {
        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key1))]
        Key1,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key2))]
        Key2,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key3))]
        Key3,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key4))]
        Key4,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key5))]
        Key5,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key6))]
        Key6,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key7))]
        Key7,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key8))]
        Key8,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key9))]
        Key9,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key10))]
        Key10,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key11))]
        Key11,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key12))]
        Key12,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key13))]
        Key13,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key14))]
        Key14,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key15))]
        Key15,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key16))]
        Key16,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key17))]
        Key17,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key18))]
        Key18,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key19))]
        Key19,

        [LocalisableDescription(typeof(ActionStringsHelper), nameof(ActionStringsHelper.Key20))]
        Key20,

        [LocalisableDescription(typeof(ManiaEditorStrings), nameof(ManiaEditorStrings.NoteTool))]
        EditorNoteTool = 10000,

        [LocalisableDescription(typeof(ManiaEditorStrings), nameof(ManiaEditorStrings.HoldNoteTool))]
        EditorHoldNoteTool,
    }

    // Workaround for the inability to pass arguments to `LocalisableDescription`.
    // Should be removed if such a feature is added at all.
    static file class ActionStringsHelper
    {
        public static LocalisableString Key1 => ActionStrings.Key(1);
        public static LocalisableString Key2 => ActionStrings.Key(2);
        public static LocalisableString Key3 => ActionStrings.Key(3);
        public static LocalisableString Key4 => ActionStrings.Key(4);
        public static LocalisableString Key5 => ActionStrings.Key(5);
        public static LocalisableString Key6 => ActionStrings.Key(6);
        public static LocalisableString Key7 => ActionStrings.Key(7);
        public static LocalisableString Key8 => ActionStrings.Key(8);
        public static LocalisableString Key9 => ActionStrings.Key(9);
        public static LocalisableString Key10 => ActionStrings.Key(10);
        public static LocalisableString Key11 => ActionStrings.Key(11);
        public static LocalisableString Key12 => ActionStrings.Key(12);
        public static LocalisableString Key13 => ActionStrings.Key(13);
        public static LocalisableString Key14 => ActionStrings.Key(14);
        public static LocalisableString Key15 => ActionStrings.Key(15);
        public static LocalisableString Key16 => ActionStrings.Key(16);
        public static LocalisableString Key17 => ActionStrings.Key(17);
        public static LocalisableString Key18 => ActionStrings.Key(18);
        public static LocalisableString Key19 => ActionStrings.Key(19);
        public static LocalisableString Key20 => ActionStrings.Key(20);
    }
}
