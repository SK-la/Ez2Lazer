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
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class FooterButtonEzUtils : ScreenFooterButton, IHasPopover
    {
        private readonly Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps;
        private readonly Func<BeatmapInfo?> getSelectedBeatmap;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        public FooterButtonEzUtils(Func<IReadOnlyList<BeatmapInfo>> getFilteredBeatmaps, Func<BeatmapInfo?> getSelectedBeatmap)
            : base(null)
        {
            this.getFilteredBeatmaps = getFilteredBeatmaps;
            this.getSelectedBeatmap = getSelectedBeatmap;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Text = FooterButtonEzUtilsStrings.BUTTON_TEXT;
            Icon = FontAwesome.Solid.Toolbox;
            AccentColour = colour.Orange1;
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new EzUtilsPopover(this, getFilteredBeatmaps, getSelectedBeatmap, ruleset.Value, mods.Value)
        {
            ColourProvider = colourProvider,
        };

        private partial class EzUtilsPopover : OsuPopover
        {
            private readonly FooterButtonEzUtils footerButton;
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

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notifications { get; set; }

            public required OverlayColourProvider ColourProvider { get; init; }

            public EzUtilsPopover(FooterButtonEzUtils footerButton,
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
            private void load(OsuColour colours)
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

                addHeader(FooterButtonEzUtilsStrings.FILTERED_RESULTS_HEADER, $"{getFilteredBeatmaps().Count} {FooterButtonEzUtilsStrings.BEATMAPS_UNIT}");
                addButton(FooterButtonEzUtilsStrings.EXPORT_FILTERED_BEATMAPS_TO_ZIP, FontAwesome.Solid.Download, () => Task.Run(() => exportFiltered(filteredExporter)));
                addButton(FooterButtonEzUtilsStrings.EXPORT_FILTERED_BEATMAPS_CONVERTED_TO_ZIP, FontAwesome.Solid.Download, () => Task.Run(() => exportFilteredConverted(filteredExporter)));

                if (selectedBeatmap == null)
                    return;

                buttonFlow.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 10,
                });

                addHeader(FooterButtonEzUtilsStrings.SELECTED_BEATMAP_HEADER, selectedBeatmap.DifficultyName);
                addButton(FooterButtonEzUtilsStrings.EXPORT_SELECTED_BEATMAP_AS_OSU, FontAwesome.Solid.Download,
                    () => Task.Run(() => selectedExporter.ExportBeatmapAsOsu(selectedBeatmap, ruleset, mods)));

                if (selectedBeatmap.BeatmapSet != null)
                {
                    addHeader(FooterButtonEzUtilsStrings.SELECTED_SET_HEADER, selectedBeatmap.BeatmapSet.ToString());
                    addButton(FooterButtonEzUtilsStrings.EXPORT_SELECTED_BEATMAP_SET_AS_OSZ, FontAwesome.Solid.Download,
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

        private static class FooterButtonEzUtilsStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString BUTTON_TEXT = new EzLocalizationManager.EzLocalisableString("Ez 工具", "Ez Utils");

            internal static readonly EzLocalizationManager.EzLocalisableString FILTERED_RESULTS_HEADER = new EzLocalizationManager.EzLocalisableString("筛选结果", "Filtered Results");
            internal static readonly EzLocalizationManager.EzLocalisableString BEATMAPS_UNIT = new EzLocalizationManager.EzLocalisableString("张谱面", "beatmaps");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_FILTERED_BEATMAPS_TO_ZIP = new EzLocalizationManager.EzLocalisableString("导出筛选谱面到 .zip", "Export Beatmaps to .zip");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_FILTERED_BEATMAPS_CONVERTED_TO_ZIP = new EzLocalizationManager.EzLocalisableString("导出筛选 Mods 转谱到 .zip", "Convert Beatmaps with active mods to .zip");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_BEATMAP_HEADER = new EzLocalizationManager.EzLocalisableString("选中谱面", "Selected Beatmap");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_SELECTED_BEATMAP_AS_OSU = new EzLocalizationManager.EzLocalisableString("导出选中谱面为 .osu", "Export selected Beatmap as .osu");

            internal static readonly EzLocalizationManager.EzLocalisableString SELECTED_SET_HEADER = new EzLocalizationManager.EzLocalisableString("选中谱包", "Selected Set");
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_SELECTED_BEATMAP_SET_AS_OSZ = new EzLocalizationManager.EzLocalisableString("导出选中谱包为 .osz", "Export selected BeatmapSet as .osz");
        }
    }
}
