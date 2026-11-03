// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.BMS.Objects.Drawables
{
    public abstract partial class DrawableBMSHitObject : DrawableHitObject<BMSHitObject>
    {
        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        /// <summary>
        /// When set, judgements are ignored while this returns false (note lock / judge precedence).
        /// </summary>
        public Func<DrawableHitObject, double, bool>? CheckHittable;

        protected DrawableBMSHitObject(BMSHitObject? hitObject)
            : base(hitObject!)
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            Direction.BindTo(scrollingInfo.Direction);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
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

        public virtual void MissForcefully() => ApplyMinResult();

        #region note 音交由 BMSPlayfield 的按文件名通道池统一发声

        [Resolved(canBeNull: true)]
        private BMSPlayfield? playfield { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        /// <summary>
        /// 池可用时不向每条 note 自带的样本容器装载：keysound 由池按文件名发声，
        /// 逐 note 装载池化采样既浪费，又会在 playfield 上按音频名留下永不释放的
        /// <c>DrawablePool&lt;PoolableSkinnableSample&gt;</c>。
        /// 无池场景（未挂载到 BMSPlayfield 的构造）仍走基类装载，否则 <see cref="PlaySamples"/> 的回退路径会无声。
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
                base.PlaySamples();
                return;
            }

            var samples = GetSamples().Cast<ISampleInfo>().ToArray();

            if (samples.Length == 0)
                return;

            double balance = CalculateSamplePlaybackBalance(SamplePlaybackPosition);

            foreach (var sample in samples)
                pool.Play(sample, balance);

            gameplayState?.ApplySamples(samples);
        }

        #endregion
    }

    public abstract partial class DrawableBMSHitObject<TObject> : DrawableBMSHitObject
        where TObject : BMSHitObject
    {
        public new TObject HitObject => (TObject)base.HitObject;

        protected DrawableBMSHitObject(TObject? hitObject)
            : base(hitObject)
        {
        }
    }
}
