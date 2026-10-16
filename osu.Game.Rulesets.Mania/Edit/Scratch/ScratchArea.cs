// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Rulesets.Mania.Edit.Scratch
{
    /// <summary>
    /// Renders the background (scratch) tracks of a <see cref="ScratchTrackSet"/>.
    /// </summary>
    /// <remarks>
    /// A placeholder track is shown while the set is empty, so the background area stays visible.
    /// </remarks>
    public partial class ScratchArea : CompositeDrawable
    {
        private const float placeholder_width = 24;

        private readonly ScratchTrackSet trackSet;

        private FillFlowContainer<ScratchTrackColumn> columnFlow = null!;
        private Drawable placeholder = null!;

        public ScratchArea(ScratchTrackSet trackSet)
        {
            this.trackSet = trackSet;

            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                columnFlow = new FillFlowContainer<ScratchTrackColumn>
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                },
                placeholder = createPlaceholder(),
            };

            trackSet.Tracks.BindCollectionChanged(onTracksChanged, true);
        }

        private void onTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems!.Count; i++)
                        columnFlow.Insert(e.NewStartingIndex + i, new ScratchTrackColumn((ScratchTrack)e.NewItems[i]!));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < e.OldItems!.Count; i++)
                        columnFlow.Remove(columnFlow[e.OldStartingIndex], true);
                    break;

                default:
                    columnFlow.Clear();

                    foreach (var track in trackSet.Tracks)
                        columnFlow.Add(new ScratchTrackColumn(track));

                    break;
            }

            placeholder.Alpha = trackSet.Tracks.Count == 0 ? 1 : 0;
        }

        private static Drawable createPlaceholder() => new Container
        {
            RelativeSizeAxes = Axes.Y,
            Width = placeholder_width,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.35f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Colour = Colour4.White.Opacity(0.15f),
                },
            },
        };
    }
}
