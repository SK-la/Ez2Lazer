// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : CompositeDrawable
    {
        protected virtual string ColorPrefix => "blue";
        protected virtual string ComponentSuffix => "longnote/tail";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ClearInternal();
            loadAnimation();

            factory.OnTextureNameChanged += onSkinChanged;
        }

        private void onSkinChanged()
        {
            Schedule(() =>
            {
                ClearInternal();
                loadAnimation();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            // 取消订阅，防止内存泄漏
            factory.OnTextureNameChanged -= onSkinChanged;
        }

        private void loadAnimation()
        {
            var animationContainer = factory.CreateAnimation(ComponentName);

            AddInternal(animationContainer);
        }
    }
}
