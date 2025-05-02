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
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
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

        private Vector2 dragStartPosition;
        private bool isDragging;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        protected HitWindows HitWindows { get; private set; } = null!;

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

        // private (HitResult result, double length)[] hitWindows = null!;

        [BackgroundDependencyLoader]
        private void load(DrawableRuleset drawableRuleset)
        {
            HitWindows = drawableRuleset.FirstAvailableHitWindows ?? HitWindows.Empty;
            // hitWindows = HitWindows.GetAllAvailableWindows().ToArray();
            // This is to allow the visual state to be correct after HUD comes visible after being hidden.
            AlwaysPresent = true;
        }

        private Sprite? fullComboSprite;
        private Sample? fullComboSound;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            gameplayClockContainer.OnSeek += Clear;

            processor.NewJudgement += processorNewJudgement;

            // 加载 FULL COMBO 贴图和音效
            fullComboSprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(1.5f),
                // Y = -20,
                Alpha = 0, // 初始隐藏
                Texture = textures.Get("Gameplay/AllCombo/ALL-COMBO2"), // 替换为你的贴图路径
            };
            AddInternal(fullComboSprite);

            fullComboSound = sampleStore.Get("Gameplay/full_combo_sound"); // 替换为你的音效路径
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

            ClearInternal(true);

            var judgementText = CreateJudgementTexture(judgement.Type);
            AddInternal(judgementText);
        }

        private void checkFullCombo()
        {
            var missCounter = judgementCountController.Counters
                                                      .FirstOrDefault(counter => counter.Types.Contains(HitResult.Miss));

            if (missCounter.ResultCount.Value == 0 && fullComboSprite != null)
            {
                // 显示 FULL COMBO 贴图
                fullComboSprite.FadeIn(200).Then().FadeOut(5000);

                // 播放音效
                fullComboSound?.Play();
            }
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

        protected TextureAnimation CreateJudgementTexture(HitResult result)
        {
            var judgementTexture = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(1.2f),
                DefaultFrameLength = 1000 / PlaybackFps.Value,
                Loop = false
            };

            string gifPath = getGifPath(result);

            for (int i = 0;; i++)
            {
                var texture = textures.Get($@"{gifPath}/frame_{i}");
                if (texture == null)
                    break;

                judgementTexture.AddFrame(texture);
            }

            PlaybackFps.BindValueChanged(fps =>
            {
                judgementTexture.DefaultFrameLength = 1000 / fps.NewValue;
            }, true);

            PlayAnimation(result, judgementTexture);

            judgementTexture.OnUpdate += _ =>
            {
                if (judgementTexture.CurrentFrameIndex == judgementTexture.FrameCount - 1)
                    judgementTexture.Expire();
            };
            return judgementTexture;
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

        private string getGifPath(HitResult hitResult)
        {
            return hitResult switch
            {
                HitResult.Miss => @"Gameplay/Ez2/score/Miss",
                HitResult.Meh => @"Gameplay/Ez2/score/Fail1",
                HitResult.Ok => @"Gameplay/Ez2/score/Fail",
                HitResult.Good => @"Gameplay/Ez2/score/Good",
                HitResult.Great => @"Gameplay/Ez2/score/Cool",
                HitResult.Perfect => @"Gameplay/Ez2/score/Kool",
                _ => @"Gameplay/Ez2/score",
            };
        }

        public virtual void PlayAnimation(HitResult hitResult, Drawable drawable)
        {
            const float flash_speed = 60f;

            switch (hitResult)
            {
                case HitResult.Miss:
                    applyFadeEffect(drawable, new[] { Color4.Red, Color4.IndianRed }, flash_speed);
                    break;

                case HitResult.Meh:
                    applyFadeEffect(drawable, new[] { Color4.Purple, Color4.MediumPurple }, flash_speed);
                    break;

                case HitResult.Ok:
                    applyFadeEffect(drawable, new[] { Color4.ForestGreen, Color4.SeaGreen }, flash_speed);
                    break;

                case HitResult.Good:
                    applyFadeEffect(drawable, new[] { Color4.Green, Color4.LightGreen }, flash_speed);
                    break;

                case HitResult.Great:
                    applyFadeEffect(drawable, new[] { Color4.AliceBlue, Color4.LightSkyBlue }, flash_speed);
                    break;

                case HitResult.Perfect:
                    applyFadeEffect(drawable, new[] { Color4.LightBlue, Color4.LightGreen }, flash_speed);
                    break;

                default:
                    return;
            }
        }

        private void applyFadeEffect(Drawable drawable, Color4[] colors, double flashSpeed)
        {
            if (!drawable.IsLoaded)
                return;

            drawable.FadeColour(colors[0], 0);
            var sequence = drawable.FadeColour(colors[0], flashSpeed, Easing.OutQuint);

            for (int i = 1; i < colors.Length; i++)
            {
                sequence = sequence.Then().FadeColour(colors[i], flashSpeed, Easing.OutQuint);
            }

            // sequence.Loop();
        }
    }
}
