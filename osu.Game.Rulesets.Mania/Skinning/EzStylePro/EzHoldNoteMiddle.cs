// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : CompositeDrawable, IHoldNoteBody
    {
        // private EzHoldNoteHittingLayer hittingLayer = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHoldNoteMiddle()
        {
            FillMode = FillMode.Stretch;
            RelativeSizeAxes = Axes.Both;
            // Anchor = Anchor.BottomCentre;
            // Origin = Anchor.BottomCentre;
            // Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(DrawableHitObject? drawableObject)
        {
            // hittingLayer = new EzHoldNoteHittingLayer();

            // if (drawableObject != null)
            // {
            //     var holdNote = (DrawableHoldNote)drawableObject;
            //
            //     // AccentColour.BindTo(holdNote.AccentColour);
            //     hittingLayer.AccentColour.BindTo(holdNote.AccentColour);
            //     ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNote.IsHolding);
            // }
        }

        // protected override void Update()
        // {
        //     base.Update();
        //     Height = DrawWidth;
        // }

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

        protected virtual string ColorPrefix => "blue";
        protected virtual string ComponentSuffix => "longnote/middle";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        private void loadAnimation()
        {
            ClearInternal();
            var animation = factory.CreateAnimation(ComponentName);

            if (animation is Container container && container.Count == 0)
            {
                string backupComponentName = $"{ColorPrefix}note";
                var backupAnimation = factory.CreateAnimation(backupComponentName);

                var cropped = new Container
                {
                    Masking = true,
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new[]
                    {
                        // new Box
                        // {
                        //     RelativeSizeAxes = Axes.Both,
                        //     Colour = Colour4.Yellow.Opacity(0.5f) // 添加黄色背景便于调试
                        // },
                        backupAnimation
                    }
                };

                AddInternal(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                    Child = cropped
                });
            }
            else
            {
                AddInternal(animation);
            }
        }

        public void Recycle()
        {
        }
    }
}
