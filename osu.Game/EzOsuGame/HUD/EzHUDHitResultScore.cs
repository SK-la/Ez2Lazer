// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Screens;
using osu.Game.EzOsuGame.WarmUp;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzHUDHitResultScore : CompositeDrawable, ISerialisableDrawable, IEzGameplayWarmUp //, IPreviewable //, IAnimatableJudgement
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_TEXT_FONT_LABEL), nameof(EzHUDStrings.HITRESULT_TEXT_FONT_DESCRIPTION))]
        public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_AUTO_MAP_HITMODE_LABEL), nameof(EzHUDStrings.HITRESULT_AUTO_MAP_HITMODE_DESCRIPTION))]
        public BindableBool AutoMapHitMode { get; } = new BindableBool();

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_HITMODE_TEMPLATE_LABEL), nameof(EzHUDStrings.HITRESULT_HITMODE_TEMPLATE_DESCRIPTION))]
        public Bindable<EzEnumHitMode> HitModeTemplate { get; } = new Bindable<EzEnumHitMode>(EzEnumHitMode.EZ2AC);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SKIP_BETTER_JUDGEMENT), nameof(EzHUDStrings.SKIP_BETTER_JUDGEMENT_DESCRIPTION))]
        public Bindable<EzEnumHitResult> SkipBetterJudgement { get; } = new Bindable<EzEnumHitResult>();

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.PLAYBACK_FPS_LABEL), nameof(EzHUDStrings.PLAYBACK_FPS_DESCRIPTION))]
        public BindableNumber<float> FPS { get; } = new BindableNumber<float>(60)
        {
            MinValue = 1,
            MaxValue = 240,
            Precision = 1f
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_ANIMATION_TEMPLATE_LABEL), nameof(EzHUDStrings.HITRESULT_ANIMATION_TEMPLATE_DESCRIPTION), SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> AnimationFrameTemplate { get; } = new Bindable<string>("{result}/frame_{0}");

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_BLENDING_LABEL), nameof(EzHUDStrings.HITRESULT_BLENDING_DESCRIPTION))]
        public Bindable<EzBlendMode> HitResultBlendModeSetting { get; } = new Bindable<EzBlendMode>(EzBlendMode.Additive);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.FULLCOMBO_EFFECT_LABEL), nameof(EzHUDStrings.FULLCOMBO_EFFECT_DESCRIPTION))]
        public BindableBool FullComboEffectEnabled { get; } = new BindableBool(true);

        private Bindable<BlendingParameters> hitResultBlending { get; } = new Bindable<BlendingParameters>(new BlendingParameters
        {
            // 2. 加法混合（发光效果）
            Source = BlendingType.SrcAlpha,
            Destination = BlendingType.One
        });

        public enum EzBlendMode
        {
            Additive,
            Alpha,
            Multiply,
        }

        // private EzComsPreviewOverlay previewOverlay = null!;
        // private IconButton previewButton = null!;

        // [SettingSource("Effect Type", "Effect Type")]
        // public Bindable<EzComEffectType> Effect { get; } = new Bindable<EzComEffectType>(EzComEffectType.Scale);
        //
        // [SettingSource("Effect Origin", "Effect Origin", SettingControlType = typeof(AnchorDropdown))]
        // public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        // {
        //     Default = Anchor.TopCentre,
        //     Value = Anchor.TopCentre
        // };

        private Drawable? hitAnimation;
        private Drawable? fullComboAnimation;
        private ISample? fullComboSound;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzResourceStore resources { get; set; } = null!;

        [Resolved]
        private EzGameplayWarmUpService warmUpService { get; set; } = null!;

        [Resolved]
        private ScoreProcessor processor { get; set; } = null!;

        [Resolved]
        private JudgementCountController judgementCountController { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        private Bindable<EzEnumGameThemeName> themeName = null!;
        private Bindable<EzEnumHitMode> maniaHitModeConfig = null!;

        public EzHUDHitResultScore()
        {
            Size = new Vector2(200, 50);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AlwaysPresent = true;

            // 在 BDL（而不是 LoadComplete）里登记：PlayerLoader 门控是在 Player 达到 `Ready` 时取快照的，
            // 而 LoadComplete 跑在 `Ready` 之后，那时登记就已经晚了一帧、会被漏掉。
            warmUpService.Register(this);

            themeName = ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName);
            themeName.BindValueChanged(e =>
            {
                ThemeName.Value = e.NewValue;
            });

            ThemeName.BindValueChanged(_ =>
            {
                ClearInternal();
                hitAnimation?.Invalidate();
            }, true);

            // HitMode 模板：自动模式跟随 Ez 全局 HitMode 配置；手动模式使用下拉栏的固定值。
            maniaHitModeConfig = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);

            AutoMapHitMode.BindValueChanged(e =>
            {
                // 自动映射开启时锁定手动下拉栏（灰显），关闭时解锁。
                HitModeTemplate.Disabled = e.NewValue;
                invalidateCurrentAnimation();
            }, true);

            HitModeTemplate.BindValueChanged(_ =>
            {
                if (!AutoMapHitMode.Value)
                    invalidateCurrentAnimation();
            });

            maniaHitModeConfig.BindValueChanged(_ =>
            {
                if (AutoMapHitMode.Value)
                    invalidateCurrentAnimation();
            });

            HitResultBlendModeSetting.BindValueChanged(mode =>
            {
                hitResultBlending.Value = mode.NewValue switch
                {
                    EzBlendMode.Alpha => new BlendingParameters
                    {
                        Source = BlendingType.SrcAlpha,
                        Destination = BlendingType.OneMinusSrcAlpha
                    },
                    EzBlendMode.Multiply => new BlendingParameters
                    {
                        Source = BlendingType.DstColor,
                        Destination = BlendingType.Zero
                    },
                    _ => new BlendingParameters
                    {
                        Source = BlendingType.SrcAlpha,
                        Destination = BlendingType.One
                    }
                };
            }, true);

            AccentColour.BindValueChanged(_ => Colour = AccentColour.Value, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek += Clear;

            processor.NewJudgement += processorNewJudgement;
        }

        private void processorNewJudgement(JudgementResult j)
        {
            Schedule(() =>
            {
                OnNewJudgement(j);

                // 没有对比过.JudgedHits和.HighestCombo.MaxValue
                if (processor.HighestCombo.MaxValue == processor.MaximumCombo)
                    checkFullCombo();
            });
        }

        protected void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || judgement.HitObject.HitWindows == null)
                return;

            var hitResult = judgement.Type;

            if (!hitResult.IsBasic())
                return;

            if (hitResult.GetIndexForOrderedDisplay() < SkipBetterJudgement.Value.GetIndexForOrderedDisplay())
                return;

            // 清除内部元素前先结束所有变换
            hitAnimation?.FinishTransforms();

            ClearInternal();
            hitAnimation = null;

            var judgementText = CreateJudgementTexture(judgement.Type);
            AddInternal(judgementText);
        }

        protected Drawable CreateJudgementTexture(HitResult result)
        {
            double frameLength = 1000.0 / FPS.Value;

            hitAnimation = probeJudgementDrawable(result, ThemeName.Value.ToString(),
                AnimationFrameTemplate.Value?.Trim() ?? "{result}/frame_{0}", frameLength, resolveActiveTemplate());

            if (hitAnimation != null)
            {
                configureJudgementDrawable(result, hitAnimation, frameLength);
                return hitAnimation;
            }

            // 所有尝试都失败，返回空动画（跳过显示）
            return new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Loop = false,
                Alpha = 0 // 完全透明，不显示
            };
        }

        /// <summary>
        /// 判定纹理的探测：原名 / 小写 / 大写变体各试两条加载路径，全失败再按同序试回退名，命中即停。
        /// </summary>
        /// <remarks>
        /// 进局（<see cref="CreateJudgementTexture"/>）与进图预热共用这一份，避免两套探测逻辑漂移——
        /// 漂移会让预热解码出进局用不到的帧、或漏掉进局真正要用的那一条。
        /// 这里刻意不读写组件字段、不做 configure / 播放，所以预热侧可以安全调用后直接 Dispose。
        /// </remarks>
        private Drawable? probeJudgementDrawable(HitResult result, string theme, string template, double frameLength, EzEnumHitMode activeTemplate)
        {
            string baseDir = $@"GameTheme/{theme}/judgement/";

            return probeJudgementVariants(baseDir, EzHitResultNameTemplate.GetResourceName(activeTemplate, result), template, frameLength)
                   ?? probeJudgementVariants(baseDir, EzHitResultNameTemplate.GetFallbackResourceName(activeTemplate, result), template, frameLength);
        }

        /// <summary>
        /// 单个资源名：原名 / 小写 / 大写依次尝试，每个先 <c>GetAnimation</c> 再 <c>GetAnimationFromTemplate</c>，命中即停。
        /// </summary>
        private Drawable? probeJudgementVariants(string baseDir, string resultName, string template, double frameLength)
        {
            if (string.IsNullOrEmpty(resultName))
                return null;

            // 尝试多种大小写变体以处理文件名大小写不确定性
            string[] variants = { resultName, resultName.ToLowerInvariant(), resultName.ToUpperInvariant() };

            foreach (string variant in variants)
            {
                if (string.IsNullOrEmpty(variant))
                    continue;

                Drawable? loaded = resources.GetAnimation(
                    $@"{baseDir}{variant}",
                    animatable: true,
                    looping: false,
                    startAtCurrentTime: true,
                    frameLength: frameLength);

                if (loaded != null)
                    return loaded;

                loaded = resources.GetAnimationFromTemplate(
                    baseDir,
                    variant,
                    template,
                    looping: false,
                    startAtCurrentTime: true,
                    frameLength: frameLength);

                if (loaded != null)
                    return loaded;
            }

            return null;
        }

        private void configureJudgementDrawable(HitResult result, Drawable drawable, double frameLength)
        {
            if (drawable is Sprite)
            {
                drawable.Anchor = Anchor.Centre;
                drawable.Origin = Anchor.Centre;
                drawable.Scale = new Vector2(0.5f);
                drawable.Alpha = 0;
                drawable.Blending = hitResultBlending.Value;

                Schedule(() => PlayAnimation(result, drawable));
            }
            else if (drawable is TextureAnimation animation)
            {
                animation.Anchor = Anchor.Centre;
                animation.Origin = Anchor.Centre;
                animation.Scale = new Vector2(1.2f);
                animation.Loop = false;
                animation.DefaultFrameLength = frameLength;

                PlayAnimationGif(result, animation);

                animation.OnUpdate += _ =>
                {
                    if (animation.CurrentFrameIndex == animation.FrameCount - 1)
                        animation.Expire();
                };
            }
        }

        /// <summary>
        /// 计算当前生效的命名模板：自动映射开启时跟随 Ez 全局 HitMode 配置，关闭时使用手动下拉栏选择。
        /// </summary>
        private EzEnumHitMode resolveActiveTemplate()
            => AutoMapHitMode.Value ? maniaHitModeConfig.Value : HitModeTemplate.Value;

        #region 自述预热

        /// <summary>可能出现的判定结果；预热只针对这些做帧探测。</summary>
        private static readonly HitResult[] warm_up_results =
        {
            HitResult.Perfect,
            HitResult.Great,
            HitResult.Good,
            HitResult.Ok,
            HitResult.Meh,
            HitResult.Miss,
            HitResult.Poor,
        };

        /// <summary>
        /// 把 <c>GameTheme/{theme}/judgement/*</c> 与 <c>FullCombo/full-combo</c> 的帧解码提前到 PlayerLoader 期间。
        /// </summary>
        /// <remarks>
        /// 这些资源原先只在**首次**判定/首个满连时才被解码，表现为进局后第一次判定 update/draw 各掉一帧。
        /// 这里按当前 <see cref="ThemeName"/> / <see cref="AnimationFrameTemplate"/> / 生效的 HitMode 模板
        /// 复刻 <see cref="CreateJudgementTexture"/> 的探测路径（含回退名），在线程池里完成解码。
        /// </remarks>
        public Task WarmUpAsync(CancellationToken cancellationToken)
        {
            // 与 CreateJudgementTexture 取同一来源：那是组件自己的 ThemeName 设置，不一定是全局配置。
            string theme = ThemeName.Value.ToString();
            string template = AnimationFrameTemplate.Value?.Trim() ?? "{result}/frame_{0}";
            double frameLength = 1000.0 / FPS.Value;
            EzEnumHitMode activeTemplate = resolveActiveTemplate();

            return Task.Run(() => warmUpJudgementResources(theme, template, frameLength, activeTemplate, cancellationToken), cancellationToken);
        }

        private void warmUpJudgementResources(string theme, string template, double frameLength, EzEnumHitMode activeTemplate, CancellationToken cancellationToken)
        {
            foreach (HitResult result in warm_up_results)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                // 与进局走同一份探测（命中即停）：解出来的必然是进局会命中的那一条，
                // 既不会多探后面那些永远用不到的变体 / 回退名，也不会漏掉真正要用的。
                probeJudgementDrawable(result, theme, template, frameLength, activeTemplate)?.Dispose();
            }

            // checkFullCombo 直接取 FullCombo/full-combo（不带 GameTheme 前缀）。
            resources.GetAnimation(@"FullCombo/full-combo", looping: false)?.Dispose();
        }

        #endregion

        private void invalidateCurrentAnimation()
        {
            hitAnimation?.FinishTransforms();
            ClearInternal();
            hitAnimation = null;
        }

        private void checkFullCombo()
        {
            if (!FullComboEffectEnabled.Value)
            {
                fullComboAnimation?.Expire();
                return;
            }

            var missCounter = judgementCountController.Counters
                                                      .FirstOrDefault(counter => counter.Types.Contains(HitResult.Miss));

            if (missCounter.ResultCount.Value == 0)
            {
                fullComboAnimation = resources.GetAnimation(@"FullCombo/full-combo", looping: false) ?? Empty();
                fullComboAnimation.Anchor = Anchor.Centre;
                fullComboAnimation.Origin = Anchor.Centre;
                // fullComboAnimation.Scale = new Vector2(1.5f);
                // fullComboAnimation.Alpha = 1;

                AddInternal(fullComboAnimation);
                fullComboAnimation.FadeIn(50).Then().FadeOut(3000);

                fullComboSound = resources.GetSample(@"FullCombo/full-combo-sound");
                fullComboSound?.Play();
            }
            else
            {
                fullComboAnimation?.Expire();
            }
        }

        public virtual void PlayAnimationGif(HitResult hitResult, Drawable? drawable)
        {
            // 防止空引用异常
            if (drawable == null)
                return;

            const float flash_speed = 60f;
            applyFadeEffect(hitResult, drawable, flash_speed);
        }

        private void applyFadeEffect(HitResult hitResult, Drawable? drawable, double flashSpeed)
        {
            // 防止空引用异常
            if (drawable == null || !drawable.IsLoaded)
                return;

            var colors = hitResult switch
            {
                HitResult.Poor => new[] { Color4.Purple, Color4.MediumPurple },
                HitResult.Miss => new[] { Color4.Red, Color4.IndianRed },
                HitResult.Meh => new[] { Color4.Purple, Color4.MediumPurple },
                HitResult.Ok => new[] { Color4.ForestGreen, Color4.SeaGreen },
                HitResult.Good => new[] { Color4.Green, Color4.LightGreen },
                HitResult.Great => new[] { Color4.AliceBlue, Color4.LightSkyBlue },
                HitResult.Perfect => new[] { Color4.LightBlue, Color4.LightGreen },
                _ => new[] { Color4.White }
            };

            if (drawable is TextureAnimation)
            {
                drawable.FadeColour(colors[0], 0);
                var sequence = drawable.FadeColour(colors[0], flashSpeed, Easing.OutQuint);

                for (int i = 1; i < colors.Length; i++) sequence = sequence.Then().FadeColour(colors[i], flashSpeed, Easing.OutQuint);
            }
            else
            {
                // // 保持原有透明度，只将颜色调整为 20% 强度
                // var fadedColors = colors.Select(c => new Color4(
                //     c.R * 0.5f + 0.5f, // 将颜色与白色混合，保持 20% 的原色
                //     c.G * 0.5f + 0.5f,
                //     c.B * 0.5f + 0.5f,
                //     1f)).ToArray();
                float[] weakerAlphas = new[] { 1f, 0.8f };

                drawable.FadeTo(weakerAlphas[0], 0);
                var sequence = drawable.FadeTo(weakerAlphas[0], flashSpeed, Easing.OutQuint);

                for (int i = 1; i < weakerAlphas.Length; i++) sequence = sequence.Then().FadeTo(weakerAlphas[i], flashSpeed, Easing.OutQuint);
            }
        }

        public virtual void PlayAnimation(HitResult hitResult, Drawable? drawable)
        {
            // 防止空引用异常
            if (drawable == null) return;

            double flashSpeed = FPS.Value * 2;
            applyFadeEffect(hitResult, drawable, flashSpeed);

            switch (hitResult)
            {
                case HitResult.Perfect:
                    // 中心直接绘制最大状态，向上移动并拉长压扁消失
                    applyEzStyleEffect(drawable, new Vector2(1.25f), 20);
                    break;

                case HitResult.Great:
                    // 中心绘制，稍微放大后拉长压扁消失
                    applyEzStyleEffect(drawable, new Vector2(1.1f));
                    break;

                case HitResult.Good:
                    // 中心小状态，向上放大并移动后拉长压扁消失
                    applyEzStyleEffect(drawable, new Vector2(1f));
                    break;

                case HitResult.Ok:
                case HitResult.Meh:
                    // 中心小状态，放大并向下移动后拉长压扁消失
                    applyEzStyleEffect(drawable, new Vector2(1f));
                    break;

                case HitResult.Poor:
                case HitResult.Miss:
                    // 中心小状态，放大后快速消失
                    applyEzStyleEffect(drawable, new Vector2(1f));
                    break;

                default:
                    applyEzStyleEffect(drawable, new Vector2(1.2f));
                    break;
            }
        }

        private void applyEzStyleEffect(Drawable drawable, Vector2 scaleUp, float moveDistance = 0)
        {
            // 先结束之前的所有变换
            drawable.FinishTransforms();

            var finalScale = new Vector2(1.5f, 0.05f);
            const double scale_phase_duration = 125; // 缩放
            const double transform_phase_duration = 150; // 变形动画总时间

            const double overlap_time = 5;

            // 按分配变形动画时间
            const double second_phase_duration = transform_phase_duration * 0.7;
            const double third_phase_duration = transform_phase_duration * 0.3;

            // 计算第二步和第三步的开始时间
            const double second_phase_start = scale_phase_duration - overlap_time;
            const double third_phase_start = second_phase_start + second_phase_duration - overlap_time;

            // 计算第二步的中间缩放值（完成70%的变形）
            var midScale = new Vector2(
                1.0f + ((finalScale.X - 1.0f) * 0.7f),
                1.0f - ((1.0f - finalScale.Y) * 0.7f)
            );

            // 重置状态
            drawable.Alpha = 1;
            drawable.Scale = scaleUp;
            drawable.Position = Vector2.Zero;

            drawable
                .Delay(2)
                // 第一步：放大动画，同时执行位移
                .ScaleTo(new Vector2(1.0f), scale_phase_duration, Easing.OutQuint)
                .MoveTo(new Vector2(0, -moveDistance), scale_phase_duration, Easing.OutQuint);

            using (drawable.BeginDelayedSequence(second_phase_start))
            {
                drawable
                    // 第二步：完成70%的扁平化变形
                    .TransformTo(nameof(Scale), midScale, second_phase_duration, Easing.InQuint);
            }

            using (drawable.BeginDelayedSequence(third_phase_start))
            {
                drawable
                    // 第三步：完成剩余30%变形并淡出
                    .TransformTo(nameof(Scale), finalScale, third_phase_duration, Easing.InQuint)
                    .FadeOut(third_phase_duration - 5, Easing.InQuint);
            }
        }

        protected virtual void Clear()
        {
            FinishTransforms(true);

            ClearInternal();
        }

        protected override void Dispose(bool isDisposing)
        {
            processor.NewJudgement -= processorNewJudgement;

            if (isDisposing)
                warmUpService.Unregister(this);

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= Clear;

            base.Dispose(isDisposing);
        }
    }
}
