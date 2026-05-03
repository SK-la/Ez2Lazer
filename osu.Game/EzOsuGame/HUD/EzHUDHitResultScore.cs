// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
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
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzHUDHitResultScore : CompositeDrawable, ISerialisableDrawable //, IPreviewable //, IAnimatableJudgement
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_TEXT_FONT_LABEL), nameof(EzHUDStrings.HITRESULT_TEXT_FONT_DESCRIPTION))]
        public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.PLAYBACK_FPS_LABEL), nameof(EzHUDStrings.PLAYBACK_FPS_DESCRIPTION))]
        public BindableNumber<float> FPS { get; } = new BindableNumber<float>(60)
        {
            MinValue = 1,
            MaxValue = 240,
            Precision = 1f
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ALPHA_LABEL), nameof(EzHUDStrings.ALPHA_DESCRIPTION))]
        public BindableNumber<float> AccentAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_BLENDING_LABEL), nameof(EzHUDStrings.HITRESULT_BLENDING_DESCRIPTION))]
        public Bindable<BlendingParameters> HitResultBlending { get; } = new Bindable<BlendingParameters>(new BlendingParameters
        {
            // 2. 加法混合（发光效果）
            Source = BlendingType.SrcAlpha,
            Destination = BlendingType.One
        });

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

        private Drawable? anime;
        private Container? fullComboSprite;
        private Sample? fullComboSound;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzResourceProvider textures { get; set; } = null!;

        [Resolved]
        private ScoreProcessor processor { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private GameplayClockContainer gameplayClockContainer { get; set; } = null!;

        [Resolved]
        private JudgementCountController judgementCountController { get; set; } = null!;

        [Resolved]
        private ISampleStore sampleStore { get; set; } = null!;

        private Bindable<EzEnumGameThemeName> themeName = null!;

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

            themeName = ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName);
            themeName.BindValueChanged(e =>
            {
                ThemeName.Value = e.NewValue;
            });

            ThemeName.BindValueChanged(_ =>
            {
                ClearInternal();
                anime?.Invalidate();
            }, true);

            AccentAlpha.BindValueChanged(alpha => Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Colour = AccentColour.Value, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

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
            if (!judgement.IsHit || judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss) == 0)
                return;

            if (!judgement.Type.IsScorable() || judgement.Type.IsBonus())
                return;

            if (judgement.Type == HitResult.Meh &&
                (judgement.HitObject.HitWindows?.WindowFor(HitResult.Meh)
                 == judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss)))
                return;

            // 清除内部元素前先结束所有变换
            anime?.FinishTransforms();

            ClearInternal();
            anime = null;

            var judgementText = CreateJudgementTexture(judgement.Type);
            AddInternal(judgementText);
        }

        protected Drawable CreateJudgementTexture(HitResult result)
        {
            string resultName = getHitResultToString(result);
            string name = ThemeName.Value.ToString();

            string baseDir = $@"GameTheme/{name}/judgement/";

            // 尝试多种大小写变体以处理文件名大小写不确定性
            // 由于 TextureStore 的 Get 方法区分大小写，我们尝试常见变体：小写和大写
            string[] possibleResultNames = { resultName, resultName.ToLowerInvariant(), resultName.ToUpperInvariant() };

            foreach (string rn in possibleResultNames)
            {
                string path = $@"{baseDir}{rn}";
                var singleTexture = textures.Get(path);

                if (singleTexture != null)
                {
                    anime = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(0.5f),
                        Texture = singleTexture,
                        Alpha = 0,
                        // 使用可配置的混合模式
                        Blending = HitResultBlending.Value
                    };

                    Schedule(() =>
                    {
                        PlayAnimation(result, anime);
                    });

                    return anime;
                }
            }

            // 不存在单张图片时，尝试加载动画帧
            var animation = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(1.2f),
                Loop = false
            };

            bool hasFrames = false;

            // 对于动画帧，也尝试多种大小写变体
            foreach (string rn in possibleResultNames)
            {
                string path = $@"{baseDir}{rn}";
                bool foundFrames = false;

                for (int i = 0;; i++)
                {
                    var texture = textures.Get($@"{path}/frame_{i}");
                    if (texture == null)
                        break;

                    animation.AddFrame(texture);
                    foundFrames = true;
                    hasFrames = true;
                }

                if (foundFrames)
                    break; // 如果找到帧，使用这个路径
            }

            // 只有在有帧的情况下才播放动画
            if (hasFrames)
            {
                animation.DefaultFrameLength = 1000 / FPS.Value;
                PlayAnimationGif(result, animation);

                animation.OnUpdate += _ =>
                {
                    if (animation.CurrentFrameIndex == animation.FrameCount - 1)
                        animation.Expire();
                };
            }

            return animation;
        }

        // 如果考虑拓展能力，则倾向nameof(HitResult)，并回退到这个方法
        private string getHitResultToString(HitResult hitResult)
        {
            string resultName = hitResult switch
            {
                HitResult.Poor => "Fail",
                HitResult.Miss => "Fail",
                HitResult.Meh => "Miss",
                HitResult.Ok => "",
                HitResult.Good => "Good",
                HitResult.Great => "Cool",
                HitResult.Perfect => "Kool",
                _ => string.Empty
            };

            return resultName;
        }

        private void checkFullCombo()
        {
            var missCounter = judgementCountController.Counters
                                                      .FirstOrDefault(counter => counter.Types.Contains(HitResult.Miss));

            if (missCounter.ResultCount.Value == 0)
            {
                fullComboSprite = new Container
                {
                    Child = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(1.5f),
                        Alpha = 1,
                        Texture = textures.Get(@$"Modify/FullCombo/full-combo")
                    }
                };

                AddInternal(fullComboSprite);
                fullComboSprite.FadeIn(50).Then().FadeOut(3000);

                fullComboSound = sampleStore.Get(@"Modify/FullCombo/full-combo-sound");
                fullComboSound?.Play();
            }
            else
            {
                fullComboSprite?.Expire();
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
            gameplayClockContainer.OnSeek -= Clear;
            base.Dispose(isDisposing);
        }
    }
}
