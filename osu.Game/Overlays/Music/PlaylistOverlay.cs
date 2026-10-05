// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osuTK.Graphics;
using Realms;

namespace osu.Game.Overlays.Music
{
    [Cached]
    public partial class PlaylistOverlay : VisibilityContainer
    {
        public Bindable<Live<BeatmapSetInfo>?> SelectedSet = new Bindable<Live<BeatmapSetInfo>?>();

        private const float transition_duration = 600;
        private const float filter_area_height = 60;

        public const float PLAYLIST_HEIGHT = 510;

        private readonly BindableList<Live<BeatmapSetInfo>> beatmapSets = new BindableList<Live<BeatmapSetInfo>>();
        private readonly BindableList<Live<BeatmapSetInfo>> filteredSets = new BindableList<Live<BeatmapSetInfo>>();

        private readonly Bindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        private FilterCriteria currentCriteria = new FilterCriteria();

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IDisposable? beatmapSubscription;

        private FilterControl filter = null!;
        private Playlist list = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, Bindable<WorkingBeatmap> beatmap)
        {
            this.beatmap.BindTo(beatmap);

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = 5,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(40),
                        Radius = 5,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colours.Gray3,
                            RelativeSizeAxes = Axes.Both,
                        },
                        list = new Playlist
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = filter_area_height, Bottom = 10, Right = 10 },
                        },
                        filter = new FilterControl
                        {
                            RelativeSizeAxes = Axes.X,
                            Padding = new MarginPadding(10),
                            FilterChanged = applyFilter,
                        },
                    },
                },
            };

            filter.Search.OnCommit += (_, _) =>
            {
                var first = filteredSets.FirstOrDefault();
                if (first == null)
                    return;

                first.PerformRead(set =>
                {
                    var toSelect = set.Beatmaps.FirstOrDefault();
                    if (toSelect == null)
                        return;

                    beatmap.Value = beatmaps.GetWorkingBeatmap(toSelect);
                    beatmap.Value.Track.Restart();
                });
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmapSubscription = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected), beatmapsChanged);

            list.RowData.BindTo(filteredSets);
            beatmap.BindValueChanged(working => SelectedSet.Value = working.NewValue.BeatmapSetInfo.ToLive(realm), true);
        }

        private void applyFilter(FilterCriteria criteria)
        {
            currentCriteria = criteria;
            rebuildFilteredList();
        }

        private void rebuildFilteredList()
        {
            string query = currentCriteria.SearchText.Trim();

            filteredSets.Clear();

            if (string.IsNullOrEmpty(query))
            {
                filteredSets.AddRange(beatmapSets);
                return;
            }

            filteredSets.AddRange(beatmapSets.Where(s => setMatches(s, query)));
        }

        private static bool setMatches(Live<BeatmapSetInfo> liveSet, string query)
        {
            return liveSet.PerformRead(set =>
            {
                var metadata = set.Metadata;
                return contains(metadata.Title, query)
                       || contains(metadata.TitleUnicode, query)
                       || contains(metadata.Artist, query)
                       || contains(metadata.ArtistUnicode, query)
                       || contains(metadata.Author.Username, query)
                       || set.Beatmaps.Any(b => contains(b.DifficultyName, query));
            });
        }

        private static bool contains(string? source, string query) =>
            !string.IsNullOrEmpty(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);

        private void beatmapsChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes)
        {
            if (changes == null)
            {
                beatmapSets.Clear();
                beatmapSets.AddRange(sender.Select(b => b.ToLive(realm)));
                rebuildFilteredList();
                return;
            }

            foreach (int i in changes.InsertedIndices)
                beatmapSets.Insert(i, sender[i].ToLive(realm));

            foreach (int i in changes.DeletedIndices.OrderDescending())
                beatmapSets.RemoveAt(i);

            rebuildFilteredList();
        }

        protected override void PopIn()
        {
            filter.Search.HoldFocus = true;
            Schedule(() => filter.Search.TakeFocus());

            this.ResizeHeightTo(RelativeSizeAxes.HasFlag(Axes.Y) ? 1f : PLAYLIST_HEIGHT, transition_duration, Easing.OutQuint);
            this.FadeIn(transition_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            filter.Search.HoldFocus = false;

            this.ResizeHeightTo(0, transition_duration, Easing.OutQuint);
            this.FadeOut(transition_duration);
        }

        public void ItemSelected(Live<BeatmapSetInfo> beatmapSet)
        {
            beatmapSet.PerformRead(set =>
            {
                if (set.Equals(beatmap.Value?.BeatmapSetInfo))
                {
                    beatmap.Value?.Track.Seek(0);
                    return;
                }

                beatmap.Value = beatmaps.GetWorkingBeatmap(set.Beatmaps.First());
                beatmap.Value.Track.Restart();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            beatmapSubscription?.Dispose();
        }
    }
}
