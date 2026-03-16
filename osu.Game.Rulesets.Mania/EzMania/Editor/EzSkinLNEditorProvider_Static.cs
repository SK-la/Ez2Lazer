// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class EzSkinLNEditorProvider
    {
        private Drawable createStaticPartImpl(ISkin skin)
        {
            var transformedSkin = createTransformedSkin(skin);

            return new SkinProvidingContainer(transformedSkin)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(15, 0),
                    Padding = new MarginPadding(10),
                    Children = new[]
                    {
                        createColumn(skin, useTransformed: false, "Before"),
                        createColumn(skin, useTransformed: true, "After"),
                    }
                }
            };
        }

        private SkinProvidingContainer createColumn(ISkin skin, bool useTransformed, string label)
        {
            var skinToProvide = useTransformed ? createTransformedSkin(skin) : skin;
            var hold = new HoldNote { StartTime = 0, Duration = 500, Column = 0 };
            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return new SkinProvidingContainer(skinToProvide)
            {
                RelativeSizeAxes = Axes.Y,
                Width = preview_column_width + 10,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new PreviewDependencyContainer(preview_key_count, 0, ManiaAction.Key1)
                        {
                            Child = new EzNoteContainer(ScrollingDirection.Down, label)
                            {
                                Child = new DrawableHoldNote(hold)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                }
                            }
                        },
                    }
                }
            };
        }

        // Local NoteContainer replica to match TestSceneNotes layout and behaviour for previews.
        // Kept minimal and internal to this provider file to avoid referencing test project types.
        internal partial class EzNoteContainer : Container
        {
            private readonly Container content;
            protected override Container<Drawable> Content => content;
            private readonly ScrollingDirection direction;

            public EzNoteContainer(ScrollingDirection direction, string label)
            {
                this.direction = direction;

                RelativeSizeAxes = Axes.Both;

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Y = 100,
                    Spacing = new Vector2(0, 10),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.None,
                            Width = preview_column_width,
                            Height = 500,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Colour4.Black.Opacity(0.5f),
                                    Width = 1.2f,
                                },
                                content = new Container
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both
                                }
                            }
                        },
                        new OsuSpriteText
                        {
                            Text = label,
                            Colour = Colour4.White,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold),
                        },
                    }
                };
            }

            protected override void Update()
            {
                base.Update();

                foreach (var obj in content.OfType<DrawableHitObject>())
                {
                    if (!(obj.HitObject is IHasDuration endTime))
                        continue;

                    foreach (var nested in obj.NestedHitObjects)
                    {
                        double finalPosition = (nested.HitObject.StartTime - obj.HitObject.StartTime) / endTime.Duration;

                        switch (direction)
                        {
                            case ScrollingDirection.Up:
                                nested.Y = (float)(finalPosition * content.DrawHeight);
                                break;

                            case ScrollingDirection.Down:
                                nested.Y = (float)(-finalPosition * content.DrawHeight);
                                break;
                        }
                    }
                }
            }
        }

        // Small container to provide the minimal dependencies required by `DrawableHoldNote` in previews.
        // Registers a `StageDefinition`, `ManiaBeatmap` and `Column` so hit-object drawables resolve correctly.
        internal sealed partial class PreviewDependencyContainer : Container
        {
            private readonly Container content;
            protected override Container<Drawable> Content => content;

            private readonly StageDefinition stageDefinition;
            private readonly IBeatmap beatmapDependency;
            private readonly Column columnDependency;
            private readonly PreviewGameplayClock gameplayClockDependency = new PreviewGameplayClock();
            private readonly ManualClock previewClock = new ManualClock();

            public PreviewDependencyContainer(int keyCount, int column, ManiaAction action)
            {
                RelativeSizeAxes = Axes.Both;
                Clock = new FramedClock(previewClock);

                stageDefinition = new StageDefinition(keyCount);
                beatmapDependency = new ManiaBeatmap(stageDefinition);
                columnDependency = new Column(column, isSpecial: false);
                columnDependency.Action.Value = action;

                InternalChildren = new Drawable[]
                {
                    // Keep a hidden live column in hierarchy so custom skins depending on Column-initialised
                    // bindables (note size/colour/skin info) have valid values.
                    new Container
                    {
                        Alpha = 0,
                        Size = Vector2.Zero,
                        Child = columnDependency,
                    },
                    content = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                columnDependency.EzNoteSizeBindable.Value = new Vector2(preview_column_width, preview_column_width);
                columnDependency.EzNoteColourBindable.Value = Colour4.White;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                var scrollingInfo = new PreviewScrollingInfo();
                dependencies.CacheAs<IScrollingInfo>(scrollingInfo);
                dependencies.CacheAs<IGameplayClock>(gameplayClockDependency);

                dependencies.Cache(stageDefinition);
                dependencies.CacheAs(beatmapDependency);
                dependencies.Cache(columnDependency);
                dependencies.CacheAs<IBindable<ManiaAction>>(columnDependency.Action);
                return dependencies;
            }

            protected override void Update()
            {
                base.Update();

                // Keep static preview at a fixed point in time so notes remain visible.
                previewClock.CurrentTime = 0;
                gameplayClockDependency.CurrentTime = 0;
            }
        }
    }
}
