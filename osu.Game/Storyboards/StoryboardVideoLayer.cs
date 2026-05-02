// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Storyboards.Drawables;
using osuTK;

namespace osu.Game.Storyboards
{
    public partial class StoryboardVideoLayer : StoryboardLayer
    {
        public StoryboardVideoLayer(string name, int depth, bool masking)
            : base(name, depth, masking)
        {
        }

        public override DrawableStoryboardLayer CreateDrawable()
            => new DrawableStoryboardVideoLayer(this) { Depth = Depth, Name = Name };

        public partial class DrawableStoryboardVideoLayer : DrawableStoryboardLayer
        {
            private DrawableStoryboard? drawableStoryboard;
            private readonly bool autoVideSizeEnable;

            public DrawableStoryboardVideoLayer(StoryboardVideoLayer layer)
                : base(layer)
            {
                // for videos we want to take on the full size of the storyboard container hierarchy
                // to allow the video to fill the full available region.
                ElementContainer.RelativeSizeAxes = Axes.Both;
                ElementContainer.Size = Vector2.One;

                autoVideSizeEnable = GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.StoryboardAutoVideoSize);
            }

            // 在 LoadComplete() 缓存 DrawableStoryboard 引用（不再每帧向上找父级）。
            // Update() 里先算目标尺寸，只在 Size != targetSize 时才写入。
            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (autoVideSizeEnable)
                {
                    Drawable? current = Parent;

                    while (current != null)
                    {
                        if (current is DrawableStoryboard storyboard)
                        {
                            drawableStoryboard = storyboard;
                            break;
                        }

                        current = current.Parent;
                    }
                }
            }

            protected override void Update()
            {
                base.Update();

                if (autoVideSizeEnable)
                {
                    // When storyboard uses legacy 640x480 coordinates, it can be narrower than the actual screen.
                    // Expand the video layer to match the outer container so video can truly fill the screen.
                    Vector2 targetSize = Vector2.One;

                    if (drawableStoryboard?.Parent != null)
                    {
                        // 计算视频自适应模式下的缩放比例（以父级高度为基准，宽度按比例缩放）。
                        float baselineScale = drawableStoryboard.Parent.DrawHeight > 0 ? drawableStoryboard.Parent.DrawHeight / 480f : 1;
                        float width = drawableStoryboard.DrawWidth > 0 ? drawableStoryboard.Parent.DrawWidth / drawableStoryboard.DrawWidth : 1;
                        float height = baselineScale;

                        targetSize = new Vector2(width, height);
                    }

                    if (Size != targetSize)
                        Size = targetSize;
                }
            }
        }
    }
}
