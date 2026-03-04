// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Storyboards;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// 用于在难度卡底部显示标签的组件，包括用户标签、视频标签、故事版标签
    /// </summary>
    public partial class EzTagDisplay : CompositeDrawable
    {
        private const int max_visible_tags = 10;

        private readonly FillFlowContainer tagFlow;
        private BeatmapInfo? beatmapInfo;
        private Storyboard? storyboard;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        public EzTagDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = tagFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(3, 0),
            };
        }

        public void UpdateBeatmap(BeatmapInfo? beatmap)
        {
            beatmapInfo = beatmap;
            storyboard = beatmap != null ? beatmaps.GetWorkingBeatmap(beatmap).Storyboard : null;
            updateTags();
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmapInfo?.BeatmapSet == null || storyboard == null)
                return;

            int iconCount = 0;

            // 检查是否有视频（通过 Storyboard Video 层）
            bool hasVideo = storyboard.GetLayer("Video").Elements.Any(e => e is StoryboardVideo);

            if (hasVideo)
            {
                tagFlow.Add(new VideoIconPill { Scale = new Vector2(0.8f) });
                iconCount++;
            }

            // 检查是否有视觉故事版（Sprite/Animation，排除 Video 和 Sample 音效）
            bool hasStoryboard = storyboard.Layers
                                           .SelectMany(l => l.Elements)
                                           .Any(e => e is StoryboardSprite && e is not StoryboardVideo);

            if (hasStoryboard)
            {
                tagFlow.Add(new StoryboardIconPill { Scale = new Vector2(0.8f) });
                iconCount++;
            }

            // 添加用户标签（不受图标数量影响）
            var userTags = beatmapInfo.Metadata.UserTags.Take(max_visible_tags);

            foreach (string tag in userTags)
            {
                tagFlow.Add(new SimpleTag(tag));
            }

            // 如果没有任何标签，显示提示
            // if (!tagFlow.Any())
            // {
            //     tagFlow.Add(new SimpleTag("No tags") { Alpha = 0.5f });
            // }
        }

        private partial class SimpleTag : CompositeDrawable
        {
            private readonly string tagText;

            public SimpleTag(string text)
            {
                tagText = text;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                CornerRadius = 3;
                Masking = true;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                    },
                    new OsuSpriteText
                    {
                        Text = tagText,
                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content2,
                        Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                    }
                };
            }
        }
    }
}
