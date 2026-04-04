using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Extensions;
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
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzManageXxySrBranchesDialog : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private IDisposable? duckOperation;
        private bool activationEntryRequested;
        private string? selectedDatabasePath;

        private BasicSearchTextBox searchTextBox = null!;
        private OsuTextFlowContainer subtitleText = null!;
        private TruncatingSpriteText selectedBranchText = null!;
        private FillFlowContainer listFlow = null!;
        private RoundedButton activateButton = null!;
        private DangerousRoundedButton deleteButton = null!;

        private IReadOnlyList<EzAnalysisPersistentStore.XxySrBranchDescriptor> displayedBranches = Array.Empty<EzAnalysisPersistentStore.XxySrBranchDescriptor>();

        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private MusicController? musicController { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public EzManageXxySrBranchesDialog()
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
                                                    Text = EzManageXxySrBranchesDialogStrings.TITLE,
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
                                                        activateButton = new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageXxySrBranchesDialogStrings.ACTIVATE_SELECTED_BRANCH,
                                                            Action = activateSelectedBranch,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageXxySrBranchesDialogStrings.OPEN_BRANCH_DIRECTORY,
                                                            Action = openBranchDirectory,
                                                        },
                                                        deleteButton = new DangerousRoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 48,
                                                            Text = EzManageXxySrBranchesDialogStrings.DELETE_SELECTED_BRANCH,
                                                            Action = confirmDeleteSelectedBranch,
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

            searchTextBox.Current.BindValueChanged(_ => refreshBranches());
            ruleset.BindValueChanged(_ => refreshBranches());
            mods.BindValueChanged(_ => refreshBranches());
            ezAnalysisCache.ActiveXxySrBranchVersion.BindValueChanged(_ => refreshBranches());
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
                ? EzManageXxySrBranchesDialogStrings.ACTIVATION_SUBTITLE
                : EzManageXxySrBranchesDialogStrings.MANAGER_SUBTITLE);
        }

        private void refreshBranches()
        {
            if (!IsLoaded)
                return;

            IReadOnlyList<EzAnalysisPersistentStore.XxySrBranchDescriptor> availableBranches = ezAnalysisCache.GetAvailableXxySrBranches(ruleset.Value, mods.Value);
            string searchTerm = searchTextBox.Current.Value?.Trim() ?? string.Empty;

            displayedBranches = string.IsNullOrWhiteSpace(searchTerm)
                ? availableBranches
                : availableBranches.Where(branch => matchesSearch(branch, searchTerm)).ToList();

            string? activePath = ezAnalysisCache.ActiveXxySrBranchPath.Value;

            if (displayedBranches.All(branch => !string.Equals(branch.DatabasePath, selectedDatabasePath, StringComparison.OrdinalIgnoreCase)))
            {
                string? activeBranchPath = displayedBranches.FirstOrDefault(branch => string.Equals(branch.DatabasePath, activePath, StringComparison.OrdinalIgnoreCase)).DatabasePath;
                selectedDatabasePath = activeBranchPath ?? displayedBranches.FirstOrDefault().DatabasePath;
            }

            listFlow.Clear();

            if (displayedBranches.Count == 0)
            {
                selectedDatabasePath = null;
                listFlow.Add(new EmptyStateText
                {
                    Text = string.IsNullOrWhiteSpace(searchTerm)
                        ? EzManageXxySrBranchesDialogStrings.NO_BRANCHES_AVAILABLE
                        : EzManageXxySrBranchesDialogStrings.NO_BRANCHES_MATCHING_SEARCH,
                });
            }
            else
            {
                foreach (var branch in displayedBranches)
                {
                    listFlow.Add(new BranchListItem(branch)
                    {
                        IsSelected = string.Equals(branch.DatabasePath, selectedDatabasePath, StringComparison.OrdinalIgnoreCase),
                        IsActive = string.Equals(branch.DatabasePath, activePath, StringComparison.OrdinalIgnoreCase),
                        SelectAction = selectBranch,
                        RenameAction = renameBranch,
                    });
                }
            }

            updateSelectedBranchState();
        }

        private static bool matchesSearch(EzAnalysisPersistentStore.XxySrBranchDescriptor branch, string searchTerm)
        {
            return branch.Metadata.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                   || branch.RelativePath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                   || branch.Metadata.ModsDisplay.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }

        private void selectBranch(EzAnalysisPersistentStore.XxySrBranchDescriptor branch)
        {
            selectedDatabasePath = branch.DatabasePath;
            refreshBranches();
        }

        private void activateSelectedBranch()
        {
            var selectedBranch = getSelectedBranch();

            if (selectedBranch == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageXxySrBranchesDialogStrings.SELECT_BRANCH_FIRST,
                });
                return;
            }

            activateBranch(selectedBranch.Value.DatabasePath);
        }

        private void activateBranch(string databasePath)
        {
            var selectedBranch = displayedBranches.FirstOrDefault(branch => string.Equals(branch.DatabasePath, databasePath, StringComparison.OrdinalIgnoreCase));

            if (!ezAnalysisCache.TryActivateXxySrBranch(databasePath, out LocalisableString message))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                return;
            }

            tryApplyBranchMods(selectedBranch.Metadata);

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();

            if (activationEntryRequested)
                Hide();
        }

        private void tryApplyBranchMods(EzAnalysisPersistentStore.XxySrBranchMetadata metadata)
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

        private void renameBranch(EzAnalysisPersistentStore.XxySrBranchDescriptor branch, string newDisplayName)
        {
            if (branch.Metadata.DisplayName == newDisplayName)
                return;

            if (!ezAnalysisCache.TryRenameXxySrBranch(branch.DatabasePath, newDisplayName, out LocalisableString message, out var renamedBranch))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                refreshBranches();
                return;
            }

            selectedDatabasePath = renamedBranch.DatabasePath;

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();
        }

        private void confirmDeleteSelectedBranch()
        {
            var selectedBranch = getSelectedBranch();

            if (selectedBranch == null)
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = EzManageXxySrBranchesDialogStrings.SELECT_BRANCH_FIRST,
                });
                return;
            }

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteBranchDialog(selectedBranch.Value.Metadata.DisplayName, () => deleteBranch(selectedBranch.Value.DatabasePath)));
                return;
            }

            deleteBranch(selectedBranch.Value.DatabasePath);
        }

        private void deleteBranch(string databasePath)
        {
            if (!ezAnalysisCache.TryDeleteXxySrBranch(databasePath, out LocalisableString message))
            {
                postNotification(new SimpleErrorNotification
                {
                    Text = message,
                });
                refreshBranches();
                return;
            }

            if (string.Equals(selectedDatabasePath, databasePath, StringComparison.OrdinalIgnoreCase))
                selectedDatabasePath = null;

            postNotification(new SimpleNotification
            {
                Text = message,
            });

            refreshBranches();
        }

        private void openBranchDirectory()
            => storage.GetStorageForDirectory(EzAnalysisPersistentStore.XXY_SR_BRANCH_DATABASE_DIRECTORY).PresentExternally();

        private EzAnalysisPersistentStore.XxySrBranchDescriptor? getSelectedBranch()
        {
            foreach (var branch in displayedBranches)
            {
                if (string.Equals(branch.DatabasePath, selectedDatabasePath, StringComparison.OrdinalIgnoreCase))
                    return branch;
            }

            return null;
        }

        private void updateSelectedBranchState()
        {
            var selectedBranch = getSelectedBranch();
            bool hasSelection = selectedBranch != null;

            activateButton.Enabled.Value = hasSelection;
            deleteButton.Enabled.Value = hasSelection;

            if (selectedBranch is EzAnalysisPersistentStore.XxySrBranchDescriptor branch)
                selectedBranchText.Text = LocalisableString.Format(EzManageXxySrBranchesDialogStrings.SELECTED_BRANCH, branch.Metadata.DisplayName);
            else
                selectedBranchText.Text = EzManageXxySrBranchesDialogStrings.NO_BRANCH_SELECTED;
        }

        private void postNotification(Notification notification)
            => Schedule(() => notifications?.Post(notification));

        private partial class BranchListItem : OsuClickableContainer
        {
            private readonly EzAnalysisPersistentStore.XxySrBranchDescriptor branch;

            private Box background = null!;
            private NameTextBox nameTextBox = null!;

            public bool IsSelected { get; init; }

            public bool IsActive { get; init; }

            public Action<EzAnalysisPersistentStore.XxySrBranchDescriptor>? SelectAction { get; init; }

            public Action<EzAnalysisPersistentStore.XxySrBranchDescriptor, string>? RenameAction { get; init; }

            public BranchListItem(EzAnalysisPersistentStore.XxySrBranchDescriptor branch)
            {
                this.branch = branch;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 10;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = IsSelected ? colours.BlueDarker : colours.GreySeaFoam,
                            Alpha = IsSelected ? 0.9f : 0.35f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 4,
                            Colour = colours.Yellow,
                            Alpha = IsActive ? 1 : 0,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Vertical = 10, Horizontal = 14 },
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                nameTextBox = new NameTextBox
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 36,
                                    CommitOnFocusLost = true,
                                    SelectAllOnFocus = true,
                                    Text = branch.Metadata.DisplayName,
                                    FocusedAction = () => SelectAction?.Invoke(branch),
                                },
                                new OsuSpriteText
                                {
                                    Text = createDetailsText(),
                                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Medium),
                                    Colour = IsActive ? colours.Yellow : colours.BlueLight,
                                },
                                new OsuSpriteText
                                {
                                    Text = branch.RelativePath,
                                    Font = OsuFont.Default.With(size: 12),
                                    Colour = colours.GreySeaFoamLighter,
                                }
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                nameTextBox.OnCommit += (_, __) =>
                {
                    string trimmed = nameTextBox.Text.Trim();

                    if (trimmed == branch.Metadata.DisplayName)
                        return;

                    RenameAction?.Invoke(branch, trimmed);
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                SelectAction?.Invoke(branch);
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(1, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(IsSelected ? 0.9f : 0.35f, 100, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            private string createDetailsText()
            {
                string activeText = IsActive ? $"{EzManageXxySrBranchesDialogStrings.ACTIVE_BRANCH_BADGE} · " : string.Empty;
                return $"{activeText}{branch.Metadata.ModsDisplay} · {branch.Metadata.BeatmapCount:#,0} diff";
            }
        }

        private partial class NameTextBox : OsuTextBox
        {
            public Action? FocusedAction { get; init; }

            protected override float LeftRightPadding => 14;

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundUnfocused = colours.GreySeaFoamDarker.Darken(0.3f);
                BackgroundFocused = colours.GreySeaFoam;
            }

            protected override void OnFocus(FocusEvent e)
            {
                FocusedAction?.Invoke();
                base.OnFocus(e);
            }
        }

        private partial class EmptyStateText : OsuSpriteText
        {
            public EmptyStateText()
            {
                Margin = new MarginPadding(20);
                Font = OsuFont.Default.With(size: 18, weight: FontWeight.Medium);
                Colour = Color4.White.Opacity(0.7f);
            }
        }

        private partial class DeleteBranchDialog : DeletionDialog
        {
            public DeleteBranchDialog(string displayName, Action deleteAction)
            {
                BodyText = LocalisableString.Format(EzManageXxySrBranchesDialogStrings.DELETE_BRANCH_CONFIRMATION, displayName);
                DangerousAction = deleteAction;
            }
        }

        private static class EzManageXxySrBranchesDialogStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString TITLE = new EzLocalizationManager.EzLocalisableString("xxySR 分支库管理", "xxySR Branch Manager");
            internal static readonly EzLocalizationManager.EzLocalisableString MANAGER_SUBTITLE = new EzLocalizationManager.EzLocalisableString("选中分支库后，点击深色名称框可直接重命名；下方可启用、打开目录或删除。", "Select a branch, click the dark name box to rename it directly, then use the buttons below to activate, open the directory, or delete it.");
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATION_SUBTITLE = new EzLocalizationManager.EzLocalisableString("先选中要启用的分支库；点击深色名称框可直接重命名，启用请使用下方按钮。", "Select the branch to activate first. Click the dark name box to rename it directly, then use the button below to activate it.");
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATE_SELECTED_BRANCH = new EzLocalizationManager.EzLocalisableString("启用选中分支库", "Activate Selected Branch");
            internal static readonly EzLocalizationManager.EzLocalisableString OPEN_BRANCH_DIRECTORY = new EzLocalizationManager.EzLocalisableString("打开分支库目录", "Open Branch Directory");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_SELECTED_BRANCH = new EzLocalizationManager.EzLocalisableString("删除选中分支库", "Delete Selected Branch");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_BRANCHES_AVAILABLE = new EzLocalizationManager.EzLocalisableString("当前没有可用分支库。", "No branch sqlite files are available.");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_BRANCHES_MATCHING_SEARCH = new EzLocalizationManager.EzLocalisableString("没有匹配的分支库。", "No branch sqlite files match the current search.");
            internal static readonly EzLocalizationManager.EzLocalisableString SELECT_BRANCH_FIRST = new EzLocalizationManager.EzLocalisableString("请先选择一个分支库。", "Select a branch sqlite first.");
            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_BRANCH = new EzLocalizationManager.EzLocalisableString("已选中：{0}", "Selected: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_BRANCH_SELECTED = new EzLocalizationManager.EzLocalisableString("未选中分支库", "No branch sqlite selected");
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVE_BRANCH_BADGE = new EzLocalizationManager.EzLocalisableString("当前启用", "Active");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_BRANCH_CONFIRMATION = new EzLocalizationManager.EzLocalisableString("删除分支库：{0}", "Delete branch sqlite: {0}");
        }
    }
}
