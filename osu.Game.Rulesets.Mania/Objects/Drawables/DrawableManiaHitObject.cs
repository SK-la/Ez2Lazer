// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    public abstract partial class DrawableManiaHitObject : DrawableHitObject<ManiaHitObject>
    {
        /// <summary>
        /// The <see cref="ManiaAction"/> which causes this <see cref="DrawableManiaHitObject{TObject}"/> to be hit.
        /// </summary>
        protected readonly IBindable<ManiaAction> Action = new Bindable<ManiaAction>();

        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        [Resolved(canBeNull: true)]
        private ManiaPlayfield playfield { get; set; }

        [Resolved(canBeNull: true)]
        private DrawableManiaRuleset drawableManiaRuleset { get; set; }

        internal DrawableManiaRuleset EzDrawableManiaRuleset => drawableManiaRuleset;

        protected bool UsesEzJudgement { get; private set; }

        /// <summary>
        /// 本地完整规则集由 Column 到期队列统一驱动 automiss；detached / 预览场景仍沿用 Drawable 自身更新。
        /// </summary>
        internal bool ColumnSchedulesAutoMiss
        {
            get => AutomissHandledExternally;
            set => AutomissHandledExternally = value;
        }

        protected override float SamplePlaybackPosition
        {
            get
            {
                if (playfield == null)
                    return base.SamplePlaybackPosition;

                return (float)HitObject.Column / playfield.TotalColumns;
            }
        }

        /// <summary>
        /// Whether this <see cref="DrawableManiaHitObject"/> can be hit, given a time value.
        /// If non-null, judgements will be ignored whilst the function returns false.
        /// </summary>
        public Func<DrawableHitObject, double, bool> CheckHittable;

        /// <summary>
        /// 列级按键已路由到其它 drawable 时跳过本物件的 <see cref="IKeyBindingHandler{T}.OnPressed"/>。
        /// </summary>
        public Func<DrawableHitObject, bool> ShouldSkipColumnRoutedPress;

        protected DrawableManiaHitObject(ManiaHitObject hitObject)
            : base(hitObject)
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader(true)]
        private void load([CanBeNull] IBindable<ManiaAction> action, [NotNull] IScrollingInfo scrollingInfo)
        {
            if (action != null)
                Action.BindTo(action);

            Direction.BindTo(scrollingInfo.Direction);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            drawableManiaRuleset ??= this.FindClosestParent<DrawableManiaRuleset>();
            UsesEzJudgement = drawableManiaRuleset?.JudgementRound?.IsEzHitMode
                              ?? (HitObject.HitWindows is ManiaHitWindows windows
                                  && windows.ActiveHitMode is not (EzEnumHitMode.Lazer or EzEnumHitMode.Classic));
            ColumnSchedulesAutoMiss = drawableManiaRuleset?.ColumnRoutesInput == true;

            Direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected virtual void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            Anchor = Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Miss:
                    this.FadeOut(150, Easing.In);
                    break;

                case ArmedState.Hit:
                    this.FadeOut();
                    break;
            }
        }

        #region note 音交由 ManiaPlayfield 的按文件名通道池统一发声

        [Resolved(canBeNull: true)]
        private GameplayState gameplayState { get; set; }

        /// <summary>
        /// 池可用时不向每条 note 自带的样本容器装载：note 音由池按文件名发声，
        /// 逐 note 装载每个 sample 的池化采样包装纯属浪费。
        /// 无池场景（皮肤预览、编辑器）仍走基类装载，否则 <see cref="PlaySamples"/> 的回退路径会无声。
        /// </summary>
        protected override void LoadSamples()
        {
            if (playfield?.SampleChannels == null)
                base.LoadSamples();
        }

        public override void PlaySamples()
        {
            var pool = playfield?.SampleChannels;

            if (pool == null)
            {
                // 非 gameplay（皮肤预览、编辑器等）没有 playfield 池，沿用每条 note 自带的样本播放。
                base.PlaySamples();
                return;
            }

            // [Ez] mania 的 GetSamples() 就是 HitObject.Samples（ICollection），ToArray() 走精确长度快路径，
            // 不再经过 Cast 迭代器。数组仍每次新建，因为 GameplayState.ApplySamples 靠引用不等触发绑定变更。
            var samples = GetSamples().ToArray();

            if (samples.Length == 0)
                return;

            double balance = CalculateSamplePlaybackBalance(SamplePlaybackPosition);

            foreach (var sample in samples)
                pool.Play(sample, balance);

            gameplayState?.ApplySamples(samples);
        }

        #endregion

        internal bool EvaluateColumnAutoMiss() => UpdateResult(false);

        /// <summary>
        /// 被动 miss：在通知 <see cref="ScoreProcessor"/> 前写入 stored TimeOffset（与 Session end-sweep 对齐）。
        /// </summary>
        internal void EzApplyPassiveMissWithStoredOffset()
        {
            ApplyResultWithStoredTiming(
                static (r, _) =>
                {
                    r.Type = HitResult.Miss;
                    r.IsComboHit = false;
                },
                0,
                ManiaDrawableMissTiming.ResolveStoredOffset(this));
        }

        internal void EzApplyBmsAutoMissFinalWithStoredOffset(HitResult result, EzEnumHitMode hitMode)
        {
            ApplyResultWithStoredTiming(
                static (r, data) =>
                {
                    r.Type = data.result;

                    if (data.result == HitResult.Miss
                        || (data.result == HitResult.Meh && HitModeHelper.MehBreaksCombo(data.hitMode)))
                    {
                        r.IsComboHit = false;
                    }
                },
                (result, hitMode),
                ManiaDrawableMissTiming.ResolveStoredOffset(this));
        }

        /// <summary>
        /// Causes this <see cref="DrawableManiaHitObject"/> to get missed, disregarding all conditions in implementations of <see cref="DrawableHitObject.CheckForResult"/>.
        /// </summary>
        public virtual void MissForcefully() => ApplyMinResult();
    }

    public abstract partial class DrawableManiaHitObject<TObject> : DrawableManiaHitObject
        where TObject : ManiaHitObject
    {
        public new TObject HitObject => (TObject)base.HitObject;

        protected DrawableManiaHitObject(TObject hitObject)
            : base(hitObject)
        {
        }
    }
}
