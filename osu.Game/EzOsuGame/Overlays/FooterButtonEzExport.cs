// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class FooterButtonEzExport : ScreenFooterButton, IHasPopover
    {
        private readonly Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps;
        private readonly Func<BeatmapInfo?> getSelectedBeatmap;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        public FooterButtonEzExport(Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps, Func<BeatmapInfo?> getSelectedBeatmap)
            : base(null)
        {
            this.getFilteredBeatmaps = getFilteredBeatmaps;
            this.getSelectedBeatmap = getSelectedBeatmap;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Text = FooterButtonEzExportStrings.EXPORT_BUTTON_TEXT;
            Icon = FontAwesome.Solid.Toolbox;
            AccentColour = colour.Orange1;
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new EzExportPopover(this, getFilteredBeatmaps, getSelectedBeatmap, ruleset.Value, mods.Value)
        {
            ColourProvider = colourProvider,
        };

        private partial class EzExportPopover : OsuPopover
        {
            private readonly FooterButtonEzExport footerButton;
            private readonly Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps;
            private readonly Func<BeatmapInfo?> getSelectedBeatmap;
            private readonly RulesetInfo ruleset;
            private readonly IReadOnlyList<Mod> mods;

            private FillFlowContainer buttonFlow = null!;

            [Resolved]
            private Storage storage { get; set; } = null!;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            [Resolved]
            private RealmAccess realm { get; set; } = null!;

            [Resolved]
            private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

            [Resolved(CanBeNull = true)]
            private EzManageXxySrBranchesDialog? manageXxySrBranchesDialog { get; set; }

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notifications { get; set; }

            public required OverlayColourProvider ColourProvider { get; init; }

            public EzExportPopover(FooterButtonEzExport footerButton,
                                   Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps,
                                   Func<BeatmapInfo?> getSelectedBeatmap,
                                   RulesetInfo ruleset,
                                   IReadOnlyList<Mod> mods)
                : base(false)
            {
                this.footerButton = footerButton;
                this.getFilteredBeatmaps = getFilteredBeatmaps;
                this.getSelectedBeatmap = getSelectedBeatmap;
                this.ruleset = ruleset;
                this.mods = mods;

                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Content.Padding = new MarginPadding(5);

                var filteredExporter = new VisibleBeatmapZipExporter(storage, beatmapManager, realm)
                {
                    PostNotification = notification => notifications?.Post(notification),
                };

                var selectedExporter = new SelectedBeatmapExporter(storage, beatmapManager, realm)
                {
                    PostNotification = notification => notifications?.Post(notification),
                };

                Child = buttonFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(3),
                };

                BeatmapInfo? selectedBeatmap = getSelectedBeatmap();
                string xxySrBranchContext = getXxySrBranchContext();

                addHeader(FooterButtonEzExportStrings.XXY_SR_BRANCH_HEADER, xxySrBranchContext);
                addButton(FooterButtonEzExportStrings.MANAGE_XXY_SR_BRANCHES, FontAwesome.Solid.List, openXxySrBranchManager);
                addButton(FooterButtonEzExportStrings.GENERATE_XXY_SR_BRANCH, FontAwesome.Solid.Database, () => Task.Run(generateAndActivateXxySrBranchAsync));
                addButton(FooterButtonEzExportStrings.ENABLE_XXY_SR_BRANCH, FontAwesome.Solid.Play, openXxySrBranchManagerForActivation);
                addButton(FooterButtonEzExportStrings.DEACTIVATE_XXY_SR_BRANCH, FontAwesome.Solid.PowerOff, deactivateXxySrBranch, ColourProvider.Content2);

                addHeader(FooterButtonEzExportStrings.FILTERED_RESULTS_HEADER, $"{getFilteredBeatmaps().Count} {FooterButtonEzExportStrings.BEATMAPS_UNIT}");
                addButton(FooterButtonEzExportStrings.EXPORT_FILTERED_BEATMAPS_TO_ZIP, FontAwesome.Solid.Download, () => Task.Run(() => exportFiltered(filteredExporter)));
                addButton(FooterButtonEzExportStrings.EXPORT_FILTERED_BEATMAPS_CONVERTED_TO_ZIP, FontAwesome.Solid.Download, () => Task.Run(() => exportFilteredConverted(filteredExporter)));

                if (selectedBeatmap == null)
                    return;

                buttonFlow.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 10,
                });

                addHeader(FooterButtonEzExportStrings.SELECTED_BEATMAP_HEADER, selectedBeatmap.DifficultyName);
                addButton(FooterButtonEzExportStrings.EXPORT_SELECTED_BEATMAP_AS_OSU, FontAwesome.Solid.Download,
                    () => Task.Run(() => selectedExporter.ExportBeatmapAsOsu(selectedBeatmap, ruleset, mods)));

                if (selectedBeatmap.BeatmapSet != null)
                {
                    addHeader(FooterButtonEzExportStrings.SELECTED_SET_HEADER, selectedBeatmap.BeatmapSet.ToString());
                    addButton(FooterButtonEzExportStrings.EXPORT_SELECTED_BEATMAP_SET_AS_OSZ, FontAwesome.Solid.Download,
                        () => Task.Run(() => selectedExporter.ExportBeatmapSetAsOsz(selectedBeatmap, ruleset, mods)));
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ScheduleAfterChildren(() => GetContainingFocusManager()!.ChangeFocus(this));
            }

            protected override void UpdateState(ValueChangedEvent<Visibility> state)
            {
                base.UpdateState(state);
                footerButton.OverlayState.Value = state.NewValue;
            }

            private void exportFiltered(VisibleBeatmapZipExporter exporter)
            {
                var filteredBeatmaps = getFilteredBeatmaps();
                exporter.Export("Ez2Lazer-beatmaps", filteredBeatmaps);
            }

            private void exportFilteredConverted(VisibleBeatmapZipExporter exporter)
            {
                var filteredBeatmaps = getFilteredBeatmaps();

                string exportName = BeatmapExportUtils.HasMods(mods)
                    ? $"Ez2Lazer-beatmaps-{BeatmapExportUtils.GetExportCreator(mods)}"
                    : "Ez2Lazer-beatmaps";

                exporter.ExportConverted(exportName, filteredBeatmaps, ruleset, mods);
            }

            private async Task generateAndActivateXxySrBranchAsync()
            {
                var filteredBeatmaps = getFilteredBeatmaps();

                if (filteredBeatmaps.Count == 0)
                {
                    postNotification(new SimpleErrorNotification
                    {
                        Text = FooterButtonEzExportStrings.XXY_SR_BRANCH_EMPTY_FILTER_RESULT
                    });
                    return;
                }

                var notification = new ProgressNotification
                {
                    State = ProgressNotificationState.Active,
                    Text = LocalisableString.Format(FooterButtonEzExportStrings.GENERATING_XXY_SR_BRANCH, filteredBeatmaps.Count)
                };

                postNotification(notification);

                try
                {
                    var result = await ezAnalysisCache.CreateAndActivateXxySrBranchAsync(filteredBeatmaps, ruleset, mods,
                        progress: (processed, total) => notification.Progress = total <= 0 ? 0 : (float)processed / total).ConfigureAwait(false);

                    if (!result.Success)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        postNotification(new SimpleErrorNotification
                        {
                            Text = result.Message
                        });
                        return;
                    }

                    notification.CompletionText = LocalisableString.Format(
                        FooterButtonEzExportStrings.XXY_SR_BRANCH_ACTIVATED,
                        result.DisplayName ?? string.Empty,
                        result.StoredBeatmapCount,
                        result.RequestedBeatmapCount);

                    if (!string.IsNullOrEmpty(result.DatabasePath))
                        notification.CompletionClickAction = () => storage.PresentFileExternally(result.DatabasePath);

                    notification.State = ProgressNotificationState.Completed;
                }
                catch (Exception)
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    postNotification(new SimpleErrorNotification
                    {
                        Text = FooterButtonEzExportStrings.GENERATE_XXY_SR_BRANCH_FAILED
                    });
                }
            }

            private void deactivateXxySrBranch()
            {
                if (!ezAnalysisCache.HasActiveXxySrBranch)
                {
                    postNotification(new SimpleNotification
                    {
                        Text = FooterButtonEzExportStrings.XXY_SR_BRANCH_ALREADY_INACTIVE
                    });
                    return;
                }

                ezAnalysisCache.DeactivateXxySrBranch();
                postNotification(new SimpleNotification
                {
                    Text = FooterButtonEzExportStrings.DEACTIVATED_XXY_SR_BRANCH
                });
            }

            private void openXxySrBranchManager()
            {
                if (manageXxySrBranchesDialog == null)
                {
                    postNotification(new SimpleErrorNotification
                    {
                        Text = FooterButtonEzExportStrings.XXY_SR_BRANCH_MANAGER_UNAVAILABLE
                    });
                    return;
                }

                manageXxySrBranchesDialog.ShowManager();
            }

            private void openXxySrBranchManagerForActivation()
            {
                if (manageXxySrBranchesDialog == null)
                {
                    postNotification(new SimpleErrorNotification
                    {
                        Text = FooterButtonEzExportStrings.XXY_SR_BRANCH_MANAGER_UNAVAILABLE
                    });
                    return;
                }

                manageXxySrBranchesDialog.ShowForActivation();
            }

            private string getXxySrBranchContext()
            {
                if (!ezAnalysisCache.HasActiveXxySrBranch)
                    return FooterButtonEzExportStrings.XXY_SR_BRANCH_INACTIVE;

                string context = ezAnalysisCache.ActiveXxySrBranchDisplayName.Value ?? FooterButtonEzExportStrings.XXY_SR_BRANCH_ACTIVE;

                if (!ezAnalysisCache.HasActiveXxySrBranchFor(ruleset))
                    context += $"\n{FooterButtonEzExportStrings.XXY_SR_BRANCH_RULESET_MISMATCH}";
                else if (!ezAnalysisCache.IsActiveXxySrBranchFor(ruleset, mods))
                    context += $"\n{FooterButtonEzExportStrings.XXY_SR_BRANCH_MODS_OUT_OF_SYNC}";

                return context;
            }

            private void postNotification(Notification notification)
                => footerButton.Schedule(() => notifications?.Post(notification));

            private void addHeader(LocalisableString text, string? context = null)
            {
                var textFlow = new OsuTextFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Padding = new MarginPadding(10),
                };

                textFlow.AddText(text, t => t.Font = OsuFont.Default.With(weight: FontWeight.SemiBold));

                if (context != null)
                {
                    textFlow.NewLine();
                    textFlow.AddText(context, t =>
                    {
                        t.Colour = ColourProvider.Content2;
                        t.Font = t.Font.With(size: 13);
                    });
                }

                buttonFlow.Add(textFlow);
            }

            private void addButton(LocalisableString text, IconUsage? icon, Action? action, Color4? colour = null)
            {
                var button = new OptionButton
                {
                    Text = text,
                    Icon = icon ?? new IconUsage(),
                    BackgroundColour = ColourProvider.Background3,
                    TextColour = colour,
                    Action = () =>
                    {
                        Scheduler.AddDelayed(Hide, 50);
                        action?.Invoke();
                    },
                };

                buttonFlow.Add(button);
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (e.ControlPressed) return false;

                if (!e.Repeat && e.Key >= Key.Number1 && e.Key <= Key.Number9)
                {
                    int requested = e.Key - Key.Number1;

                    OptionButton? found = buttonFlow.Children.OfType<OptionButton>().ElementAtOrDefault(requested);

                    if (found != null)
                    {
                        found.TriggerClick();
                        return true;
                    }
                }

                return base.OnKeyDown(e);
            }

            private partial class OptionButton : OsuButton
            {
                public IconUsage Icon { get; init; }
                public Color4? TextColour { get; init; }

                public OptionButton()
                {
                    Size = new Vector2(265, 50);
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    SpriteText.Colour = TextColour ?? Color4.White;
                    Content.CornerRadius = 10;

                    Add(new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(17),
                        X = 15,
                        Icon = Icon,
                        Colour = TextColour ?? Color4.White,
                    });
                }

                protected override SpriteText CreateText() => new OsuSpriteText
                {
                    Depth = -1,
                    Origin = Anchor.CentreLeft,
                    Anchor = Anchor.CentreLeft,
                    X = 40
                };
            }
        }

        private static class FooterButtonEzExportStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_BUTTON_TEXT = new EzLocalizationManager.EzLocalisableString("Ez 导出", "Ez Export");

            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_HEADER = new EzLocalizationManager.EzLocalisableString("xxySR 分支库", "xxySR Branch SQLite");
            internal static readonly EzLocalizationManager.EzLocalisableString MANAGE_XXY_SR_BRANCHES = new EzLocalizationManager.EzLocalisableString("管理分支库", "Manage Branch SQLite Files");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_XXY_SR_BRANCH = new EzLocalizationManager.EzLocalisableString("生成分支库", "Generate Branch SQLite");
            internal static readonly EzLocalizationManager.EzLocalisableString ENABLE_XXY_SR_BRANCH = new EzLocalizationManager.EzLocalisableString("启用分支库", "Enable Branch SQLite");
            internal static readonly EzLocalizationManager.EzLocalisableString DEACTIVATE_XXY_SR_BRANCH = new EzLocalizationManager.EzLocalisableString("停用分支库", "Deactivate Branch SQLite");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_EMPTY_FILTER_RESULT = new EzLocalizationManager.EzLocalisableString("当前筛选结果为空，未生成 xxySR 分支库。", "The current filtered result is empty. No xxySR branch sqlite was generated.");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATING_XXY_SR_BRANCH = new EzLocalizationManager.EzLocalisableString("正在生成 xxySR 分支库（{0} 张谱面）...", "Generating xxySR branch sqlite ({0} beatmaps)...");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_ACTIVATED = new EzLocalizationManager.EzLocalisableString("xxySR 分支库已启用：{0}（写入 {1}/{2} 张谱面）。", "xxySR branch sqlite activated: {0} ({1}/{2} beatmaps stored).");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_XXY_SR_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("生成 xxySR 分支库失败。", "Failed to generate xxySR branch sqlite.");
            internal static readonly EzLocalizationManager.EzLocalisableString DEACTIVATED_XXY_SR_BRANCH = new EzLocalizationManager.EzLocalisableString("已停用当前 xxySR 分支库。", "The current xxySR branch sqlite has been deactivated.");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_ALREADY_INACTIVE = new EzLocalizationManager.EzLocalisableString("当前没有已启用的 xxySR 分支库。", "There is no active xxySR branch sqlite.");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_MANAGER_UNAVAILABLE = new EzLocalizationManager.EzLocalisableString("分支库管理器当前不可用。", "The branch sqlite manager is currently unavailable.");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_INACTIVE = new EzLocalizationManager.EzLocalisableString("当前未启用", "Currently inactive");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_ACTIVE = new EzLocalizationManager.EzLocalisableString("当前已启用", "Currently active");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_RULESET_MISMATCH = new EzLocalizationManager.EzLocalisableString("当前 ruleset 与分支库不一致", "Current ruleset does not match this branch");
            internal static readonly EzLocalizationManager.EzLocalisableString XXY_SR_BRANCH_MODS_OUT_OF_SYNC = new EzLocalizationManager.EzLocalisableString("当前 mods 已偏离建库时状态，但列表仍按分支库显示", "Current mods differ from the branch build state, but the list is still driven by the branch sqlite");

            internal static readonly EzLocalizationManager.EzLocalisableString FILTERED_RESULTS_HEADER = new EzLocalizationManager.EzLocalisableString("筛选结果", "Filtered Results");
            internal static readonly EzLocalizationManager.EzLocalisableString BEATMAPS_UNIT = new EzLocalizationManager.EzLocalisableString("张谱面", "beatmaps");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_FILTERED_BEATMAPS_TO_ZIP = new EzLocalizationManager.EzLocalisableString("导出筛选谱面到 .zip", "Export Beatmaps to .zip");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_FILTERED_BEATMAPS_CONVERTED_TO_ZIP = new EzLocalizationManager.EzLocalisableString("导出筛选 Mods 转谱到 .zip", "Convert Beatmaps with active mods to .zip");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_BEATMAP_HEADER = new EzLocalizationManager.EzLocalisableString("选中谱面", "Selected Beatmap");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_SELECTED_BEATMAP_AS_OSU = new EzLocalizationManager.EzLocalisableString("导出 Mods 转谱为 .osu", "Export selected beatmap converted with active mods as .osu");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_SET_HEADER = new EzLocalizationManager.EzLocalisableString("选中谱包", "Selected Set");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_SELECTED_BEATMAP_SET_AS_OSZ = new EzLocalizationManager.EzLocalisableString("导出 Mods 转谱为 .osz", "Export selected beatmap set converted with active mods as .osz");
        }
    }
}
