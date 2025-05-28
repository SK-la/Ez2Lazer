// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Screens;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNote : CompositeDrawable
    {
        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzNote()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        private EzSkinSettingsManager? ezSkinConfig;

        // private EzLocalTextureFactory factory = null!;
        // [Resolved]
        // private EzLocalTexture factory { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;

            // factory = new EzLocalTextureFactory(
            //     ezSkinConfig,
            //     new TextureStore(renderer),
            //     host.Storage
            // );
        }

        protected override void Update()
        {
            base.Update();

            if (IsSquare)
            {
                Height = DrawWidth;
            }
            else
            {
                Height = (float)(ezSkinConfig?.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value ?? DEFAULT_NON_SQUARE_HEIGHT);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ClearInternal();
            LoadAnimation();

            factory.OnTextureNameChanged += onSkinChanged;
        }

        private void onSkinChanged()
        {
            Schedule(() =>
            {
                ClearInternal();
                LoadAnimation();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            // 取消订阅，防止内存泄漏
            factory.OnTextureNameChanged -= onSkinChanged;
        }

        protected virtual string ColorPrefix => "blue";
        protected virtual string ComponentSuffix => "note";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        public virtual void LoadAnimation()
        {
            var animationContainer = factory.CreateAnimation(ComponentName);

            if (animationContainer is Container container &&
                container.Children.FirstOrDefault() is TextureAnimation animation &&
                animation.FrameCount > 0)
            {
                var texture = animation.CurrentFrame;

                if (texture != null)
                {
                    float ratio = texture.Height / (float)texture.Width;
                    IsSquare = ratio >= 0.7f;
                    AspectRatio = ratio;
                }
            }

            AddInternal(animationContainer);
        }

        protected bool IsSquare { get; private set; } = true;
        protected float AspectRatio { get; private set; } = 1.0f;
        protected const float DEFAULT_NON_SQUARE_HEIGHT = 20f;

        public float GetCurrentDrawHeight()
        {
            return DrawHeight;
        }

        public Texture? GetFinalTexture()
        {
            // 获取动画容器
            if (InternalChildren.FirstOrDefault() is TextureAnimation animation && animation.FrameCount > 0)
                return animation.CurrentFrame;

            return null;
        }
    }
}
