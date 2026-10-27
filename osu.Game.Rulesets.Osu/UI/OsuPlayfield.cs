// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.EzOsu.Judgement.Live;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables.Connections;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    [Cached]
    public partial class OsuPlayfield : Playfield
    {
        private readonly Container borderContainer;
        private readonly PlayfieldBorder playfieldBorder;
        private readonly ProxyContainer approachCircles;
        private readonly ProxyContainer spinnerProxies;
        private readonly JudgementContainer<DrawableOsuJudgement> judgementLayer;

        private OsuKernelDrawableAdapter? kernelAdapter;

        /// <summary>本 playfield 是否由统一判定内核驱动。</summary>
        internal bool JudgementKernelActive => kernelAdapter != null;

        /// <summary>
        /// 把 Drawable 已分发到的按下交给内核判定。返回 <see langword="false"/> 表示内核未启用，调用方应走原有自查。
        /// </summary>
        internal bool TryKernelPress(DrawableOsuHitObject drawable, OsuAction action)
        {
            if (kernelAdapter == null)
                return false;

            kernelAdapter.TryPress(drawable, action);
            return true;
        }

        private readonly JudgementPooler<DrawableOsuJudgement> judgementPooler;

        // For osu! gameplay, everything is always on screen.
        // Skipping masking calculations improves performance in intense beatmaps (ie. https://osu.ppy.sh/beatmapsets/150945#osu/372245)
        public override bool UpdateSubTreeMasking() => false;

        public SmokeContainer Smoke { get; }
        public FollowPointRenderer FollowPoints { get; }

        public static readonly Vector2 BASE_SIZE = new Vector2(512, 384);

        protected override GameplayCursorContainer? CreateCursor() => new OsuCursorContainer();

        public override Quad SkinnableComponentScreenSpaceDrawQuad => playfieldBorder.ScreenSpaceDrawQuad;

        private readonly Container judgementAboveHitObjectLayer;

        public OsuPlayfield()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                borderContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = playfieldBorder = new PlayfieldBorder { RelativeSizeAxes = Axes.Both },
                },
                Smoke = new SmokeContainer { RelativeSizeAxes = Axes.Both },
                spinnerProxies = new ProxyContainer { RelativeSizeAxes = Axes.Both },
                FollowPoints = new FollowPointRenderer { RelativeSizeAxes = Axes.Both },
                judgementLayer = new JudgementContainer<DrawableOsuJudgement> { RelativeSizeAxes = Axes.Both },
                HitObjectContainer,
                judgementAboveHitObjectLayer = new Container { RelativeSizeAxes = Axes.Both },
                approachCircles = new ProxyContainer { RelativeSizeAxes = Axes.Both },
            };

            HitPolicy = new StartTimeOrderedHitPolicy();

            AddInternal(judgementPooler = new JudgementPooler<DrawableOsuJudgement>(new[]
            {
                HitResult.Great,
                HitResult.Ok,
                HitResult.Meh,
                HitResult.Miss,
                HitResult.LargeTickHit,
                HitResult.SliderTailHit,
                HitResult.LargeTickMiss,
                HitResult.IgnoreMiss,
            }, onJudgementLoaded));

            NewResult += onNewResult;
        }

        private IHitPolicy hitPolicy;

        /// <summary>
        /// 是否启用 stable note lock 的 Drawable 侧：已判中的滑条头遮挡其下方物件，直到整条滑条判完。
        /// 只由判定档置位，与经典 mod 无关。
        /// </summary>
        public bool BlockInputUnderSliderHeads { get; set; }

        public IHitPolicy HitPolicy
        {
            get => hitPolicy;
            [MemberNotNull(nameof(hitPolicy))]
            set
            {
                hitPolicy = value ?? throw new ArgumentNullException(nameof(value));
                hitPolicy.HitObjectContainer = HitObjectContainer;
            }
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawable)
        {
            ((DrawableOsuHitObject)drawable).CheckHittable = hitPolicy.CheckHittable;

            if (BlockInputUnderSliderHeads && drawable is DrawableSliderHead head)
                StableClassicNoteLock.BlockInputUnderSliderHead(head);

            if (UseStableViewportBodies && drawable is DrawableSlider slider)
                slider.UseStableViewportBody = true;

            Debug.Assert(!drawable.IsLoaded, $"Already loaded {nameof(DrawableHitObject)} is added to {nameof(OsuPlayfield)}");
            drawable.OnLoadComplete += onDrawableHitObjectLoaded;
        }

        /// <summary>
        /// 滑条身是否像 osu!stable 一样栅格化到一张窗口大小的画布上。
        /// </summary>
        /// <remarks>
        /// 仅 aspire 谱面与 stable classic 判定档走此路径；普通 lazer 游玩不受影响。
        /// 从 score processor 解析一次，与判定档同源。
        /// </remarks>
        private bool UseStableViewportBodies { get; set; }

        private void onDrawableHitObjectLoaded(Drawable drawable)
        {
            // note: `Slider`'s `ProxiedLayer` is added when its nested `DrawableHitCircle` is loaded.
            switch (drawable)
            {
                case DrawableSpinner:
                    spinnerProxies.Add(drawable.CreateProxy());
                    break;

                case DrawableHitCircle hitCircle:
                    approachCircles.Add(hitCircle.ProxiedLayer.CreateProxy());
                    break;
            }
        }

        private void onJudgementLoaded(DrawableOsuJudgement judgement)
        {
            judgementAboveHitObjectLayer.Add(judgement.ProxiedAboveHitObjectsContent);
        }

        [BackgroundDependencyLoader]
        private void load(OsuRulesetConfigManager? config, IBeatmap? beatmap, ScoreProcessor? scoreProcessor, Ez2ConfigManager? ezConfig)
        {
            config?.BindWith(OsuRulesetSetting.PlayfieldBorderStyle, playfieldBorder.PlayfieldBorderStyle);

            // 判定档是 stable 输入机制的开关：note lock 必须在任何 drawable 创建前定好，因此放在 pools 之前。
            // 只认判定档，不认经典 mod。
            if (scoreProcessor is OsuScoreProcessor osuScoreProcessor && OsuStableClassic.IsActive(osuScoreProcessor.JudgeMode))
                OsuStableClassic.ApplyLiveRules(this, osuScoreProcessor.Mods.Value.Any(m => m is OsuModAutopilot));

            // 滑条主体的绘制口径：aspire 谱面或 stable classic 判定档下按 stable 的方式绘制到一张窗口大小的画布上。
            // 必须在 drawable 创建前定好，普通 lazer 游玩不受影响。
            if (scoreProcessor is OsuScoreProcessor bodyScoreProcessor)
            {
                UseStableViewportBodies = OsuStableClassic.IsActive(bodyScoreProcessor.JudgeMode)
                                          || bodyScoreProcessor.Mods.Value.Any(m => m is OsuModAspire);
            }

            // 内核必须在 drawable 创建前就位：drawable 依赖 playfield 的 JudgementKernelActive 关闭自查。
            if (scoreProcessor is OsuScoreProcessor kernelScoreProcessor && beatmap != null)
            {
                AddInternal(kernelAdapter = new OsuKernelDrawableAdapter(this, beatmap, kernelScoreProcessor, ezConfig));

                if (hitPolicy is LegacyHitPolicy legacyHitPolicy)
                    legacyHitPolicy.KernelQuery = (hitObject, time) => kernelAdapter.CheckHittable(hitObject, time);
            }

            var osuBeatmap = (OsuBeatmap?)beatmap;

            RegisterPool<HitCircle, DrawableHitCircle>(20, 100);

            // handle edge cases where a beatmap has a slider with many repeats.
            int maxRepeatsOnOneSlider = 0;
            int maxTicksOnOneSlider = 0;

            if (osuBeatmap != null)
            {
                foreach (var slider in osuBeatmap.HitObjects.OfType<Slider>())
                {
                    maxRepeatsOnOneSlider = Math.Max(maxRepeatsOnOneSlider, slider.RepeatCount);
                    maxTicksOnOneSlider = Math.Max(maxTicksOnOneSlider, slider.NestedHitObjects.OfType<SliderTick>().Count());
                }
            }

            RegisterPool<Slider, DrawableSlider>(20, 100);
            RegisterPool<SliderHeadCircle, DrawableSliderHead>(20, 100);
            RegisterPool<SliderTailCircle, DrawableSliderTail>(20, 100);

            // 池的初始大小是预先创建多少实例，最大值只限制能长到多大。
            // 两者都跟谱面最大折返数走时，一条有数千折返的滑条会在加载时、尚未绘制前就准备数千 drawable。
            // 预热有限数量，需要时再增长。
            const int pool_warmup_limit = 200;

            RegisterPool<SliderTick, DrawableSliderTick>(
                Math.Min(Math.Max(maxTicksOnOneSlider, 20), pool_warmup_limit),
                Math.Max(maxTicksOnOneSlider, 200));

            RegisterPool<SliderRepeat, DrawableSliderRepeat>(
                Math.Min(Math.Max(maxRepeatsOnOneSlider, 20), pool_warmup_limit),
                Math.Max(maxRepeatsOnOneSlider, 200));

            RegisterPool<Spinner, DrawableSpinner>(2, 20);
            RegisterPool<SpinnerTick, DrawableSpinnerTick>(10, 200);
            RegisterPool<SpinnerBonusTick, DrawableSpinnerBonusTick>(10, 200);

            if (beatmap != null)
                ApplyCircleSizeToPlayfieldBorder(beatmap);
        }

        protected void ApplyCircleSizeToPlayfieldBorder(IBeatmap beatmap)
        {
            borderContainer.Padding = new MarginPadding(OsuHitObject.OBJECT_RADIUS * -LegacyRulesetExtensions.CalculateScaleFromCircleSize(beatmap.Difficulty.CircleSize, true));
        }

        protected override HitObjectLifetimeEntry CreateLifetimeEntry(HitObject hitObject) => new OsuHitObjectLifetimeEntry(hitObject);

        protected override void OnHitObjectAdded(HitObject hitObject)
        {
            base.OnHitObjectAdded(hitObject);
            FollowPoints.AddFollowPoints((OsuHitObject)hitObject);
        }

        protected override void OnHitObjectRemoved(HitObject hitObject)
        {
            base.OnHitObjectRemoved(hitObject);
            FollowPoints.RemoveFollowPoints((OsuHitObject)hitObject);
        }

        private void onNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            // 内核驱动时 note lock 与强制 Miss 都由内核完成，Drawable 侧不再二次仲裁。
            if (!JudgementKernelActive)
                hitPolicy.HandleHit(judgedObject);

            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            var explosion = judgementPooler.Get(result.Type, doj => doj.Apply(result, judgedObject));

            if (explosion == null)
                return;

            judgementLayer.Add(explosion);

            // the proxied content is added to judgementAboveHitObjectLayer once, on first load, and never removed from it.
            // ensure that ordering is consistent with expectations (latest judgement should be front-most).
            judgementAboveHitObjectLayer.ChangeChildDepth(explosion.ProxiedAboveHitObjectsContent, (float)-result.TimeAbsolute);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => HitObjectContainer.ReceivePositionalInputAt(screenSpacePos);

        private OsuResumeOverlay.OsuResumeOverlayInputBlocker? resumeInputBlocker;

        public void AttachResumeOverlayInputBlocker(OsuResumeOverlay.OsuResumeOverlayInputBlocker resumeInputBlocker)
        {
            Debug.Assert(this.resumeInputBlocker == null);
            this.resumeInputBlocker = resumeInputBlocker;
            AddInternal(resumeInputBlocker);
        }

        private partial class ProxyContainer : LifetimeManagementContainer
        {
            public void Add(Drawable proxy) => AddInternal(proxy);
        }

        private class OsuHitObjectLifetimeEntry : HitObjectLifetimeEntry
        {
            public OsuHitObjectLifetimeEntry(HitObject hitObject)
                : base(hitObject)
            {
                // Prevent past objects in idles states from remaining alive as their end times are skipped in non-frame-stable contexts.
                applyEnd();

                // 解析规则与 defaults 可能在条目已存在后才落地。stable 结束时刻要等速度已知。
                hitObject.DefaultsApplied += _ => applyEnd();
            }

            protected override double InitialLifetimeOffset => ((OsuHitObject)HitObject).TimePreempt;

            private void applyEnd()
            {
                // stable 结束变成 int.MinValue 的滑条已经结束。若 LifetimeEnd 留在真实结束，drawable 与其身缓冲
                // 会一直留在更新列表直到谱面结束。
                if (HitObject is Slider slider && slider.RevealEndTimeWrapsUnderStableRules)
                {
                    LifetimeEnd = slider.StableRevealEndTime;
                    return;
                }

                LifetimeEnd = HitObject.GetEndTime() + HitObject.HitWindows.WindowFor(HitResult.Miss);
            }
        }
    }
}
