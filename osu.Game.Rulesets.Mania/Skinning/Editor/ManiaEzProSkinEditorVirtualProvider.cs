// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Editor
{
    public partial class ManiaEzProSkinEditorVirtualProvider : ISkinEditorVirtualProvider
    {
        public Drawable CreateVirtualPlayfield(ISkin skin, IBeatmap beatmap)
        {
            var transformedSkin = createTransformedSkin(skin);

            float columnWidth0 = getColumnWidth(transformedSkin, 0);
            float columnWidth1 = getColumnWidth(transformedSkin, 1);

            // 轻量级 2K：不创建 Stage/Column/ManiaPlayfield（它们会引入 Player/背景等依赖）。
            // 这里只用两列容器（等价于一个简单的 ColumnFlow<Drawable>），每列放一个循环 HoldNote。
            return new SkinProvidingContainer(transformedSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = createTwoKeyLayout(new Drawable[]
                {
                    createHoldPreview(looping: true, column: 0, maniaAction: ManiaAction.Key1, width: columnWidth0),
                    createHoldPreview(looping: true, column: 1, maniaAction: ManiaAction.Key2, width: columnWidth1),
                })
            };
        }

        public Drawable CreateCurrentSkinNoteDisplay(ISkin skin)
        {
            return createSinglePreview(skin, label: "Current");
        }

        public Drawable CreateEditedNoteDisplay(ISkin skin)
        {
            return createSinglePreview(skin, label: "Edited");
        }

        private static Drawable createSinglePreview(ISkin skin, string label)
        {
            // 中间对比区目前只做“绘制一致、标题不同”，后续编辑态再替换 skin/transformer 即可。
            var transformedSkin = createTransformedSkin(skin);

            float columnWidth0 = getColumnWidth(transformedSkin, 0);

            return new SkinProvidingContainer(transformedSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = label,
                            Colour = Colour4.White,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 220,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Colour4.Black.Opacity(0.3f),
                                },
                                createHoldPreview(looping: false, column: 0, maniaAction: ManiaAction.Key1, width: columnWidth0)
                            }
                        }
                    }
                }
            };
        }

        private static float getColumnWidth(ISkin skin, int columnIndex)
        {
            // For EzPro, column width is provided via ManiaSkinConfigurationLookup.
            // Fall back to the default mania column width if not present.
            return skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                           new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, columnIndex))
                       ?.Value ?? Column.COLUMN_WIDTH;
        }

        private static ISkin createTransformedSkin(ISkin skin)
        {
            var ruleset = new ManiaRuleset();

            // EzPro transformer 要求传入 ManiaBeatmap（会强转）。
            // 该 provider 固定服务 2K 预览，因此直接在 ruleset 内部使用固定 2K beatmap。
            var beatmap = new ManiaBeatmap(new StageDefinition(2));
            return ruleset.CreateSkinTransformer(skin, beatmap) ?? skin;
        }

        private static FillFlowContainer createTwoKeyLayout(Drawable[] columns)
        {
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6),
                Padding = new MarginPadding(10),
                Children = columns
            };
        }

        private static Container createHoldPreview(bool looping, int column, ManiaAction maniaAction)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Y,
                Height = 1,
                Width = 0.5f,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.3f),
                    },
                    new HoldNotePreview(looping, column, maniaAction)
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            };
        }

        private static Container createHoldPreview(bool looping, int column, ManiaAction maniaAction, float width)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Y,
                Height = 1,
                Width = width,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.3f),
                    },
                    new HoldNotePreview(looping, column, maniaAction)
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            };
        }

        private sealed partial class HoldNotePreview : CompositeDrawable
        {
            private const double time_range = 2000;
            private const double cycle_length = time_range;

            private readonly bool looping;
            private readonly int column;
            private readonly Bindable<ManiaAction> action;
            private readonly PreviewScrollingInfo scrollingInfo = new PreviewScrollingInfo();
            private readonly Column columnDependency;
            private readonly StageDefinition stageDefinition = new StageDefinition(2);

            private readonly StopwatchClock playbackClock = new StopwatchClock(true);
            private readonly ManualClock manualClock = new ManualClock();

            private DrawableHoldNote drawableHoldNote = null!;

            private Container judgementArea = null!;

            private double staticPreviewTime;

            public HoldNotePreview(bool looping, int column, ManiaAction maniaAction)
            {
                this.looping = looping;
                this.column = column;
                action = new Bindable<ManiaAction>(maniaAction);

                // EzPro 的 note 组件在加载时需要解析 Column / StageDefinition。
                // 这里提供最小的 2K column/stage 依赖（不进入场景树，仅用于依赖注入）。
                columnDependency = new Column(column, isSpecial: false);
                columnDependency.Action.Value = maniaAction;

                RelativeSizeAxes = Axes.Both;

                // Drive time manually to:
                // - loop previews without lifetime expiry
                // - keep centre previews completely static
                Clock = new FramedClock(manualClock);
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs<IScrollingInfo>(scrollingInfo);

                dependencies.Cache(stageDefinition);
                dependencies.Cache(columnDependency);
                dependencies.CacheAs<IBindable<ManiaAction>>(columnDependency.Action);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                scrollingInfo.Set(ScrollingDirection.Down, time_range, new ConstantScrollAlgorithm());

                var hold = new HoldNote
                {
                    StartTime = time_range,
                    Duration = 1200,
                    Column = column,
                };
                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                // Centre preview should be static and always visible.
                // Freeze time at the hitobject start time and do not animate.
                staticPreviewTime = hold.StartTime;
                manualClock.CurrentTime = looping ? 0 : staticPreviewTime;

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 20, Vertical = 10 },
                        Children = new Drawable[]
                        {
                            judgementArea = new Container
                            {
                                RelativeSizeAxes = Axes.None,
                                BorderThickness = 2,
                                BorderColour = Colour4.Green.Opacity(0.6f),
                                Masking = true,
                                Depth = 1,
                                Child = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Colour4.Green.Opacity(0.18f),
                                }
                            },
                            drawableHoldNote = new DrawableHoldNote(hold)
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        }
                    }
                };
            }

            protected override void Update()
            {
                base.Update();

                if (drawableHoldNote == null)
                    return;

                if (!looping)
                {
                    // Static centre preview: no movement, no alpha gating, no time progression.
                    manualClock.CurrentTime = staticPreviewTime;
                    drawableHoldNote.Y = 0;
                    drawableHoldNote.Alpha = 1;
                    updateJudgementArea();
                    return;
                }

                double currentTime;

                if (looping)
                    currentTime = playbackClock.CurrentTime % cycle_length;
                else
                    currentTime = staticPreviewTime;

                manualClock.CurrentTime = currentTime;

                // Scroll the whole hold note. Nested hitobjects are positioned by DrawableHoldNote itself based on IScrollingInfo.
                double finalPosition = (drawableHoldNote.HitObject.StartTime - currentTime) / scrollingInfo.TimeRange.Value;
                float scrollLength = DrawHeight;
                // Map [0..1] to [0..H] so motion is visible across full height.
                drawableHoldNote.Y = (float)((1 - finalPosition) * scrollLength);
                drawableHoldNote.Alpha = finalPosition >= 0 && finalPosition <= 1 ? 1 : 0;

                updateJudgementArea();
            }

            private void updateJudgementArea()
            {
                // 用户要求：底色 box 对齐到 holdnote head 底部与 tail 底部，宽度略大，且在 holdnote 下方。
                var headRect = drawableHoldNote.Head.DrawRectangle;
                var tailRect = drawableHoldNote.Tail.DrawRectangle;

                var holdRect = drawableHoldNote.DrawRectangle;

                float startY = headRect.Height > 0 ? headRect.Bottom : holdRect.Top;
                float endY = tailRect.Height > 0 ? tailRect.Bottom : holdRect.Bottom;

                if (endY < startY)
                    (startY, endY) = (endY, startY);

                const float extra_width = 12;

                judgementArea.X = -extra_width / 2;
                judgementArea.Y = startY;
                judgementArea.Width = drawableHoldNote.DrawWidth + extra_width;
                judgementArea.Height = Math.Max(0, endY - startY);
            }

            private sealed class PreviewScrollingInfo : IScrollingInfo
            {
                private readonly Bindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
                private readonly Bindable<double> timeRange = new Bindable<double>();
                private readonly Bindable<IScrollAlgorithm> algorithm = new Bindable<IScrollAlgorithm>();

                public IBindable<ScrollingDirection> Direction => direction;
                public IBindable<double> TimeRange => timeRange;
                public IBindable<IScrollAlgorithm> Algorithm => algorithm;

                public void Set(ScrollingDirection direction, double timeRange, IScrollAlgorithm algorithm)
                {
                    this.direction.Value = direction;
                    this.timeRange.Value = timeRange;
                    this.algorithm.Value = algorithm;
                }
            }
        }
    }
}
