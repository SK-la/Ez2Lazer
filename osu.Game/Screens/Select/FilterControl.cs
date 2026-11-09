// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select.Filter;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public sealed partial class FilterControl : OverlayContainer
    {
        // taken from draw visualiser. used for carousel alignment purposes.
        public const float HEIGHT_FROM_SCREEN_TOP = 171 - CORNER_RADIUS;

        public const float CORNER_RADIUS = EzUIStyleV1.CORNER_RADIUS_WEDGE;

        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

        private SongSelectSearchTextBox searchTextBox = null!;
        private ShearedToggleButton showConvertedBeatmapsButton = null!;
        private DifficultyRangeSlider difficultyRangeSlider = null!;
        private SortModeDropdown sortDropdown = null!; // Ez功能
        private SkillAxisDropdown sortAxisDropdown = null!;
        private ShearedDropdown<GroupModeDropdownItem> groupDropdown = null!;
        private SkillAxisDropdown groupAxisDropdown = null!;
        private GridContainer skillAxisRow = null!;
        private EzKeyModeSelector csSelector = null!;
        private ShearedKSPreviewButton ksPreviewButton = null!;
        private ShearedToggleButton ezAnalysisFilterButton = null!;
        private readonly Bindable<string> configCollectionFilter = new Bindable<string>();
        private Bindable<string> configSkillSortAxis = null!;
        private Bindable<string> configSkillGroupAxis = null!;
        private Bindable<string> configAnalysisSystemSelection = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        /// <summary>
        /// An optional method which can force certain criteria adjustments.
        /// </summary>
        public Action<FilterCriteria>? ApplyRequiredCriteria { get; set; }

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IBindable<APIUser> localUser = null!;
        private readonly IBindableList<int> localUserFavouriteBeatmapSets = new BindableList<int>();

        public LocalisableString StatusText
        {
            get => searchTextBox.StatusText;
            set => searchTextBox.StatusText = value;
        }

        public event Action<FilterCriteria>? CriteriaChanged;

        private FilterCriteria currentCriteria = null!;

        private IDisposable? collectionsSubscription;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Shear = OsuGame.SHEAR;
            Margin = new MarginPadding { Top = -CORNER_RADIUS, Right = -40 };

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = CORNER_RADIUS,
                    Masking = true,
                    Child = new EzSongSelectWedgeBackground
                    {
                        Anchor = Anchor.TopRight,
                        Scale = new Vector2(-1, 1),
                    }
                },
                new ReverseChildIDFillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, 5f),
                    Padding = new MarginPadding { Top = CORNER_RADIUS - 0.5f, Bottom = 2, Right = 40f, Left = 2f },
                    Children = new Drawable[]
                    {
                        new ReverseChildIDFillFlowContainer<Drawable>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0f, 0.5f),
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Shear = -OsuGame.SHEAR,
                                    Child = searchTextBox = new SongSelectSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        HoldFocus = true,
                                        ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                                    },
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Absolute), // can probably be removed?
                                        new Dimension(GridSizeMode.AutoSize),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            difficultyRangeSlider = new DifficultyRangeSlider
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                MinRange = 0.1f,
                                            },
                                            Empty(),
                                            showConvertedBeatmapsButton = new ShearedToggleButton
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.X,
                                                Text = UserInterfaceStrings.ShowConverts,
                                                Height = 30f,
                                            },
                                            ksPreviewButton = new ShearedKSPreviewButton
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                AutoSizeAxes = Axes.X,
                                                Text = "kSound",
                                                TooltipText = EzSongSelectStrings.KEY_SOUND_PREVIEW_TOOLTIP,
                                                Height = 30f,
                                            },
                                        },
                                    }
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 30,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            sortDropdown = new SortModeDropdown(SongSelectStrings.Sort)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                            },
                                            Empty(),
                                            groupDropdown = new GroupModeDropdown(SongSelectStrings.Group)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                            },
                                            Empty(),
                                            new CollectionDropdown
                                            {
                                                RelativeSizeAxes = Axes.X,
                                            },
                                            Empty(),
                                        }
                                    }
                                },
                                // Second row for skill-axis pickers; collapses when neither sort nor group uses SkillAxis.
                                skillAxisRow = new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 30,
                                    Shear = -OsuGame.SHEAR,
                                    Alpha = 0,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            sortAxisDropdown = new SkillAxisDropdown(EzSongSelectStrings.SKILL_AXIS_SORT)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Alpha = 0,
                                            },
                                            Empty(),
                                            groupAxisDropdown = new SkillAxisDropdown(EzSongSelectStrings.SKILL_AXIS_GROUP)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Alpha = 0,
                                            },
                                            Empty(),
                                            Empty(),
                                        }
                                    }
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Absolute), // can probably be removed?
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            csSelector = new EzKeyModeSelector
                                            {
                                                RelativeSizeAxes = Axes.X,
                                            },
                                            Empty(),
                                            ezAnalysisFilterButton = new ShearedToggleButton
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.X,
                                                Text = EzSongSelectStrings.EZ_ANALYSIS_FILTER,
                                                TooltipText = EzSongSelectStrings.EZ_ANALYSIS_FILTER_TOOLTIP,
                                                Height = 30f,
                                            },
                                        },
                                    }
                                },
                                new ScopedBeatmapSetDisplay
                                {
                                    ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                                }
                            },
                        }
                    },
                },
            };

            localUser = api.LocalUser.GetBoundCopy();
            localUserFavouriteBeatmapSets.BindTo(api.LocalUserState.FavouriteBeatmapSets);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            difficultyRangeSlider.LowerBound = config.GetBindable<double>(OsuSetting.DisplayStarsMinimum);
            difficultyRangeSlider.UpperBound = config.GetBindable<double>(OsuSetting.DisplayStarsMaximum);
            config.BindWith(OsuSetting.ShowConvertedBeatmaps, showConvertedBeatmapsButton.Active);
            // SortModeDropdown owns the SongSelectSortingMode binding itself.
            config.BindWith(OsuSetting.SongSelectCollectionFilter, configCollectionFilter);

            ezConfig.BindWith(Ez2Setting.EzAnalysisFilter, ezAnalysisFilterButton.Active);
            ezConfig.BindWith(Ez2Setting.KeySoundPreviewMode, ksPreviewButton.State);
            configSkillSortAxis = ezConfig.GetBindable<string>(Ez2Setting.SongSelectSkillSortAxis);
            configSkillGroupAxis = ezConfig.GetBindable<string>(Ez2Setting.SongSelectSkillGroupAxis);
            configAnalysisSystemSelection = ezConfig.GetBindable<string>(Ez2Setting.AnalysisSystemSelection);

            ruleset.BindValueChanged(_ =>
            {
                refreshSkillAxisDropdowns();
                updateCriteria();
                updateEzControlVisibility();
            });
            configAnalysisSystemSelection.BindValueChanged(_ =>
            {
                refreshSkillAxisDropdowns();
                updateCriteria();
            });
            mods.BindValueChanged(m =>
            {
                // The following is a note carried from old song select and may not be a valid reason anymore:
                // // Mods are updated once by the mod select overlay when song select is entered,
                // // regardless of if there are any mods or any changes have taken place.
                // // Updating the criteria here so early triggers a re-ordering of panels on song select, via... some mechanism.
                // // Todo: Investigate/fix and potentially remove this.
                // TODO: this might be simply removable with the new song select & carousel code.
                if (m.NewValue.SequenceEqual(m.OldValue))
                    return;

                var rulesetCriteria = currentCriteria.RulesetCriteria;
                if (sortDropdown.Current.Value is SortMode.PP or SortMode.SkillAxis || rulesetCriteria?.FilterMayChangeFromMods(currentCriteria, m) == true)
                    updateCriteria();
            });

            searchTextBox.Current.BindValueChanged(_ =>
            {
                updateCriteria();
                updateVisibleResultsActionAvailability();
            });

            searchTextBox.VisibleResultsAction = () =>
            {
                if (manageCollectionsDialog == null)
                    return;

                manageCollectionsDialog.Show();
            };

            ScheduledDelegate? sliderDebounce = null;

            // For slider dragging (where input events can arrive very often), even creating criteria can have
            // overhead, especially when a collection is selected (see ToImmutableHashSet() call).
            void debouncedUpdateCriteria()
            {
                sliderDebounce?.Cancel();
                sliderDebounce = Scheduler.AddDelayed(() => updateCriteria(), 50);
            }

            difficultyRangeSlider.LowerBound.BindValueChanged(_ => debouncedUpdateCriteria());
            difficultyRangeSlider.UpperBound.BindValueChanged(_ => debouncedUpdateCriteria());

            showConvertedBeatmapsButton.Active.BindValueChanged(_ => updateCriteria());
            sortDropdown.Current.BindValueChanged(_ =>
            {
                updateSkillAxisDropdownVisibility();
                updateCriteria();
            });
            groupDropdown.Current.BindValueChanged(_ =>
            {
                updateSkillAxisDropdownVisibility();
                updateCriteria();
            });
            sortAxisDropdown.Current.BindValueChanged(e =>
            {
                if (e.NewValue != null)
                    configSkillSortAxis.Value = formatAxisConfig(e.NewValue.System, e.NewValue.AxisId);
                updateCriteria();
            });
            groupAxisDropdown.Current.BindValueChanged(e =>
            {
                if (e.NewValue != null)
                    configSkillGroupAxis.Value = formatAxisConfig(e.NewValue.System, e.NewValue.AxisId);
                updateCriteria();
            });
            configCollectionFilter.BindValueChanged(_ => updateCriteria());
            collectionsSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>(), (_, changeSet) =>
            {
                if (changeSet != null && groupDropdown.Current.Value.Value == GroupMode.Collections)
                    updateCriteria();
            });

            localUser.BindValueChanged(_ => updateCriteria());
            localUserFavouriteBeatmapSets.BindCollectionChanged((_, _) => updateCriteria());
            ScopedBeatmapSet.BindValueChanged(_ => updateCriteria(clearScopedSet: false));

            csSelector.Current.BindValueChanged(_ => updateCriteria());
            ezAnalysisFilterButton.Active.BindValueChanged(_ => updateCriteria());

            refreshSkillAxisDropdowns();
            updateEzControlVisibility();
            updateVisibleResultsActionAvailability();
            updateCriteria();
        }

        private void updateEzControlVisibility()
        {
            int id = ruleset.Value.OnlineID;

            if (id == 3)
            {
                csSelector.Show();
                updateSkillAxisDropdownVisibility();
                return;
            }

            if (id == 1) // Taiko
            {
                csSelector.Hide();
                updateSkillAxisDropdownVisibility();
                return;
            }

            csSelector.Show();
            updateSkillAxisDropdownVisibility();
        }

        private void refreshSkillAxisDropdowns()
        {
            var axes = buildSkillAxisItems(ruleset.Value?.OnlineID ?? -1);
            sortAxisDropdown.SetAxes(axes, configSkillSortAxis.Value);
            groupAxisDropdown.SetAxes(axes, configSkillGroupAxis.Value);
            updateSkillAxisDropdownVisibility();
        }

        private void updateSkillAxisDropdownVisibility()
        {
            if (sortDropdown.Current.Value == SortMode.SkillAxis && !sortAxisDropdown.HasAxes)
                sortDropdown.Current.Value = SortMode.Title;

            if (groupDropdown.Current.Value?.Value == GroupMode.SkillAxis && !groupAxisDropdown.HasAxes)
            {
                var noneItem = groupDropdown.Items.FirstOrDefault(i => i.Value == GroupMode.None);
                if (noneItem != null)
                    groupDropdown.Current.Value = noneItem;
            }

            bool showSortAxis = sortDropdown.Current.Value == SortMode.SkillAxis && sortAxisDropdown.HasAxes;
            bool showGroupAxis = groupDropdown.Current.Value?.Value == GroupMode.SkillAxis && groupAxisDropdown.HasAxes;
            bool showRow = showSortAxis || showGroupAxis;

            skillAxisRow.Alpha = showRow ? 1 : 0;
            sortAxisDropdown.Alpha = showSortAxis ? 1 : 0;
            groupAxisDropdown.Alpha = showGroupAxis ? 1 : 0;

            if (!showSortAxis && !string.IsNullOrEmpty(configSkillSortAxis.Value) && sortDropdown.Current.Value != SortMode.SkillAxis)
                configSkillSortAxis.Value = string.Empty;

            if (!showGroupAxis && !string.IsNullOrEmpty(configSkillGroupAxis.Value) && groupDropdown.Current.Value?.Value != GroupMode.SkillAxis)
                configSkillGroupAxis.Value = string.Empty;
        }

        private static List<SkillAxisDropdownItem> buildSkillAxisItems(int rulesetOnlineId)
        {
            var items = new List<SkillAxisDropdownItem>();

            if (EzRulesetReadout.Selected(rulesetOnlineId) is not { } readout)
                return items;

            switch (readout.SkillRating)
            {
                case EzAnalysisSystem.Mina:
                    foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
                        items.Add(new SkillAxisDropdownItem(EzAnalysisSystem.Mina, axis.ToId(), axis.Meta().DisplayName));
                    break;

                case EzAnalysisSystem.PPP:
                    foreach (var spec in EzPPPSkillCatalog.MetricsFor(rulesetOnlineId))
                    {
                        if (!spec.InRadar || spec.MetricId.StartsWith("_pad", StringComparison.Ordinal))
                            continue;

                        items.Add(new SkillAxisDropdownItem(EzAnalysisSystem.PPP, spec.MetricId, spec.DisplayName));
                    }

                    break;
            }

            return items;
        }

        private static string formatAxisConfig(EzAnalysisSystem system, string axisId)
            => $"{system.ToStorageId()}:{axisId}";

        private static bool tryParseAxisConfig(string? raw, out EzAnalysisSystem system, out string axisId)
        {
            system = EzAnalysisSystem.None;
            axisId = string.Empty;

            if (string.IsNullOrEmpty(raw))
                return false;

            int separator = raw.IndexOf(':');
            if (separator <= 0 || separator >= raw.Length - 1)
                return false;

            system = EzAnalysisSystemStorage.FromStorageId(raw[..separator]);
            axisId = raw[(separator + 1)..];
            return system != EzAnalysisSystem.None && !string.IsNullOrEmpty(axisId);
        }

        private void updateVisibleResultsActionAvailability()
        {
            bool available = !string.IsNullOrWhiteSpace(searchTextBox.Current.Value);
            searchTextBox.UpdateVisibleResultsActionAvailability(available);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            collectionsSubscription?.Dispose();
        }

        /// <summary>
        /// Creates a <see cref="FilterCriteria"/> based on the current state of the controls.
        /// </summary>
        public FilterCriteria CreateCriteria()
        {
            string query = searchTextBox.Current.Value;
            bool isValidUser = localUser.Value.Id > 1;

            IEnumerable<string>? collectionBeatmapMD5Hashes = null;

            if (Guid.TryParse(configCollectionFilter.Value, out var collectionId))
                collectionBeatmapMD5Hashes = realm.Run(r => r.Find<BeatmapCollection>(collectionId)?.BeatmapMD5Hashes.ToImmutableHashSet());

            var criteria = new FilterCriteria
            {
                SelectedBeatmapSet = ScopedBeatmapSet.Value,
                Sort = sortDropdown.Current.Value,
                Group = groupDropdown.Current.Value?.Value ?? GroupMode.None,
                AllowConvertedBeatmaps = showConvertedBeatmapsButton.Active.Value,
                Ruleset = ruleset.Value,
                Mods = mods.Value,
                CollectionBeatmapMD5Hashes = collectionBeatmapMD5Hashes,
                LocalUserId = isValidUser ? localUser.Value.Id : null,
                LocalUserUsername = isValidUser ? localUser.Value.Username : null,
            };

            if (criteria.Sort == SortMode.SkillAxis && sortAxisDropdown.Current.Value is { } sortAxis)
            {
                criteria.SkillAxisSortSystem = sortAxis.System;
                criteria.SkillAxisSortId = sortAxis.AxisId;
            }

            if (criteria.Group == GroupMode.SkillAxis && groupAxisDropdown.Current.Value is { } groupAxis)
            {
                criteria.SkillAxisGroupSystem = groupAxis.System;
                criteria.SkillAxisGroupId = groupAxis.AxisId;
            }

            if (!difficultyRangeSlider.LowerBound.IsDefault)
                criteria.UserStarDifficulty.Min = difficultyRangeSlider.LowerBound.Value;

            if (!difficultyRangeSlider.UpperBound.IsDefault)
                criteria.UserStarDifficulty.Max = difficultyRangeSlider.UpperBound.Value;

            criteria.RulesetCriteria = ruleset.Value.CreateInstance().CreateRulesetFilterCriteria();

            applyCircleSizeFilter(criteria);

            FilterQueryParser.ApplyQueries(criteria, query);

            ApplyRequiredCriteria?.Invoke(criteria);

            return criteria;
        }

        private void applyCircleSizeFilter(FilterCriteria criteria) => csSelector.ApplyToFilterCriteria(criteria);

        private void updateCriteria(bool clearScopedSet = true)
        {
            if (clearScopedSet && ScopedBeatmapSet.Value != null)
            {
                songSelect?.UnscopeBeatmapSet();
                // because `ScopedBeatmapSet` has a value change callback bound to it that calls `updateCriteria()` again,
                // we can just do nothing other than clear it to avoid extra work and duplicated `CriteriaChanged` invocations
                return;
            }

            currentCriteria = CreateCriteria();
            CriteriaChanged?.Invoke(currentCriteria);
        }

        /// <summary>
        /// Set the query to the search text box.
        /// </summary>
        /// <param name="query">The string to search.</param>
        public void Search(string query)
        {
            searchTextBox.Current.Value = query;
        }

        /// <summary>
        /// Set the query to the search text box.
        /// </summary>
        /// <param name="query">The string to search.</param>
        public void AddToSearch(string query)
        {
            string existingQuery = searchTextBox.Current.Value;

            if (existingQuery.Contains(query))
                return;

            searchTextBox.Current.Value = string.IsNullOrWhiteSpace(existingQuery)
                ? query
                : string.Join(' ', existingQuery.Trim(), query);
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        internal partial class SongSelectSearchTextBox : ShearedFilterTextBox
        {
            public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            public Action? VisibleResultsAction { get; set; }

            private readonly BatchAddToCollectionButton batchAddToCollectionButton;

            public SongSelectSearchTextBox()
            {
                AddInternal(batchAddToCollectionButton = new BatchAddToCollectionButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -50,
                    Alpha = 0,
                    TooltipText = "Add all visible beatmaps to collection",
                    Action = () => VisibleResultsAction?.Invoke(),
                });
            }

            public void UpdateVisibleResultsActionAvailability(bool available)
            {
                batchAddToCollectionButton.Enabled.Value = available;
                batchAddToCollectionButton.FadeTo(available ? 1 : 0, 200, Easing.OutQuint);
            }

            protected override InnerSearchTextBox CreateInnerTextBox() => new InnerTextBox
            {
                ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
            };

            private partial class InnerTextBox : InnerFilterTextBox
            {
                public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

                public override bool HandleLeftRightArrows => false;

                public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
                {
                    if (e.Action == GlobalAction.Back && ScopedBeatmapSet.Value != null)
                        return false;

                    return base.OnPressed(e);
                }

                public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
                {
                    // Conflicts with default group navigation keys (shift-left shift-right).
                    if (e.Action == PlatformAction.SelectBackwardChar || e.Action == PlatformAction.SelectForwardChar)
                        return false;

                    // the "cut" platform key binding (shift-delete) conflicts with the beatmap deletion action.
                    if (e.Action == PlatformAction.Cut && e.ShiftPressed && e.CurrentState.Keyboard.Keys.IsPressed(Key.Delete))
                        return false;

                    return base.OnPressed(e);
                }
            }

            private partial class BatchAddToCollectionButton : OsuClickableContainer
            {
                private readonly Box background;

                private Color4 normalColour;
                private Color4 hoverColour;

                public BatchAddToCollectionButton()
                {
                    RelativeSizeAxes = Axes.Y;
                    Width = 50;
                    CornerRadius = EzUIStyleV1.CORNER_RADIUS_SHEARED;
                    Masking = true;

                    Child = background = new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    };
                }

                [BackgroundDependencyLoader]
                private void load(OsuColour colours)
                {
                    normalColour = colours.Yellow.Darken(0.45f);
                    hoverColour = colours.Yellow;

                    background.Colour = normalColour;

                    AddInternal(new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Plus,
                                Size = new Vector2(10),
                                Shear = -OsuGame.SHEAR,
                            }
                        }
                    });
                }

                protected override bool OnHover(HoverEvent e)
                {
                    background.FadeColour(hoverColour, 100, Easing.Out);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    background.FadeColour(normalColour, 100, Easing.Out);
                    base.OnHoverLost(e);
                }
            }
        }

        private partial class SortModeDropdown : ShearedDropdown<SortMode>
        {
            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            private Bindable<SortMode> configSortMode { get; } = new Bindable<SortMode>();
            private Bindable<string>? analysisSystemSelection;

            public SortModeDropdown(LocalisableString label)
                : base(label)
            {
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
            {
                config.BindWith(OsuSetting.SongSelectSortingMode, configSortMode);
                analysisSystemSelection = ezConfig.GetBindable<string>(Ez2Setting.AnalysisSystemSelection);
            }

            protected override void LoadAsyncComplete()
            {
                ruleset.BindValueChanged(_ =>
                {
                    updateAvailableItems();
                    updateCurrentFromConfig();
                }, true);
                analysisSystemSelection?.BindValueChanged(_ =>
                {
                    updateAvailableItems();
                    updateCurrentFromConfig();
                });
                configSortMode.BindValueChanged(_ => updateCurrentFromConfig());
                Current.BindValueChanged(currentChanged);

                base.LoadAsyncComplete();
            }

            private void updateAvailableItems()
            {
                bool hasSkillAxes = buildSkillAxisItems(ruleset.Value?.OnlineID ?? -1).Count > 0;
                Items = Enum.GetValues<SortMode>().Where(m => m != SortMode.SkillAxis || hasSkillAxes).ToArray();
            }

            private bool synchronisingBindables;

            private void updateCurrentFromConfig()
            {
                if (synchronisingBindables)
                    return;

                synchronisingBindables = true;
                Current.Value = Items.Contains(configSortMode.Value) ? configSortMode.Value : SortMode.Title;
                synchronisingBindables = false;
            }

            private void currentChanged(ValueChangedEvent<SortMode> current)
            {
                if (synchronisingBindables)
                    return;

                // Keep a skill-axis selection out of the upstream config when the active readout has no axes
                // (same pattern as GroupMode.Variant).
                if (current.NewValue == SortMode.SkillAxis && !Items.Contains(SortMode.SkillAxis))
                    return;

                configSortMode.Value = current.NewValue;
            }

            protected override LocalisableString GenerateItemText(SortMode item)
                => item == SortMode.SkillAxis ? EzSongSelectStrings.SKILL_AXIS : item.GetLocalisableDescription();
        }

        private partial class SkillAxisDropdown : ShearedDropdown<SkillAxisDropdownItem>
        {
            public bool HasAxes => Items.Any();

            public SkillAxisDropdown(LocalisableString label)
                : base(label)
            {
            }

            public void SetAxes(IReadOnlyList<SkillAxisDropdownItem> axes, string? preferredConfig)
            {
                Items = axes.ToArray();

                if (axes.Count == 0)
                {
                    Current.Value = null!;
                    return;
                }

                SkillAxisDropdownItem? preferred = null;

                if (tryParseAxisConfig(preferredConfig, out var system, out string axisId))
                    preferred = axes.FirstOrDefault(a => a.System == system && string.Equals(a.AxisId, axisId, StringComparison.Ordinal));

                Current.Value = preferred ?? axes[0];
            }

            protected override LocalisableString GenerateItemText(SkillAxisDropdownItem item) => item.Text;
        }

        private partial class GroupModeDropdown : ShearedDropdown<GroupModeDropdownItem>
        {
            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            private Bindable<GroupMode> configGroupMode { get; } = new Bindable<GroupMode>();
            private Bindable<string>? analysisSystemSelection;

            public GroupModeDropdown(LocalisableString label)
                : base(label)
            {
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
            {
                config.BindWith(OsuSetting.SongSelectGroupMode, configGroupMode);
                analysisSystemSelection = ezConfig.GetBindable<string>(Ez2Setting.AnalysisSystemSelection);
            }

            protected override void LoadAsyncComplete()
            {
                // Bindings set up intentionally in `LoadAsyncComplete()` rather than `LoadComplete()` as normal
                // to avoid double-filter on entering song select with the "variant" group mode engaged.
                ruleset.BindValueChanged(_ =>
                {
                    updateAvailableItems();
                    updateCurrentFromConfig();
                }, true);
                analysisSystemSelection?.BindValueChanged(_ =>
                {
                    updateAvailableItems();
                    updateCurrentFromConfig();
                });
                configGroupMode.BindValueChanged(_ => updateCurrentFromConfig());
                Current.BindValueChanged(currentChanged);

                // Ordering important - base call must run *after* the above bindings
                // because the bindings set up `Items`, and the base call is responsible for calling `GenerateItemText()`.
                base.LoadAsyncComplete();
            }

            private void updateAvailableItems()
            {
                var rulesetInstance = ruleset.Value.CreateInstance();
                var items = new List<GroupModeDropdownItem>();

                foreach (var item in Enum.GetValues<GroupMode>())
                {
                    switch (item)
                    {
                        case GroupMode.PP:
                            items.Add(new GroupModeDropdownItem(GroupMode.PP, "PP"));
                            break;

                        case GroupMode.SkillAxis:
                            if (buildSkillAxisItems(ruleset.Value?.OnlineID ?? -1).Count == 0)
                                break;

                            items.Add(new GroupModeDropdownItem(GroupMode.SkillAxis, EzSongSelectStrings.SKILL_AXIS));
                            break;

                        default:
                            items.Add(new GroupModeDropdownItem(item, item.GetLocalisableDescription()));
                            break;

                        case GroupMode.Variant:
                            if (rulesetInstance.GameplayVariants.Count() <= 1)
                                break;

                            items.Add(new GroupModeDropdownItem(GroupMode.Variant, rulesetInstance.VariantDescription));
                            break;
                    }
                }

                Items = items.ToArray();
            }

            private bool synchronisingBindables;

            private void updateCurrentFromConfig()
            {
                if (synchronisingBindables)
                    return;

                synchronisingBindables = true;
                // Only rulesets that actually have variants expose and support the "variant" grouping mode.
                // If it's missing, default to no grouping.
                Current.Value = Items.SingleOrDefault(i => i.Value == configGroupMode.Value) ?? Items.Single(i => i.Value == GroupMode.None);
                synchronisingBindables = false;
            }

            private void currentChanged(ValueChangedEvent<GroupModeDropdownItem> current)
            {
                if (synchronisingBindables)
                    return;

                // Only rulesets that actually have variants expose and support the "variant" grouping mode.
                // If it's missing, code above will revert to no grouping, which also incurs a `Current` change.
                // However, for better user experience, don't write the new value out to config in this scenario
                // so that the variant grouping is re-engaged on switching to a ruleset that has variant support
                // (this also persists across game restarts).
                if (current.OldValue.Value == GroupMode.Variant && Items.All(i => i.Value != GroupMode.Variant))
                    return;

                if (current.OldValue.Value == GroupMode.SkillAxis && Items.All(i => i.Value != GroupMode.SkillAxis))
                    return;

                synchronisingBindables = true;
                configGroupMode.Value = current.NewValue.Value;
                synchronisingBindables = false;
            }

            protected override LocalisableString GenerateItemText(GroupModeDropdownItem item) => item.Text;
        }

        private record GroupModeDropdownItem(GroupMode Value, LocalisableString Text);

        private record SkillAxisDropdownItem(EzAnalysisSystem System, string AxisId, LocalisableString Text);
    }
}
