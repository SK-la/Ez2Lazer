// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Changelog;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class ChangelogOverlay : OnlineOverlay<ChangelogHeader>
    {
        public readonly Bindable<APIChangelogBuild> Current = new Bindable<APIChangelogBuild>();

        private List<APIChangelogBuild> builds;

        protected List<APIUpdateStream> Streams;

        private readonly EzGitHubChangelogCatalogue ezCatalogue = new EzGitHubChangelogCatalogue();

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private readonly Bindable<Language> ezLanguage = new Bindable<Language>();

        public ChangelogOverlay()
            : base(OverlayColourScheme.Purple, false)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Build.BindTarget = Current;

            ezLanguage.BindTo(game.CurrentLanguage);
            ezLanguage.BindValueChanged(_ =>
            {
                if (Current.Value != null)
                    Current.TriggerChange();
            });

            Current.BindValueChanged(e =>
            {
                if (e.NewValue != null)
                    loadContent(CreateSingleBuildContent(e.NewValue));
                else
                    loadContent(CreateListingContent(builds));
            });
        }

        protected virtual ChangelogContent CreateListingContent(List<APIChangelogBuild> buildList) => new EzChangelogListing(buildList);

        protected virtual ChangelogContent CreateSingleBuildContent(APIChangelogBuild build)
        {
            string body = ezCatalogue.GetBody(build.DisplayVersion) ?? string.Empty;
            return new EzChangelogReleaseContent(build, body, ezLanguage);
        }

        protected override ChangelogHeader CreateHeader() => CreateChangelogHeader();

        protected virtual ChangelogHeader CreateChangelogHeader() => new EzChangelogHeader
        {
            ListingSelected = ShowListing,
        };

        /// <summary>
        /// When implemented, fetches build listing from a non-ppy source and returns <see langword="true"/>.
        /// </summary>
        protected virtual Task<bool> TryFetchListingAsync(Action<List<APIChangelogBuild>> onSuccess, Action<Exception> onFailure) =>
            fetchEzReleasesAsync(onSuccess, onFailure);

        private async Task<bool> fetchEzReleasesAsync(Action<List<APIChangelogBuild>> onSuccess, Action<Exception> onFailure)
        {
            try
            {
                var releaseBuilds = await ezCatalogue.FetchBuildsAsync(storage).ConfigureAwait(false);
                onSuccess(new List<APIChangelogBuild>(releaseBuilds));
                return true;
            }
            catch (Exception ex)
            {
                onFailure(ex);
                return true;
            }
        }

        protected override Color4 BackgroundColour => ColourProvider.Background4;

        public void ShowListing()
        {
            ezOpenedLatestBuild = true;
            Current.Value = null;
            Show();
        }

        /// <summary>
        /// Fetches and shows a specific build from a specific update stream.
        /// </summary>
        /// <param name="build">Must contain at least <see cref="APIUpdateStream.Name"/> and
        /// <see cref="APIChangelogBuild.Version"/>. If <see cref="APIUpdateStream.DisplayName"/> and
        /// <see cref="APIChangelogBuild.DisplayVersion"/> are specified, the header will instantly display them.</param>
        public void ShowBuild([NotNull] APIChangelogBuild build)
        {
            ArgumentNullException.ThrowIfNull(build);

            ezOpenedLatestBuild = true;
            Current.Value = build;
            Show();
        }

        public void ShowBuild([NotNull] string version)
        {
            ArgumentNullException.ThrowIfNull(version);

            Show();

            string[] split = version.Split('-');

            if (split.Length < 2)
                return;

            string versionPart = split[0];
            string updateStream = split[1];

            performAfterFetch(() =>
            {
                var build = builds.Find(b => b.Version == versionPart && b.UpdateStream.Name == updateStream)
                            ?? Streams.Find(s => s.Name == updateStream)?.LatestBuild;

                if (build != null)
                    ShowBuild(build);
            });
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Back:
                    if (Current.Value == null)
                    {
                        Hide();
                    }
                    else
                    {
                        Current.Value = null;
                    }

                    return true;
            }

            return false;
        }

        protected override void PopIn()
        {
            base.PopIn();

            if (initialFetchTask == null)
            {
                performAfterFetch(() =>
                {
                    if (!ezOpenedLatestBuild && Current.Value == null && builds?.Count > 0)
                    {
                        ezOpenedLatestBuild = true;
                        Current.Value = builds[0];
                    }
                    else
                        Current.TriggerChange();
                });
            }
        }

        private Task initialFetchTask;

        private bool ezOpenedLatestBuild;

        protected override void PopOut()
        {
            ezOpenedLatestBuild = false;
            base.PopOut();
        }

        private void performAfterFetch(Action action)
        {
            Debug.Assert(State.Value == Visibility.Visible);

            Schedule(() =>
            {
                fetchListing()?.ContinueWith(_ =>
                    Schedule(action), TaskContinuationOptions.OnlyOnRanToCompletion);
            });
        }

        private Task fetchListing()
        {
            if (initialFetchTask != null)
                return initialFetchTask;

            return initialFetchTask = Task.Run(async () =>
            {
                var tcs = new TaskCompletionSource<bool>();

                if (await TryFetchListingAsync(
                        buildList => Schedule(() =>
                        {
                            builds = new List<APIChangelogBuild>(buildList);
                            Streams = new List<APIUpdateStream>(EzGitHubChangelogCatalogue.CreateUpdateStreams(builds));
                            Header.Populate(Streams);
                            tcs.SetResult(true);
                        }),
                        ex => Schedule(() =>
                        {
                            initialFetchTask = null;
                            builds = new List<APIChangelogBuild>();
                            Streams = new List<APIUpdateStream>(EzGitHubChangelogCatalogue.CreateUpdateStreams(builds));
                            Header.Populate(Streams);
                            Logger.Log($"Ez changelog listing failed: {ex.Message}", level: LogLevel.Important);
                            tcs.SetResult(true);
                        })).ConfigureAwait(false))
                {
                    await tcs.Task.ConfigureAwait(false);
                    return;
                }

                var req = new GetChangelogRequest();

                req.Success += res => Schedule(() =>
                {
                    // remap streams to builds to ensure model equality
                    res.Builds.ForEach(b => b.UpdateStream = res.Streams.Find(s => s.Id == b.UpdateStream.Id));
                    res.Streams.ForEach(s => s.LatestBuild.UpdateStream = res.Streams.Find(s2 => s2.Id == s.LatestBuild.UpdateStream.Id));

                    builds = res.Builds;
                    Streams = res.Streams;

                    Header.Populate(res.Streams);

                    tcs.SetResult(true);
                });

                req.Failure += e =>
                {
                    initialFetchTask = null;
                    tcs.SetException(e);
                };

                await API.PerformAsync(req).ConfigureAwait(false);

                await tcs.Task.ConfigureAwait(false);
            });
        }

        private CancellationTokenSource loadContentCancellation;

        private void loadContent(ChangelogContent newContent)
        {
            Content.FadeTo(0.2f, 300, Easing.OutQuint);

            loadContentCancellation?.Cancel();

            LoadComponentAsync(newContent, c =>
            {
                Content.FadeIn(300, Easing.OutQuint);

                c.BuildSelected = ShowBuild;
                Child = c;
            }, (loadContentCancellation = new CancellationTokenSource()).Token);
        }
    }
}
