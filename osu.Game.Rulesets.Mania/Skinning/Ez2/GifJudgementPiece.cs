using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class GifJudgementPiece : TextJudgementPiece, IAnimatableJudgement, ISerialisableDrawable
    {
        private TextureAnimation? gifAnimation;
        private Vector2 dragStartPosition;
        private bool isDragging;
        public bool UsesFixedAnchor { get; set; }

        public GifJudgementPiece(HitResult result)
            : base(result)
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            if (Result.IsHit())
            {
                gifAnimation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    DefaultFrameLength = 100,
                    // AutoSizeAxes = Axes.Both
                };

                string gifPath = getGifPath(Result);

                for (int i = 0; i < 26; i++) // 假设每个GIF有10帧
                {
                    gifAnimation.AddFrame(textures.Get($"{gifPath}/frame_{i}"));
                }

                AddInternal(gifAnimation);
                PlayAnimation();
            }
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

        private string getGifPath(HitResult result)
        {
            return result switch
            {
                HitResult.Miss => "osu.Game.Resources/Skins/Ez2/Miss",
                HitResult.Meh => "osu.Game.Resources/Skins/Ez2/Fail1",
                HitResult.Ok => "osu.Game.Resources/Skins/Ez2/Fail",
                HitResult.Good => "osu.Game.Resources/Skins/Ez2/Good",
                HitResult.Great => "osu.Game.Resources/Skins/Ez2/Cool",
                HitResult.Perfect => "osu.Game.Resources/Skins/Ez2/Kool",
                _ => string.Empty,
            };
        }

        protected override OsuSpriteText CreateJudgementText() => new OsuSpriteText();

        public virtual void PlayAnimation()
        {
            const float flash_speed = 60f;

            switch (Result)
            {
                case HitResult.Miss:
                    applyFadeEffect(this, new[] { Color4.Red, Color4.IndianRed }, flash_speed);
                    break;

                case HitResult.Meh:

                    applyFadeEffect(this, new[] { Color4.Purple, Color4.MediumPurple }, flash_speed);
                    break;

                case HitResult.Ok:
                    applyFadeEffect(this, new[] { Color4.ForestGreen, Color4.SeaGreen }, flash_speed);
                    break;

                case HitResult.Good:
                    applyFadeEffect(this, new[] { Color4.Green, Color4.LightGreen }, flash_speed);
                    break;

                case HitResult.Great:
                    applyFadeEffect(this, new[] { Color4.AliceBlue, Color4.LightSkyBlue }, flash_speed);
                    break;

                case HitResult.Perfect:
                    applyFadeEffect(this, new[] { Color4.LightBlue, Color4.LightGreen }, flash_speed);
                    break;
            }
        }

        private void applyFadeEffect(Drawable drawable, Color4[] colors, double flashSpeed)
        {
            var sequence = drawable.FadeColour(colors[0], flashSpeed, Easing.OutQuint);

            for (int i = 1; i < colors.Length; i++)
            {
                sequence = sequence.Then().FadeColour(colors[i], flashSpeed, Easing.OutQuint);
            }

            sequence.Loop();
        }

        public Drawable? GetAboveHitObjectsProxiedContent()
        {
            return null; // 根据需要返回适当的 Drawable
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Schedule(() => Schedule(updateAnchor));
        }

        private void updateAnchor()
        {
            // 如果锚点不是垂直中心，则根据滚动方向设置顶部或底部锚点
            if (Anchor.HasFlag(Anchor.y1))
                return;

            Anchor &= ~(Anchor.y0 | Anchor.y2);
        }
    }
}
