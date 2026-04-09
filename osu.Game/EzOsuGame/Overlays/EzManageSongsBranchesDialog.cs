// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzManageSongsBranchesDialog : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private IDisposable? duckOperation;
        private IDisposable? collectionSubscription;
        private bool activationEntryRequested;
        private string? selectedEntryKey;
        private int generationQueueCounter;
        private readonly HashSet<Guid> pendingCollectionHideOperations = new HashSet<Guid>();
        private readonly HashSet<Guid> pendingCollectionDeleteOperations = new HashSet<Guid>();

        private BasicSearchTextBox searchTextBox = null!;
        private OsuTextFlowContainer subtitleText = null!;
        private TruncatingSpriteText selectedBranchText = null!;
        private FillFlowContainer listFlow = null!;
        private RoundedButton generateButton = null!;
        private Func<IReadOnlyList<BeatmapInfo>>? filteredBeatmapsProvider;

        private IReadOnlyList<BranchManagerEntry> displayedEntries = Array.Empty<BranchManagerEntry>();

        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private BeatmapCarousel? carousel { get; set; }

        [Resolved]
        private MusicController? musicController { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public EzManageSongsBranchesDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.55f, 0.82f);

            Masking = true;
            CornerRadius = 10;
        }

        public void ShowManager()
        {
            activationEntryRequested = false;

            if (IsLoaded)
            {
                updateSubtitle();
                refreshBranches();
            }

            Show();
        }

        public void SetFilteredBeatmapsProvider(Func<IReadOnlyList<BeatmapInfo>> provider)
        {
            filteredBeatmapsProvider = provider;

            if (IsLoaded)
                updateSelectedBranchState();
        }

        public void ShowForActivation()
        {
            activationEntryRequested = true;

            if (IsLoaded)
            {
                updateSubtitle();
                refreshBranches();
            }

            Show();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Padding = new MarginPadding { Vertical = 10, Horizontal = 20 },
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    Text = EzManageSongsBranchesDialogStrings.TITLE,
                                                    Font = OsuFont.GetFont(size: 30),
                                                },
                                                subtitleText = new OsuTextFlowContainer(t =>
                                                {
                                                    t.Colour = colours.Yellow;
                                                    t.Font = t.Font.With(size: 15, weight: FontWeight.Medium);
                                                })
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    TextAnchor = Anchor.TopCentre,
                                                    Margin = new MarginPadding { Top = 4 },
                                                },
                                            }
                                        },
                                        new IconButton
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            Icon = FontAwesome.Solid.Times,
                                            Colour = colours.GreySeaFoamDarker,
                                            Scale = new Vector2(0.8f),
                                            Margin = new MarginPadding { Top = 12, Right = 10 },
                                            Action = Hide,
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = 12, Bottom = 10 },
                                    Child = searchTextBox = new BasicSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        ReleaseFocusOnCommit = false,
                                        HoldFocus = true,
                                        PlaceholderText = HomeStrings.SearchPlaceholder,
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Horizontal = 12 },
                                    Masking = true,
                                    CornerRadius = 10,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.GreySeaFoamDarker,
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding(10),
                                            Child = new OsuScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Child = listFlow = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(0, 8),
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(12),
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                        Children = new Drawable[]
                                        {
                                            selectedBranchText = new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.Medium),
                                            },
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                RowDimensions = new[]
                                                {
                                                    new Dimension(GridSizeMode.AutoSize),
                                                },
                                                ColumnDimensions = new[]
                                                {
                                                    new Dimension(),
                                                    new Dimension(),
                                                    new Dimension(),
                                                },
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        generateButton = new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageSongsBranchesDialogStrings.GENERATE_FILTER_BRANCH,
                                                            Action = generateVisibleBranch,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageSongsBranchesDialogStrings.GENERATE_COLLECTION_BRANCH,
                                                            Action = generateSelectedBranch,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageSongsBranchesDialogStrings.OPEN_BRANCH_DIRECTORY,
                                                            Action = openBranchDirectory,
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            searchTextBox.Current.BindValueChanged(_ => Schedule(refreshBranches));
            ruleset.BindValueChanged(_ => Schedule(refreshBranches));
            mods.BindValueChanged(_ => Schedule(refreshBranches));
            ezAnalysisCache.ActiveSongsBranchVersion.BindValueChanged(_ => Schedule(refreshBranches));
            collectionSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>().OrderBy(c => c.Name), (_, __) => Schedule(refreshBranches));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            collectionSubscription?.Dispose();
        }

        protected override void PopIn()
        {
            refreshBranches();
            updateSubtitle();

            duckOperation = musicController?.Duck(new DuckParameters
            {
                DuckVolumeTo = 1,
                DuckDuration = 100,
                RestoreDuration = 100,
            });

            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);

            ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(searchTextBox));
        }

        protected override void PopOut()
        {
            base.PopOut();

            duckOperation?.Dispose();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);

            GetContainingFocusManager()?.TriggerFocusContention(this);
        }

        private void updateSubtitle()
        {
            subtitleText.Clear();
            subtitleText.AddText(activationEntryRequested
                ? EzManageSongsBranchesDialogStrings.ACTIVATION_SUBTITLE
                : EzManageSongsBranchesDialogStrings.MANAGER_SUBTITLE);
        }

        private void refreshBranches()
        {
            if (!IsLoaded)
                return;

            IReadOnlyList<BranchManagerEntry> availableEntries = getAvailableEntries();
            string searchTerm = searchTextBox.Current.Value?.Trim() ?? string.Empty;

            displayedEntries = string.IsNullOrWhiteSpace(searchTerm)
                ? availableEntries
                : availableEntries.Where(entry => matchesSearch(entry, searchTerm)).ToList();

            if (displayedEntries.All(entry => !string.Equals(entry.SelectionKey, selectedEntryKey, StringComparison.Ordinal)))
            {
                string? activeEntryKey = displayedEntries.FirstOrDefault(entry => entry.HasBranch && entry.BranchDatabasePath != null && ezAnalysisCache.IsSongsBranchActive(entry.BranchDatabasePath))
                                                         .SelectionKey;
                selectedEntryKey = activeEntryKey ?? displayedEntries.FirstOrDefault().SelectionKey;
            }

            listFlow.Clear();

            if (displayedEntries.Count == 0)
            {
                selectedEntryKey = null;
                listFlow.Add(new EmptyStateText
                {
                    Text = string.IsNullOrWhiteSpace(searchTerm)
                        ? EzManageSongsBranchesDialogStrings.NO_ITEMS_AVAILABLE
                        : EzManageSongsBranchesDialogStrings.NO_ITEMS_MATCHING_SEARCH,
                });
            }
            else
            {
                var createdEntries = displayedEntries.Where(entry => entry.HasBranch).ToList();
                var collectionEntries = displayedEntries.Where(entry => !entry.HasBranch).ToList();

                if (createdEntries.Count > 0)
                {
                    listFlow.Add(new SectionHeaderText
                    {
                        Text = EzManageSongsBranchesDialogStrings.GENERATED_SECTION,
                    });

                    foreach (var entry in createdEntries)
                    {
                        listFlow.Add(new EzBranchListItem(entry)
                        {
                            IsSelected = string.Equals(entry.SelectionKey, selectedEntryKey, StringComparison.Ordinal),
                            IsActive = entry.BranchDatabasePath != null && ezAnalysisCache.IsSongsBranchActive(entry.BranchDatabasePath),
                            SelectAction = selectEntry,
                            ToggleActivationAction = toggleBranchActivation,
                            DeleteBranchAction = confirmDeleteBranch,
                        });
                    }
                }

                if (collectionEntries.Count > 0)
                {
                    listFlow.Add(new SectionHeaderText
                    {
                        Text = EzManageSongsBranchesDialogStrings.UNGENERATED_SECTION,
                    });

                    foreach (var entry in collectionEntries)
                    {
                        listFlow.Add(new EzBranchListItem(entry)
                        {
                            IsSelected = string.Equals(entry.SelectionKey, selectedEntryKey, StringComparison.Ordinal),
                            SelectAction = selectEntry,
                            ToggleHideAction = toggleCollectionHidden,
                            DeleteCollectionBeatmapsAction = deleteCollectionLocalBeatmaps,
                        });
                    }
                }
            }

            updateSelectedBranchState();
        }

        private IReadOnlyList<BranchManagerEntry> getAvailableEntries()
        {
            IReadOnlyList<EzAnalysisPersistentStore.SongsBranchDescriptor> branches = ezAnalysisCache.GetAvailableSongsBranches(ruleset.Value, mods.Value);
            List<CollectionSnapshot> collections = realm.Run(r => r.All<BeatmapCollection>()
                                                                   .OrderBy(c => c.Name)
                                                                   .AsEnumerable()
                                                                   .Select(c => new CollectionSnapshot(c.ID, c.Name, c.BeatmapMD5Hashes.Count))
                                                                   .ToList());

            var collectionsById = collections.ToDictionary(collection => collection.ID);
            IReadOnlySet<Guid> hiddenCollectionIds = ezAnalysisCache.GetHiddenCollectionIds();
            var usedSelectionKeys = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<BranchManagerEntry>(branches.Count + collections.Count);

            foreach (var branch in branches)
            {
                Guid sourceCollectionId = branch.Metadata.SourceCollectionId;
                CollectionSnapshot sourceCollection = default;
                bool hasSourceCollectionId = sourceCollectionId != Guid.Empty;
                bool hasMappedCollection = hasSourceCollectionId && collectionsById.TryGetValue(sourceCollectionId, out sourceCollection);
                string preferredSelectionKey = hasMappedCollection ? createCollectionSelectionKey(sourceCollectionId) : createBranchSelectionKey(branch.DatabasePath);
                string selectionKey = usedSelectionKeys.Add(preferredSelectionKey) ? preferredSelectionKey : createBranchSelectionKey(branch.DatabasePath);

                entries.Add(new BranchManagerEntry(
                    IsCollectionRow: false,
                    selectionKey,
                    hasMappedCollection ? sourceCollectionId : null,
                    hasMappedCollection ? sourceCollection.Name : branch.Metadata.SourceCollectionName,
                    hasMappedCollection ? sourceCollection.BeatmapCount : branch.Metadata.SourceCollectionBeatmapCount,
                    branch,
                    false));
            }

            foreach (var collection in collections)
            {
                entries.Add(new BranchManagerEntry(
                    IsCollectionRow: true,
                    createCollectionSelectionKey(collection.ID),
                    collection.ID,
                    collection.Name,
                    collection.BeatmapCount,
                    null,
                    hiddenCollectionIds.Contains(collection.ID)));
            }

            return entries;
        }

        private static bool matchesSearch(BranchManagerEntry entry, string searchTerm)
        {
            if (entry.HasBranch)
            {
                return entry.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                       || entry.RelativePath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                       || entry.ModsDisplay.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                       || entry.SourceCollectionName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
            }

            return entry.SourceCollectionName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }

        private void selectEntry(BranchManagerEntry entry)
        {
            selectedEntryKey = entry.SelectionKey;
            refreshBranches();
        }

        private void generateSelectedBranch() => _ = generateSelectedBranchAsync();

        private void generateVisibleBranch() => _ = generateVisibleBranchAsync();

        private async Task generateVisibleBranchAsync()
        {
            var filteredBeatmaps = getFilteredBeatmaps();

            if (filteredBeatmaps.Count == 0)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.VISIBLE_BRANCH_EMPTY_FILTER_RESULT,
                });
                return;
            }

            int queueId = ++generationQueueCounter;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = withGenerationQueueTag(queueId, LocalisableString.Format(EzManageSongsBranchesDialogStrings.GENERATING_BRANCH_FROM_VISIBLE, filteredBeatmaps.Count))
            };

            postNotification(notification);

            try
            {
                var result = await ezAnalysisCache.CreateAndActivateSongsBranchAsync(filteredBeatmaps, null,
                    ruleset.Value, mods.Value, activateAfterCreate: false,
                    progress: (processed, total) => notification.Progress = total <= 0 ? 0 : (float)processed / total).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (!result.Success)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleErrorNotification
                        {
                            Text = withGenerationQueueTag(queueId, result.Message),
                        });
                        refreshBranches();
                        return;
                    }

                    notification.CompletionText = withGenerationQueueTag(queueId,
                        LocalisableString.Format(EzManageSongsBranchesDialogStrings.BRANCH_GENERATED,
                            result.DisplayName ?? EzManageSongsBranchesDialogStrings.VISIBLE_GENERATED_SOURCE_NAME,
                            result.StoredBeatmapCount,
                            result.RequestedBeatmapCount));

                    if (!string.IsNullOrEmpty(result.DatabasePath))
                        notification.CompletionClickAction = () => storage.PresentFileExternally(result.DatabasePath);

                    notification.State = ProgressNotificationState.Completed;
                    refreshBranches();
                });
            }
            catch (Exception)
            {
                Schedule(() =>
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    notifications?.Post(new SimpleErrorNotification
                    {
                        Text = withGenerationQueueTag(queueId, EzManageSongsBranchesDialogStrings.GENERATE_BRANCH_FAILED),
                    });
                });
            }
        }

        private async Task generateSelectedBranchAsync()
        {
            if (getSelectedEntry() is not BranchManagerEntry selectedEntry || selectedEntry.SourceCollectionId == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.SELECT_COLLECTION_FIRST,
                });
                return;
            }

            EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection = getSourceCollectionSnapshot(selectedEntry.SourceCollectionId.Value);

            if (sourceCollection == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.SELECT_COLLECTION_FIRST,
                });
                return;
            }

            IReadOnlyList<BeatmapInfo> collectionBeatmaps = getLocalBeatmapsForCollection(sourceCollection.Value.BeatmapMd5Hashes);

            if (collectionBeatmaps.Count == 0)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.COLLECTION_HAS_NO_LOCAL_BEATMAPS,
                });
                return;
            }

            int queueId = ++generationQueueCounter;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = withGenerationQueueTag(queueId,
                    LocalisableString.Format(EzManageSongsBranchesDialogStrings.GENERATING_BRANCH_FROM_COLLECTION, selectedEntry.SourceCollectionName, collectionBeatmaps.Count))
            };

            postNotification(notification);

            try
            {
                var result = await ezAnalysisCache.CreateAndActivateSongsBranchAsync(collectionBeatmaps, sourceCollection,
                    ruleset.Value, mods.Value, activateAfterCreate: false,
                    progress: (processed, total) => notification.Progress = total <= 0 ? 0 : (float)processed / total).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (!result.Success)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleErrorNotification
                        {
                            Text = withGenerationQueueTag(queueId, result.Message),
                        });
                        refreshBranches();
                        return;
                    }

                    selectedEntryKey = createCollectionSelectionKey(selectedEntry.SourceCollectionId.Value);
                    notification.CompletionText = withGenerationQueueTag(queueId,
                        LocalisableString.Format(EzManageSongsBranchesDialogStrings.BRANCH_GENERATED,
                            result.DisplayName ?? selectedEntry.SourceCollectionName,
                            result.StoredBeatmapCount,
                            result.RequestedBeatmapCount));

                    if (!string.IsNullOrEmpty(result.DatabasePath))
                        notification.CompletionClickAction = () => storage.PresentFileExternally(result.DatabasePath);

                    notification.State = ProgressNotificationState.Completed;
                    refreshBranches();
                });
            }
            catch (Exception)
            {
                Schedule(() =>
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    notifications?.Post(new SimpleErrorNotification
                    {
                        Text = withGenerationQueueTag(queueId, EzManageSongsBranchesDialogStrings.GENERATE_BRANCH_FAILED),
                    });
                });
            }
        }

        private EzAnalysisPersistentStore.SourceCollectionSnapshot? getSourceCollectionSnapshot(Guid collectionId)
        {
            return realm.Run<EzAnalysisPersistentStore.SourceCollectionSnapshot?>(r =>
            {
                var collection = r.Find<BeatmapCollection>(collectionId);

                if (collection == null)
                    return null;

                return new EzAnalysisPersistentStore.SourceCollectionSnapshot(
                    collection.ID,
                    collection.Name,
                    collection.LastModified.ToUnixTimeMilliseconds(),
                    collection.BeatmapMD5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            });
        }

        private IReadOnlyList<BeatmapInfo> getLocalBeatmapsForCollection(IEnumerable<string> beatmapMd5Hashes)
        {
            HashSet<string> beatmapLookup = beatmapMd5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (beatmapLookup.Count == 0)
                return Array.Empty<BeatmapInfo>();

            return beatmapManager.GetAllUsableBeatmapSets()
                                 .SelectMany(set => set.Beatmaps)
                                 .Where(beatmap => !string.IsNullOrWhiteSpace(beatmap.MD5Hash) && beatmapLookup.Contains(beatmap.MD5Hash))
                                 .DistinctBy(beatmap => beatmap.ID)
                                 .ToList();
        }

        private void activateSelectedBranch()
        {
            if (getSelectedEntry() is not BranchManagerEntry selectedEntry || !selectedEntry.HasBranch || selectedEntry.BranchDatabasePath == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.SELECT_GENERATED_BRANCH_FIRST,
                });
                return;
            }

            activateBranch(selectedEntry.BranchDatabasePath);
        }

        private void activateBranch(string databasePath)
        {
            EzAnalysisPersistentStore.SongsBranchDescriptor selectedBranch = displayedEntries.Where(entry => entry.HasBranch).Select(entry => entry.BranchValue)
                                                                                             .FirstOrDefault(branch => string.Equals(branch.DatabasePath, databasePath,
                                                                                                 StringComparison.OrdinalIgnoreCase));

            if (!ezAnalysisCache.TryActivateSongsBranch(databasePath, out LocalisableString message))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                return;
            }

            if (!string.IsNullOrEmpty(selectedBranch.DatabasePath))
                tryApplyBranchMods(selectedBranch.Metadata);

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();

            if (activationEntryRequested)
                Hide();
        }

        private void tryApplyBranchMods(EzAnalysisPersistentStore.SongsBranchMetadata metadata)
        {
            if (metadata.RulesetOnlineId != ruleset.Value.OnlineID)
                return;

            if (string.IsNullOrWhiteSpace(metadata.ModsJson))
                return;

            if (mods is not Bindable<IReadOnlyList<Mod>> writableMods)
            {
                Logger.Log($"启用分支库“{metadata.DisplayName}”时，当前 mods 绑定不可写，已跳过套用建库时 mods。",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            try
            {
                var apiMods = JsonConvert.DeserializeObject<IEnumerable<APIMod>>(metadata.ModsJson)?.ToArray();

                if (apiMods == null)
                    return;

                var rulesetInstance = ruleset.Value.CreateInstance();
                var restoredMods = new List<Mod>();
                var ignoredMods = new List<string>();
                var ignoredSettings = new List<string>();

                foreach (var apiMod in apiMods)
                {
                    Mod? restoredMod = rulesetInstance.CreateModFromAcronym(apiMod.Acronym);

                    if (restoredMod == null)
                    {
                        ignoredMods.Add(apiMod.Acronym);
                        continue;
                    }

                    HashSet<string> availableSettings = restoredMod.GetSettingsSourceProperties()
                                                                   .Select(tuple => tuple.Item2.Name.ToSnakeCase())
                                                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (string settingKey in apiMod.Settings.Keys)
                    {
                        if (!availableSettings.Contains(settingKey))
                            ignoredSettings.Add($"{apiMod.Acronym}.{settingKey}");
                    }

                    foreach (var (_, property) in restoredMod.GetSettingsSourceProperties())
                    {
                        string settingKey = property.Name.ToSnakeCase();

                        if (!apiMod.Settings.TryGetValue(settingKey, out object? settingValue))
                            continue;

                        try
                        {
                            restoredMod.CopyAdjustedSetting((IBindable)property.GetValue(restoredMod)!, settingValue);
                        }
                        catch
                        {
                            ignoredSettings.Add($"{apiMod.Acronym}.{settingKey}");
                        }
                    }

                    restoredMods.Add(restoredMod);
                }

                if (!ModUtils.CheckCompatibleSet(restoredMods, out var invalidMods))
                {
                    ignoredMods.AddRange(invalidMods.Select(mod => mod.Acronym));
                    restoredMods = restoredMods.Where(mod => !invalidMods.Contains(mod)).ToList();
                }

                writableMods.Value = restoredMods.ToArray();
                logIgnoredBranchMods(metadata.DisplayName, ignoredMods, ignoredSettings);
            }
            catch
            {
            }
        }

        private void logIgnoredBranchMods(string branchDisplayName, IEnumerable<string> ignoredMods, IEnumerable<string> ignoredSettings)
        {
            string[] ignoredModList = ignoredMods.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string[] ignoredSettingList = ignoredSettings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            if (ignoredModList.Length > 0)
            {
                Logger.Log($"启用分支库“{branchDisplayName}”时，以下 mods 无法套用，已忽略: {string.Join(", ", ignoredModList)}",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }

            if (ignoredSettingList.Length > 0)
            {
                Logger.Log($"启用分支库“{branchDisplayName}”时，以下 mod 设置无法套用，已忽略: {string.Join(", ", ignoredSettingList)}",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
        }

        private void toggleBranchHidden(BranchManagerEntry entry)
        {
            if (!entry.HasBranch || entry.BranchDatabasePath == null)
                return;

            if (!ezAnalysisCache.TryToggleSongsBranchHidden(entry.BranchDatabasePath, out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });

                refreshBranches();
                return;
            }

            selectedEntryKey = entry.SelectionKey;

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();

            if (nonHideableBeatmapSets.Count == 0)
                return;

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteNonHideableBeatmapSetsDialog(nonHideableBeatmapSets.Count,
                    () => deleteNonHideableBeatmapSets(nonHideableBeatmapSets)));
                return;
            }

            postNotification(new SimpleNotification
            {
                Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.NON_HIDEABLE_BEATMAPSETS_FOUND, nonHideableBeatmapSets.Count),
            });
        }

        private void toggleCollectionHidden(BranchManagerEntry entry) => _ = toggleCollectionHiddenAsync(entry);

        private void deleteCollectionLocalBeatmaps(BranchManagerEntry entry)
        {
            if (entry.SourceCollectionId == null)
                return;

            if (dialogOverlay == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_CONFIRMATION_UNAVAILABLE,
                });
                return;
            }

            dialogOverlay.Push(new DeleteCollectionLocalBeatmapsDialog(entry.SourceCollectionName, () => _ = deleteCollectionLocalBeatmapsAsync(entry)));
        }

        private async Task deleteCollectionLocalBeatmapsAsync(BranchManagerEntry entry)
        {
            if (entry.SourceCollectionId == null)
                return;

            lock (pendingCollectionDeleteOperations)
            {
                if (!pendingCollectionDeleteOperations.Add(entry.SourceCollectionId.Value))
                    return;
            }

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_RUNNING, entry.SourceCollectionName),
            };

            postNotification(notification);

            try
            {
                var deleteResult = await Task.Run(() =>
                {
                    EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection = getSourceCollectionSnapshot(entry.SourceCollectionId.Value);

                    if (sourceCollection == null)
                        return (success: false, message: EzManageSongsBranchesDialogStrings.SELECT_COLLECTION_FIRST);

                    IReadOnlyList<BeatmapInfo> localBeatmaps = getLocalBeatmapsForCollection(sourceCollection.Value.BeatmapMd5Hashes);

                    if (localBeatmaps.Count == 0)
                    {
                        return (success: true, message: LocalisableString.Format(
                            EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_NO_LOCAL_BEATMAPS,
                            sourceCollection.Value.BeatmapMd5Hashes.Count));
                    }

                    List<BeatmapSetInfo> deletableBeatmapSets = localBeatmaps
                                                                .Select(beatmap => beatmap.BeatmapSet)
                                                                .Where(set => set != null)
                                                                .DistinctBy(set => set!.ID)
                                                                .Select(set => set!)
                                                                .ToList();

                    if (deletableBeatmapSets.Count > 0)
                        beatmapManager.Delete(deletableBeatmapSets, silent: true);

                    return (success: true, message: LocalisableString.Format(
                        EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_COMPLETED,
                        localBeatmaps.Count,
                        sourceCollection.Value.BeatmapMd5Hashes.Count));
                }).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (!deleteResult.success)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleErrorNotification
                        {
                            Text = deleteResult.message,
                        });
                        return;
                    }

                    notification.State = ProgressNotificationState.Completed;
                    notification.CompletionText = LocalisableString.Interpolate($"{deleteResult.message}\n{EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_RESTORE_HINT}");
                    selectedEntryKey = entry.SelectionKey;
                    refreshBranches();
                });
            }
            catch
            {
                Schedule(() =>
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    notifications?.Post(new SimpleErrorNotification
                    {
                        Text = EzManageSongsBranchesDialogStrings.COLLECTION_DELETE_FAILED,
                    });
                });
            }
            finally
            {
                lock (pendingCollectionDeleteOperations)
                    pendingCollectionDeleteOperations.Remove(entry.SourceCollectionId.Value);
            }
        }

        private async Task toggleCollectionHiddenAsync(BranchManagerEntry entry)
        {
            if (entry.SourceCollectionId == null)
                return;

            lock (pendingCollectionHideOperations)
            {
                if (!pendingCollectionHideOperations.Add(entry.SourceCollectionId.Value))
                    return;
            }

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.COLLECTION_HIDE_RUNNING, entry.SourceCollectionName),
            };

            postNotification(notification);

            try
            {
                var hideResult = await Task.Run(() =>
                {
                    EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection = getSourceCollectionSnapshot(entry.SourceCollectionId.Value);

                    if (sourceCollection == null)
                        return (success: false, message: EzManageSongsBranchesDialogStrings.SELECT_COLLECTION_FIRST, nonHideableBeatmapSets: Array.Empty<BeatmapSetInfo>());

                    bool success = ezAnalysisCache.TryToggleCollectionHidden(sourceCollection.Value.CollectionId, sourceCollection.Value.Name, sourceCollection.Value.BeatmapMd5Hashes,
                        out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets);

                    return (success, message, nonHideableBeatmapSets);
                }).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (!hideResult.success)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleErrorNotification
                        {
                            Text = hideResult.message,
                        });
                        refreshBranches();
                        return;
                    }

                    notification.State = ProgressNotificationState.Completed;
                    notification.CompletionText = hideResult.message;
                    selectedEntryKey = entry.SelectionKey;
                    refreshBranches();

                    if (hideResult.nonHideableBeatmapSets.Count == 0)
                        return;

                    if (dialogOverlay != null)
                    {
                        dialogOverlay.Push(new DeleteNonHideableBeatmapSetsDialog(hideResult.nonHideableBeatmapSets.Count,
                            () => deleteNonHideableBeatmapSets(hideResult.nonHideableBeatmapSets)));
                        return;
                    }

                    notifications?.Post(new SimpleNotification
                    {
                        Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.NON_HIDEABLE_BEATMAPSETS_FOUND, hideResult.nonHideableBeatmapSets.Count),
                    });
                });
            }
            catch
            {
                Schedule(() =>
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    notifications?.Post(new SimpleErrorNotification
                    {
                        Text = EzManageSongsBranchesDialogStrings.COLLECTION_HIDE_FAILED,
                    });
                });
            }
            finally
            {
                lock (pendingCollectionHideOperations)
                    pendingCollectionHideOperations.Remove(entry.SourceCollectionId.Value);
            }
        }

        private void toggleBranchActivation(BranchManagerEntry entry)
        {
            if (!entry.HasBranch || entry.BranchDatabasePath == null)
                return;

            if (!ezAnalysisCache.TryToggleSongsBranchActivation(entry.BranchDatabasePath, out LocalisableString message))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                return;
            }

            selectedEntryKey = entry.SelectionKey;

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();

            if (activationEntryRequested)
                Hide();
        }

        private void confirmDeleteBranch(BranchManagerEntry entry)
        {
            if (!entry.HasBranch || entry.BranchDatabasePath == null)
                return;

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteBranchDialog(entry.DisplayName, () => deleteBranch(entry.BranchDatabasePath)));
                return;
            }

            deleteBranch(entry.BranchDatabasePath);
        }

        private void deleteNonHideableBeatmapSets(IReadOnlyList<BeatmapSetInfo> beatmapSets)
        {
            List<BeatmapSetInfo> deletableBeatmapSets = beatmapSets.DistinctBy(set => set.ID).ToList();

            beatmapManager.Delete(deletableBeatmapSets, silent: true);

            postNotification(new SimpleNotification
            {
                Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.DELETED_NON_HIDEABLE_BEATMAPSETS, deletableBeatmapSets.Count),
            });

            refreshBranches();
        }

        private void confirmDeleteSelectedBranch()
        {
            if (getSelectedEntry() is not BranchManagerEntry selectedEntry || !selectedEntry.HasBranch || selectedEntry.BranchDatabasePath == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageSongsBranchesDialogStrings.SELECT_GENERATED_BRANCH_FIRST,
                });
                return;
            }

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteBranchDialog(selectedEntry.DisplayName, () => deleteBranch(selectedEntry.BranchDatabasePath)));
                return;
            }

            deleteBranch(selectedEntry.BranchDatabasePath);
        }

        private void deleteBranch(string databasePath)
        {
            if (!ezAnalysisCache.TryDeleteSongsBranch(databasePath, out LocalisableString message))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                refreshBranches();
                return;
            }

            if (getSelectedEntry() is BranchManagerEntry selectedEntry
                && string.Equals(selectedEntry.BranchDatabasePath, databasePath, StringComparison.OrdinalIgnoreCase))
            {
                selectedEntryKey = selectedEntry.SourceCollectionId.HasValue
                    ? createCollectionSelectionKey(selectedEntry.SourceCollectionId.Value)
                    : null;
            }

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();
        }

        private void openBranchDirectory() => storage.GetStorageForDirectory(EzAnalysisPersistentStore.SONGS_BRANCH_DATABASE_DIRECTORY).PresentExternally();

        private BranchManagerEntry? getSelectedEntry()
        {
            foreach (var entry in displayedEntries)
            {
                if (string.Equals(entry.SelectionKey, selectedEntryKey, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private void updateSelectedBranchState()
        {
            BranchManagerEntry? selectedEntry = getSelectedEntry();

            bool hasVisible = getFilteredBeatmaps().Count > 0;
            generateButton.Enabled.Value = hasVisible;

            if (selectedEntry is BranchManagerEntry entry)
                selectedBranchText.Text = LocalisableString.Format(EzManageSongsBranchesDialogStrings.SELECTED_ITEM, entry.DisplayName);
            else
                selectedBranchText.Text = EzManageSongsBranchesDialogStrings.NO_ITEM_SELECTED;
        }

        private static string createCollectionSelectionKey(Guid collectionId) => $"collection:{collectionId:D}";

        private static string createBranchSelectionKey(string databasePath) => $"branch:{Path.GetFullPath(databasePath)}";

        private static LocalisableString withGenerationQueueTag(int queueId, LocalisableString message)
            => LocalisableString.Interpolate($"[队列#{queueId}] {message}");

        private IReadOnlyList<BeatmapInfo> getFilteredBeatmaps()
            => filteredBeatmapsProvider?.Invoke() ?? carousel?.GetFilteredBeatmaps() ?? Array.Empty<BeatmapInfo>();

        private void postNotification(Notification notification) => Schedule(() => notifications?.Post(notification));

        private readonly record struct CollectionSnapshot(Guid ID, string Name, int BeatmapCount);

        private partial class DeleteBranchDialog : DeletionDialog
        {
            public DeleteBranchDialog(string displayName, Action deleteAction)
            {
                BodyText = LocalisableString.Format(EzManageSongsBranchesDialogStrings.DELETE_BRANCH_CONFIRMATION, displayName);
                DangerousAction = deleteAction;
            }
        }

        private partial class DeleteCollectionLocalBeatmapsDialog : DeletionDialog
        {
            public DeleteCollectionLocalBeatmapsDialog(string collectionName, Action deleteAction)
            {
                BodyText = LocalisableString.Format(EzManageSongsBranchesDialogStrings.DELETE_COLLECTION_LOCAL_BEATMAPS_CONFIRMATION, collectionName);
                DangerousAction = deleteAction;
            }
        }

        private partial class DeleteNonHideableBeatmapSetsDialog : DeletionDialog
        {
            public DeleteNonHideableBeatmapSetsDialog(int beatmapSetCount, Action deleteAction)
            {
                BodyText = LocalisableString.Format(EzManageSongsBranchesDialogStrings.DELETE_NON_HIDEABLE_BEATMAPSETS_CONFIRMATION, beatmapSetCount);
                DangerousAction = deleteAction;
            }
        }

        private static class EzManageSongsBranchesDialogStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString TITLE = new EzLocalizationManager.EzLocalisableString("分支曲库管理", "Branch Library Manager");

            internal static readonly EzLocalizationManager.EzLocalisableString MANAGER_SUBTITLE = new EzLocalizationManager.EzLocalisableString(
                "上方显示已生成的分支曲库，下方显示全部收藏夹；分支曲库右侧可启用/停用和删除，收藏夹右侧可按来源收藏夹切换隐藏。",
                "Generated branch libraries are shown first, followed by all collections. Branch entries support activate/deactivate and delete on the right, while collection entries support hide toggle by source collection on the right.");

            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATION_SUBTITLE = new EzLocalizationManager.EzLocalisableString("分支曲库右侧可逐个启用/停用与删除，可叠加启用多个分支；收藏夹右侧可按来源收藏夹切换隐藏。",
                "Use the right-side controls on branch entries to activate/deactivate or delete each branch (multiple can be active). Use the right-side controls on collection entries to toggle hide by source collection.");

            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_FILTER_BRANCH = new EzLocalizationManager.EzLocalisableString("生成过滤分支库", "Generate Filter Branch Library");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_COLLECTION_BRANCH = new EzLocalizationManager.EzLocalisableString("生成收藏夹分支库", "Generate Collection Branch Library");
            internal static readonly EzLocalizationManager.EzLocalisableString OPEN_BRANCH_DIRECTORY = new EzLocalizationManager.EzLocalisableString("打开分支库目录", "Open Branch Directory");

            internal static readonly EzLocalizationManager.EzLocalisableString NO_ITEMS_AVAILABLE = new EzLocalizationManager.EzLocalisableString("当前没有收藏夹或分支曲库。",
                "No collections or branch libraries are currently available.");

            internal static readonly EzLocalizationManager.EzLocalisableString NO_ITEMS_MATCHING_SEARCH = new EzLocalizationManager.EzLocalisableString("没有匹配的收藏夹或分支曲库。",
                "No collections or branch libraries match the current search.");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECT_COLLECTION_FIRST = new EzLocalizationManager.EzLocalisableString("请先选择一个收藏夹。", "Select a collection first.");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECT_GENERATED_BRANCH_FIRST =
                new EzLocalizationManager.EzLocalisableString("请先选择一个已生成的分支曲库。", "Select a generated branch library first.");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_ITEM = new EzLocalizationManager.EzLocalisableString("已选中：{0}", "Selected: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_ITEM_SELECTED = new EzLocalizationManager.EzLocalisableString("未选中项目", "No item selected");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATED_SECTION = new EzLocalizationManager.EzLocalisableString("已生成分支曲库", "Generated Branch Libraries");
            internal static readonly EzLocalizationManager.EzLocalisableString UNGENERATED_SECTION = new EzLocalizationManager.EzLocalisableString("收藏夹列表", "Collections");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_HIDE_RUNNING = new EzLocalizationManager.EzLocalisableString("正在后台切换收藏夹“{0}”的隐藏状态...",
                "Toggling hide for collection \"{0}\" in the background...");

            internal static readonly EzLocalizationManager.EzLocalisableString
                COLLECTION_HIDE_FAILED = new EzLocalizationManager.EzLocalisableString("切换收藏夹隐藏失败。", "Failed to toggle collection hide.");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_RUNNING = new EzLocalizationManager.EzLocalisableString("正在后台删除收藏夹“{0}”命中的本地谱面...",
                "Deleting local beatmaps matched by collection \"{0}\" in the background...");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_COMPLETED = new EzLocalizationManager.EzLocalisableString("已删除 {0:#,0} 张本地谱面；收藏夹记录完整保留（共 {1:#,0} 条）。",
                "Deleted {0:#,0} local beatmaps; collection records remain intact ({1:#,0} total entries).");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_NO_LOCAL_BEATMAPS = new EzLocalizationManager.EzLocalisableString(
                "本地没有命中可删除谱面；收藏夹记录完整保留（共 {0:#,0} 条）。", "No local beatmaps matched for deletion; collection records remain intact ({0:#,0} total entries).");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_FAILED = new EzLocalizationManager.EzLocalisableString("删除收藏夹命中的本地谱面失败。",
                "Failed to delete local beatmaps matched by the collection.");

            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_COLLECTION_LOCAL_BEATMAPS_CONFIRMATION = new EzLocalizationManager.EzLocalisableString(
                "危险操作：将删除收藏夹“{0}”命中的本地谱面（不修改收藏夹记录）。可在设置-维护中通过“恢复所有最近删除的谱面”撤销。",
                "Dangerous operation: this deletes local beatmaps matched by collection \"{0}\" (collection entries remain). You can undo this in Settings > Maintenance via \"Restore all recently deleted beatmaps\".");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_CONFIRMATION_UNAVAILABLE = new EzLocalizationManager.EzLocalisableString(
                "危险操作需要确认，当前无法显示确认对话框，已取消执行。",
                "This dangerous operation requires confirmation, but the confirmation dialog is currently unavailable. The operation was cancelled.");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_DELETE_RESTORE_HINT = new EzLocalizationManager.EzLocalisableString(
                "可在设置-维护中使用“恢复所有最近删除的谱面”撤销。",
                "You can undo this in Settings > Maintenance via \"Restore all recently deleted beatmaps\".");

            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_HAS_NO_LOCAL_BEATMAPS = new EzLocalizationManager.EzLocalisableString("选中的收藏夹当前没有可写入分支曲库的本地谱面。",
                "The selected collection does not currently contain local beatmaps that can be written into a branch library.");

            internal static readonly EzLocalizationManager.EzLocalisableString GENERATING_BRANCH_FROM_COLLECTION = new EzLocalizationManager.EzLocalisableString("正在根据收藏夹“{0}”生成分支曲库（本地命中 {1} 张谱面）...",
                "Generating a branch library from collection \"{0}\" ({1} local beatmaps matched)...");

            internal static readonly EzLocalizationManager.EzLocalisableString VISIBLE_BRANCH_EMPTY_FILTER_RESULT = new EzLocalizationManager.EzLocalisableString("当前筛选/可见结果为空，未生成分支曲库。",
                "The current filtered/visible result is empty. No branch library was generated.");

            internal static readonly EzLocalizationManager.EzLocalisableString GENERATING_BRANCH_FROM_VISIBLE = new EzLocalizationManager.EzLocalisableString("正在根据当前可见谱面生成分支曲库（本地命中 {0} 张谱面）...",
                "Generating a branch library from visible beatmaps ({0} local beatmaps matched)...");

            internal static readonly EzLocalizationManager.EzLocalisableString VISIBLE_GENERATED_SOURCE_NAME = new EzLocalizationManager.EzLocalisableString("筛选结果", "Filtered results");

            internal static readonly EzLocalizationManager.EzLocalisableString BRANCH_GENERATED = new EzLocalizationManager.EzLocalisableString("分支曲库已生成：{0}（写入 {1}/{2} 张谱面）。",
                "Branch library generated: {0} ({1}/{2} beatmaps stored).");

            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("生成分支曲库失败。",
                "Failed to generate the branch library.");

            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_BRANCH_CONFIRMATION = new EzLocalizationManager.EzLocalisableString("删除分支库：{0}", "Delete branch sqlite: {0}");

            internal static readonly EzLocalizationManager.EzLocalisableString NON_HIDEABLE_BEATMAPSETS_FOUND = new EzLocalizationManager.EzLocalisableString("有 {0} 个谱包当前只剩最后一张可见 diff，无法继续隐藏。",
                "There are {0} beatmap sets with only one visible difficulty remaining, so they cannot be hidden further.");

            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_NON_HIDEABLE_BEATMAPSETS_CONFIRMATION = new EzLocalizationManager.EzLocalisableString(
                "有 {0} 个谱包当前只剩最后一张可见 diff，无法隐藏。是否直接删除这些谱包？", "There are {0} beatmap sets with only one visible difficulty remaining, so they cannot be hidden. Delete those beatmap sets?");

            internal static readonly EzLocalizationManager.EzLocalisableString DELETED_NON_HIDEABLE_BEATMAPSETS =
                new EzLocalizationManager.EzLocalisableString("已删除 {0} 个无法隐藏的谱包。", "Deleted {0} beatmap sets that could not be hidden.");
        }
    }
}
