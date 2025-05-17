using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComHitResultScore : CompositeDrawable, ISerialisableDrawable //, IAnimatableJudgement
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Playback FPS", "The FPS value of this Animation")]
        public BindableNumber<float> PlaybackFps { get; } = new BindableNumber<float>(60)
        {
            MinValue = 1,
            MaxValue = 240,
            Precision = 1f,
        };

        [SettingSource("HitResult Text Font", "HitResult Text Font", SettingControlType = typeof(EzEnumListSelector))]
        public Bindable<OffsetNumberName> NameDropdown { get; } = new Bindable<OffsetNumberName>((OffsetNumberName)49);

        // [SettingSource("Effect Type", "Effect Type")]
        // public Bindable<EffectType> Effect { get; } = new Bindable<EffectType>(EffectType.Scale);
        //
        // [SettingSource("Effect Origin", "Effect Origin", SettingControlType = typeof(AnchorDropdown))]
        // public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        // {
        //     Default = Anchor.TopCentre,
        //     Value = Anchor.TopCentre
        // };

        private Vector2 dragStartPosition;
        private bool isDragging;
        public Sprite? StaticSprite;
        private Container? fullComboSprite;
        private Sample? fullComboSound;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private ScoreProcessor processor { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private GameplayClockContainer gameplayClockContainer { get; set; } = null!;

        [Resolved]
        private JudgementCountController judgementCountController { get; set; } = null!;

        [Resolved]
        private ISampleStore sampleStore { get; set; } = null!;

        public EzComHitResultScore()
        {
            Size = new Vector2(200, 50);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AlwaysPresent = true;

            fullComboSprite = new Container
            {
                Child = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(1.5f),
                    Alpha = 0, // 初始隐藏
                    Texture = textures.Get("EzResources/AllCombo/ALL-COMBO2"),
                }
            };

            AddInternal(fullComboSprite);

            fullComboSound = sampleStore.Get("EzResources/AllCombo/full_combo_sound");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            gameplayClockContainer.OnSeek += Clear;

            processor.NewJudgement += processorNewJudgement;

            NameDropdown.BindValueChanged(_ =>
            {
                ClearInternal(true);

                StaticSprite?.Invalidate();
            }, true);
        }

        private void processorNewJudgement(JudgementResult j)
        {
            Schedule(() =>
            {
                OnNewJudgement(j);

                if (processor.JudgedHits == processor.MaximumCombo)
                    checkFullCombo();
            });
        }

        protected void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss) == 0)
                return;

            if (!judgement.Type.IsScorable() || judgement.Type.IsBonus())
                return;

            // 清除内部元素前先结束所有变换
            StaticSprite?.FinishTransforms();

            ClearInternal();
            StaticSprite = null;

            var judgementText = CreateJudgementTexture(judgement.Type);
            AddInternal(judgementText);
        }

        protected Drawable CreateJudgementTexture(HitResult result)
        {
            string basePath = getGifPath(result);
            var singleTexture = textures.Get($"{basePath}.png");

            if (singleTexture != null)
            {
                StaticSprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(0.5f),
                    Texture = singleTexture,
                    Alpha = 0,
                    // 设置混合模式
                    Blending = new BlendingParameters
                    {
                        // 1. 标准透明混合（最常用）
                        // Source = BlendingType.SrcAlpha,
                        // Destination = BlendingType.OneMinusSrcAlpha,

                        // 2. 加法混合（发光效果）
                        Source = BlendingType.SrcAlpha,
                        Destination = BlendingType.One,

                        // 3. 减法混合（暗色透明）
                        // Source = BlendingType.Zero,
                        // Destination = BlendingType.OneMinusSrcColor,

                        // 4. 纯色叠加（忽略黑色）
                        // Source = BlendingType.One,
                        // Destination = BlendingType.One,

                        // 5. 柔和混合
                        // Source = BlendingType.DstColor,
                        // Destination = BlendingType.One,
                    }
                };

                Schedule(() =>
                {
                    PlayAnimation(result, StaticSprite);
                });

                return StaticSprite;
            }

            // 不存在单张图片时，尝试加载动画帧
            var animation = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(1.2f),
                DefaultFrameLength = 1000 / PlaybackFps.Value,
                Loop = false
            };

            for (int i = 0;; i++)
            {
                var texture = textures.Get($@"{basePath}/frame_{i}");
                if (texture == null)
                    break;

                animation.AddFrame(texture);
            }

            PlaybackFps.BindValueChanged(fps =>
            {
                animation.DefaultFrameLength = 1000 / fps.NewValue;
            }, true);

            PlayAnimationGif(result, animation);

            animation.OnUpdate += _ =>
            {
                if (animation.CurrentFrameIndex == animation.FrameCount - 1)
                    animation.Expire();
            };

            return animation;
        }

        private string getGifPath(HitResult hitResult)
        {
            string textureNameReplace = NameDropdown.Value.ToString();
            string basePath = $@"EzResources/GameTheme/{textureNameReplace}/judgement";
            string resultName = hitResult switch
            {
                HitResult.Miss => "Miss",
                HitResult.Meh => "Fail",
                HitResult.Ok => "Fail",
                HitResult.Good => "Good",
                HitResult.Great => "Cool",
                HitResult.Perfect => "Kool",
                _ => string.Empty
            };

            return $"{basePath}/{resultName}";
        }

        private void checkFullCombo()
        {
            var missCounter = judgementCountController.Counters
                                                      .FirstOrDefault(counter => counter.Types.Contains(HitResult.Miss));

            if (missCounter.ResultCount.Value == 0 && fullComboSprite != null)
            {
                // 显示 FULL COMBO 贴图
                fullComboSprite.Alpha = 1;
                fullComboSprite.FadeIn(50).Then().FadeOut(5000);

                // 播放音效
                fullComboSound?.Play();
            }
        }

        public virtual void PlayAnimationGif(HitResult hitResult, Drawable drawable)
        {
            const float flash_speed = 60f;
            applyFadeEffect(hitResult, drawable, flash_speed);
        }

        private void applyFadeEffect(HitResult hitResult, Drawable drawable, double flashSpeed)
        {
            if (!drawable.IsLoaded)
                return;

            // 为每种判定结果定义颜色数组
            var colors = hitResult switch
            {
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

                for (int i = 1; i < colors.Length; i++)
                {
                    sequence = sequence.Then().FadeColour(colors[i], flashSpeed, Easing.OutQuint);
                }
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

                for (int i = 1; i < weakerAlphas.Length; i++)
                {
                    sequence = sequence.Then().FadeTo(weakerAlphas[i], flashSpeed, Easing.OutQuint);
                }
            }
        }

        public virtual void PlayAnimation(HitResult hitResult, Drawable drawable)
        {
            double flashSpeed = PlaybackFps.Value * 2;
            applyFadeEffect(hitResult, drawable, flashSpeed);

            switch (hitResult)
            {
                case HitResult.Perfect:
                    // 中心直接绘制最大状态，向上移动并拉长压扁消失
                    applyEzStyleEffect(drawable, new Vector2(1.2f), 15);
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
            // 固定拉长压扁比例
            var finalScale = new Vector2(1.5f, 0.05f);
            // 固定动画持续时间
            const double scale_up_duration = 150; // 放大动画
            const double scale_down_duration = 180; // 压扁动画
            const double fade_out_duration = scale_down_duration + 10; // 淡出动画

            // 重置状态
            drawable.Alpha = 1;
            // drawable.Position = Vector2.Zero;

            drawable
                // 第一步：放大动画，同时执行位移（如果有）
                .ScaleTo(scaleUp, scale_up_duration, Easing.OutQuint)
                .MoveTo(new Vector2(0, moveDistance), scale_up_duration, Easing.OutQuint)
                .Delay(scale_up_duration + 20)
                // 第二步：在放大基础上进行横向拉长和纵向压缩（使用固定比例）
                .TransformTo(nameof(Scale), new Vector2(scaleUp.X * finalScale.X, scaleUp.Y * finalScale.Y), scale_down_duration, Easing.InQuint)
                .MoveTo(new Vector2(0, -moveDistance / 10), fade_out_duration, Easing.InQuint)
                .FadeOut(fade_out_duration, Easing.InQuint);
        }

        protected virtual void Clear()
        {
            FinishTransforms(true);

            ClearInternal();
        }

        protected override void Dispose(bool isDisposing)
        {
            PlaybackFps.UnbindAll();
            processor.NewJudgement -= processorNewJudgement;
            gameplayClockContainer.OnSeek -= Clear;
            base.Dispose(isDisposing);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            dragStartPosition = e.ScreenSpaceMousePosition;
            isDragging = true;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (isDragging)
            {
                var delta = e.ScreenSpaceMousePosition - dragStartPosition;
                Position += delta;
                dragStartPosition = e.ScreenSpaceMousePosition;
            }
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            isDragging = false;
        }
    }
}
