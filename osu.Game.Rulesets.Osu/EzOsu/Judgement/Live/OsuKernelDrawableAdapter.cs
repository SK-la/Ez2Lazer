// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement.Live
{
    /// <summary>
    /// 实机适配器：采样光标 / 按键并驱动内核，把内核判定映射回 <see cref="DrawableHitObject"/>。
    /// Drawable 自身不再调用 <c>CheckForResult</c>。
    /// </summary>
    internal sealed partial class OsuKernelDrawableAdapter : Component
    {
        private readonly OsuPlayfield playfield;
        private readonly IBeatmap beatmap;
        private readonly OsuScoreProcessor scoreProcessor;
        private readonly Ez2ConfigManager? config;

        private readonly OsuLiveSampleSource samples = new OsuLiveSampleSource();
        private readonly Dictionary<HitObject, DrawableHitObject> drawableMap = new Dictionary<HitObject, DrawableHitObject>();

        private OsuJudgementKernel? kernel;
        private OsuInputManager? inputManager;
        private double ezOffset;
        private double lastAdvancedTo = double.NegativeInfinity;
        private bool rewinding;

        public OsuKernelDrawableAdapter(OsuPlayfield playfield, IBeatmap beatmap, OsuScoreProcessor scoreProcessor, Ez2ConfigManager? config)
        {
            this.playfield = playfield;
            this.beatmap = beatmap;
            this.scoreProcessor = scoreProcessor;
            this.config = config;

            RelativeSizeAxes = Axes.Both;
            Depth = float.MinValue;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            ezOffset = config?.Get<double>(Ez2Setting.OffsetPlusNonMania) ?? 0;
            resetKernel();
        }

        /// <summary>note lock 查询：供 <see cref="LegacyHitPolicy"/> 退化使用。</summary>
        public ClickAction CheckHittable(HitObject hitObject, double time)
            => kernel?.CheckHittable(hitObject, time) ?? ClickAction.Hit;

        protected override void Update()
        {
            base.Update();

            if (kernel == null)
                return;

            inputManager ??= GetContainingInputManager() as OsuInputManager;

            bool isRewinding = (Clock as IGameplayClock)?.IsRewinding == true;

            if (isRewinding)
            {
                if (!rewinding)
                {
                    rewinding = true;
                    samples.IsRewinding = true;
                }

                return;
            }

            if (rewinding)
            {
                rewinding = false;
                samples.IsRewinding = false;

                // 倒带结束：只回退晚于当前时刻的判定，保留已判结果与滑条跟踪状态。
                double rewoundTo = Time.Current + ezOffset;
                samples.TruncateTo(rewoundTo);
                kernel.RewindTo(rewoundTo);
                lastAdvancedTo = rewoundTo;
            }

            if (inputManager == null)
                return;

            double time = Time.Current + ezOffset;

            if (time < lastAdvancedTo)
            {
                samples.TruncateTo(time);
                kernel.RewindTo(time);
                lastAdvancedTo = time;
            }

            Vector2 position = playfield.ToLocalSpace(inputManager.CurrentState.Mouse.Position);

            samples.Record(time, position, isPressed(OsuAction.LeftButton), isPressed(OsuAction.RightButton));

            syncSpinners(time);

            kernel.AdvanceTo(time);
            lastAdvancedTo = time;
        }

        /// <summary>
        /// 实机按下：Drawable 的输入分发已经选定目标（悬停 / 叠堆遮挡 / 输入消费都在 UI 层完成），
        /// 这里只把按键时刻解析成判定时钟后交给内核。与回放路径共用同一套窗口与顺序判定。
        /// </summary>
        public void TryPress(DrawableOsuHitObject drawable, OsuAction action)
        {
            if (kernel == null)
                return;

            inputManager ??= GetContainingInputManager() as OsuInputManager;

            if (inputManager == null)
                return;

            double rate = (Clock as IGameplayClock)?.Rate ?? 1;
            double time = Time.Current + ezOffset + subFrameCorrection(rate);
            Vector2 position = playfield.ToLocalSpace(inputManager.CurrentState.Mouse.Position);

            samples.Record(time, position, isPressed(OsuAction.LeftButton), isPressed(OsuAction.RightButton));

            if (time < lastAdvancedTo)
            {
                samples.TruncateTo(time);
                kernel.RewindTo(time);
                lastAdvancedTo = time;
            }

            kernel.AdvanceBefore(time);
            kernel.ApplyTargetPress(drawable.HitObject, time, position, action);
        }

        private bool isPressed(OsuAction action)
        {
            if (inputManager == null)
                return false;

            foreach (var pressed in inputManager.PressedActions)
            {
                if (pressed == action)
                    return true;
            }

            return false;
        }

        private double subFrameCorrection(double rate)
        {
            if (inputManager == null)
                return 0;

            return EzSubFrameCorrection.GetCorrectionMs(InputManager.EzSubFrameTimestamp, rate);
        }

        private void syncSpinners(double time)
        {
            // 直接遍历当前场上的顶层 drawable：缓存的映射可能还没建立（首个判定之前），
            // 转盘否则永远不会向内核送旋转量。
            foreach (var drawable in playfield.HitObjectContainer.Objects)
            {
                if (drawable is not DrawableSpinner spinner)
                    continue;

                // 池化 / 尚未完成加载的 drawable 没有 tracker，跳过直到其可用。
                if (!spinner.IsLoaded || spinner.HitObject == null || spinner.RotationTracker == null || spinner.Result == null)
                    continue;

                if (spinner.Result.HasResult)
                    continue;

                kernel!.TrackSpinner(spinner.HitObject, spinner.Result.History, spinner.RotationTracker.Tracking, time);
            }
        }

        private void onJudged(OsuJudgementEvent judgement)
        {
            DrawableHitObject? drawable = resolve(judgement.Target);

            if (drawable?.Result.HasResult == false && drawable is DrawableOsuHitObject osuDrawable)
                osuDrawable.ApplyKernelResult(judgement);
        }

        private DrawableHitObject? lookup(HitObject target)
            => drawableMap.TryGetValue(target, out var drawable) && ReferenceEquals(drawable.HitObject, target) ? drawable : null;

        private DrawableHitObject? resolve(HitObject target)
        {
            DrawableHitObject? drawable = lookup(target);

            if (drawable != null)
                return drawable;

            rebuildDrawableMap();

            return lookup(target);
        }

        private void rebuildDrawableMap()
        {
            drawableMap.Clear();

            foreach (var drawable in playfield.HitObjectContainer.Objects)
                indexDrawable(drawable);
        }

        private void indexDrawable(DrawableHitObject drawable)
        {
            if (drawable.HitObject != null)
                drawableMap[drawable.HitObject] = drawable;

            foreach (var nested in drawable.NestedHitObjects)
                indexDrawable(nested);
        }

        private void resetKernel()
        {
            samples.Reset();
            samples.IsRewinding = false;
            drawableMap.Clear();
            lastAdvancedTo = double.NegativeInfinity;

            if (kernel != null)
                kernel.Judged -= onJudged;

            kernel = new OsuJudgementKernel(beatmap, createSettings(), samples)
            {
                LiveArbitration = arbitrate,
            };
            kernel.Judged += onJudged;
        }

        /// <summary>
        /// 把内核的按下仲裁交回 playfield 的 <see cref="IHitPolicy"/>：策略仍可替换、仍负责 shake / ignore 反馈，
        /// 具体顺序判断在策略内部退化到内核查询。
        /// </summary>
        private ClickAction arbitrate(HitObject target, double time, HitResult result)
        {
            DrawableHitObject? drawable = resolve(target);

            if (drawable == null)
                return ClickAction.Hit;

            ClickAction action = playfield.HitPolicy.CheckHittable(drawable, time, result);

            if (action == ClickAction.Shake && drawable is DrawableOsuHitObject osuDrawable)
                osuDrawable.Shake();

            return action;
        }

        private OsuJudgementKernelSettings createSettings()
        {
            bool useLegacy = OsuStableClassic.IsActive(scoreProcessor.JudgeMode)
                             || scoreProcessor.Mods.Value.Any(m => m is OsuModClassic classic && classic.ClassicNoteLock.Value);

            double range = OsuHitWindows.MISS_WINDOW - (scoreProcessor.Mods.Value.Any(m => m is OsuModAutopilot) ? 200 : 0);

            return new OsuJudgementKernelSettings
            {
                UseLegacyNoteLock = useLegacy,
                HittableRange = range,
                AllowMultiJudgementPerPress = scoreProcessor.AllowMultiJudgementPerPress,
            };
        }
    }
}
