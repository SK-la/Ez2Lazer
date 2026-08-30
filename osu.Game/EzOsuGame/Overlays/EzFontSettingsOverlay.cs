// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Fonts;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Dialog for picking system fonts. Writes config only; applies on next launch.
    /// Visual style matches <see cref="osu.Game.Collections.ManageCollectionsDialog"/>.
    /// </summary>
    public partial class EzFontSettingsOverlay : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;
        private const float list_height = 220;
        private const double hover_font_dwell_ms = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private FillFlowContainer rows = null!;
        private OsuSpriteText statusText = null!;
        private IDisposable? duckOperation;
        private IReadOnlyList<EzSystemFontEntry> catalogEntries = Array.Empty<EzSystemFontEntry>();
        private IReadOnlyList<EzSystemFontEntry> emojiCatalogEntries = Array.Empty<EzSystemFontEntry>();
        private readonly Dictionary<string, string> lookupIdByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> registeredLookupIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> failedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<bool>> pendingFontRegistrations = new Dictionary<string, Task<bool>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<OutlineGlyphStore> dialogGlyphStores = new List<OutlineGlyphStore>();
        private readonly object fontRegisterLock = new object();
        private FontFamilyPicker? expandedPicker;
        private FontStore? dialogFonts;
        private bool dialogFontsActive;
        private int sharedLookupSerial;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private MusicController? musicController { get; set; }

        [Resolved(CanBeNull = true)]
        private SettingsOverlay? settings { get; set; }

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private FontStore fonts { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public EzFontSettingsOverlay()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.58f, 0.78f);

            Masking = true;
            CornerRadius = 10;
        }

        /// <summary>
        /// Open from settings: hide the settings sidebar first so the modal matches collection / BMS managers.
        /// </summary>
        public void ShowFromSettings()
        {
            settings?.Hide();
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
                            new Dimension(),
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
                                                    Text = EzSettingsStrings.UI_FONT_DIALOG_HEADER,
                                                    Font = OsuFont.GetFont(size: 30),
                                                },
                                                new OsuSpriteText
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    Text = EzSettingsStrings.UI_FONT_NEXT_LAUNCH,
                                                    Font = OsuFont.GetFont(size: 14),
                                                    Colour = colours.Yellow,
                                                    Margin = new MarginPadding { Top = 4 },
                                                },
                                                statusText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    Font = OsuFont.GetFont(size: 12),
                                                    Colour = colours.GreySeaFoamLighter,
                                                    Margin = new MarginPadding { Top = 2 },
                                                },
                                            }
                                        },
                                        new IconButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Icon = FontAwesome.Solid.Times,
                                            Colour = colours.GreySeaFoamDarker,
                                            Scale = new Vector2(0.8f),
                                            X = -10,
                                            Action = Hide,
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.GreySeaFoamDarker,
                                        },
                                        new OsuScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ClampExtension = 0,
                                            Padding = new MarginPadding(16),
                                            Child = rows = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 12),
                                            }
                                        }
                                    }
                                }
                            },
                        }
                    }
                }
            };

            rebuildSlots();
        }

        private void rebuildSlots()
        {
            rows.Clear();

            bool englishUi = game.CurrentLanguage.Value == Language.en;

            var uiEn = ezConfig.GetBindable<string>(Ez2Setting.UiFontDefault);
            var uiLoc = ezConfig.GetBindable<string>(Ez2Setting.UiFontDefaultLocalized);
            var titleEn = ezConfig.GetBindable<string>(Ez2Setting.UiFontTitleAlternate);
            var titleLoc = ezConfig.GetBindable<string>(Ez2Setting.UiFontTitleAlternateLocalized);
            var numeric = ezConfig.GetBindable<string>(Ez2Setting.UiFontNumeric);

            if (englishUi)
            {
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-Ui",
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiEn,
                    syncLocalized: uiLoc));
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-Title",
                    EzSettingsStrings.UI_FONT_SLOT_TITLE,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleEn,
                    syncLocalized: titleLoc));
            }
            else
            {
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-UiEn",
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT_EN,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiEn));
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-UiLoc",
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT_LOC,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiLoc));
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-TitleEn",
                    EzSettingsStrings.UI_FONT_SLOT_TITLE_EN,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleEn));
                rows.Add(new FontSlotRow(
                    this,
                    "EzPreview-TitleLoc",
                    EzSettingsStrings.UI_FONT_SLOT_TITLE_LOC,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleLoc));
            }

            rows.Add(new FontSlotRow(
                this,
                "EzPreview-Numeric",
                EzSettingsStrings.UI_FONT_SLOT_NUMERIC,
                EzSettingsStrings.UI_FONT_PREVIEW_NUMERIC,
                numeric));
            rows.Add(new FontSlotRow(
                this,
                "EzPreview-Emoji",
                EzSettingsStrings.UI_FONT_SLOT_EMOJI,
                EzSettingsStrings.UI_FONT_PREVIEW_EMOJI,
                ezConfig.GetBindable<string>(Ez2Setting.UiFontEmoji),
                noneLabel: EzSettingsStrings.UI_FONT_EMOJI_AUTO,
                emojiCatalog: true));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            duckOperation?.Dispose();
            dialogFontsActive = false;
            releaseDialogFonts();
        }

        protected override void PopIn()
        {
            dialogFontsActive = true;
            ensureDialogFontStore();

            duckOperation = musicController?.Duck(new DuckParameters
            {
                DuckVolumeTo = 1,
                DuckDuration = 100,
                RestoreDuration = 100,
            });

            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);

            if (catalogEntries.Count > 0)
            {
                applyCatalogsToRows();
                restoreDialogFontPreviews();
                statusText.Text = $"{catalogEntries.Count} / emoji {emojiCatalogEntries.Count}";
            }
            else
            {
                Schedule(loadFontListAsync);
            }
        }

        protected override void PopOut()
        {
            base.PopOut();

            duckOperation?.Dispose();
            collapseExpandedPicker();
            dialogFontsActive = false;
            releaseDialogFonts();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);

            GetContainingFocusManager()?.TriggerFocusContention(this);
        }

        /// <summary>
        /// Preview faces live in a nested FontStore with its own 4096 atlas so rapid browsing
        /// does not overflow the global UI FontStore. Released on dialog close.
        /// </summary>
        private void ensureDialogFontStore()
        {
            if (dialogFonts != null || !dialogFontsActive)
                return;

            dialogFonts = new FontStore(renderer, null, 100, TextureFilteringMode.Linear);
            fonts.AddStore(dialogFonts);
        }

        private void releaseDialogFonts()
        {
            resetDialogFontUsages();

            lock (fontRegisterLock)
            {
                foreach (var store in dialogGlyphStores)
                {
                    dialogFonts?.RemoveTextureStore(store);
                    store.Dispose();
                }

                dialogGlyphStores.Clear();
                registeredLookupIds.Clear();
                lookupIdByPath.Clear();
                pendingFontRegistrations.Clear();
            }

            if (dialogFonts != null)
            {
                fonts.RemoveStore(dialogFonts);
                fonts.EvictGlyphCache(name => name.StartsWith("EzDlg-", StringComparison.Ordinal));
                dialogFonts.ClearCache(disposeAtlas: true);
                dialogFonts.Dispose();
                dialogFonts = null;
            }
        }

        private void resetDialogFontUsages()
        {
            if (rows == null)
                return;

            foreach (var child in rows)
            {
                if (child is FontSlotRow row)
                    row.ResetDialogFonts();
            }
        }

        private void restoreDialogFontPreviews()
        {
            if (rows == null)
                return;

            foreach (var child in rows)
            {
                if (child is FontSlotRow row)
                    row.RestoreDialogFonts();
            }
        }

        private void applyCatalogsToRows()
        {
            foreach (var child in rows)
            {
                if (child is FontSlotRow row)
                    row.SetCatalog(row.UseEmojiCatalog ? emojiCatalogEntries : catalogEntries);
            }
        }

        private void loadFontListAsync()
        {
            statusText.Text = "…";

            Task.Run(() =>
                {
                    var all = EzSystemFontCatalog.GetEntries();
                    var emoji = EzSystemFontCatalog.GetEmojiEntries();
                    return (all, emoji);
                })
                .ContinueWith(t => Schedule(() =>
                {
                    if (t.IsFaulted)
                    {
                        statusText.Text = t.Exception?.GetBaseException().Message ?? "error";
                        return;
                    }

                    var result = t.GetResultSafely();
                    catalogEntries = result.all;
                    emojiCatalogEntries = result.emoji;
                    applyCatalogsToRows();
                    statusText.Text = $"{catalogEntries.Count} / emoji {emojiCatalogEntries.Count}";
                }));
        }

        private void notifyPickerExpanded(FontFamilyPicker picker)
        {
            if (expandedPicker != null && expandedPicker != picker)
                expandedPicker.SetExpanded(false);

            expandedPicker = picker.Expanded ? picker : null;
        }

        private void collapseExpandedPicker()
        {
            expandedPicker?.SetExpanded(false);
            expandedPicker = null;
        }

        /// <summary>
        /// Dialog-scoped FontStore id for a font file — shared across all slots/list rows so each face loads once.
        /// </summary>
        private string getSharedLookupId(string path)
        {
            lock (fontRegisterLock)
                return getSharedLookupIdUnlocked(path);
        }

        private string getSharedLookupIdUnlocked(string path)
        {
            if (lookupIdByPath.TryGetValue(path, out string? existing))
                return existing;

            string id = $"EzDlg-{Interlocked.Increment(ref sharedLookupSerial)}";
            lookupIdByPath[path] = id;
            return id;
        }

        private bool tryGetRegisteredLookupId(string family, out string lookupId)
        {
            lookupId = string.Empty;
            var entry = EzSystemFontCatalog.FindByFamily(family);
            if (entry == null)
                return false;

            lock (fontRegisterLock)
            {
                if (!lookupIdByPath.TryGetValue(entry.Value.Path, out string? id))
                    return false;

                if (!registeredLookupIds.Contains(id))
                    return false;

                lookupId = id;
                return true;
            }
        }

        private Task<bool> ensureFontRegisteredAsync(string path, int faceIndex)
        {
            lock (fontRegisterLock)
            {
                if (failedFontPaths.Contains(path))
                    return Task.FromResult(false);

                string lookupId = getSharedLookupIdUnlocked(path);

                if (registeredLookupIds.Contains(lookupId))
                    return Task.FromResult(true);

                if (pendingFontRegistrations.TryGetValue(path, out var pending))
                    return pending;

                var task = registerFontCoreAsync(path, lookupId, faceIndex);
                pendingFontRegistrations[path] = task;
                return task;
            }
        }

        private async Task<bool> registerFontCoreAsync(string path, string lookupId, int faceIndex)
        {
            OutlineGlyphStore? store = null;

            try
            {
                store = new OutlineGlyphStore(path, lookupId, faceIndex);
                await store.LoadFontAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                store?.Dispose();

                lock (fontRegisterLock)
                {
                    failedFontPaths.Add(path);
                    pendingFontRegistrations.Remove(path);
                }

                return false;
            }

            var tcs = new TaskCompletionSource<bool>();

            Schedule(() =>
            {
                try
                {
                    lock (fontRegisterLock)
                    {
                        if (!dialogFontsActive)
                        {
                            store.Dispose();
                            pendingFontRegistrations.Remove(path);
                            tcs.TrySetResult(false);
                            return;
                        }

                        ensureDialogFontStore();

                        if (dialogFonts == null)
                        {
                            store.Dispose();
                            pendingFontRegistrations.Remove(path);
                            tcs.TrySetResult(false);
                            return;
                        }

                        if (registeredLookupIds.Add(lookupId))
                        {
                            dialogFonts.AddTextureSource(store);
                            dialogGlyphStores.Add(store);
                        }
                        else
                            store.Dispose();

                        pendingFontRegistrations.Remove(path);
                    }

                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    store.Dispose();

                    lock (fontRegisterLock)
                    {
                        failedFontPaths.Add(path);
                        pendingFontRegistrations.Remove(path);
                    }

                    tcs.TrySetException(ex);
                }
            });

            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private static void fireAndForget(Task task)
        {
            task.ContinueWith(t =>
            {
                // Observe failures from UI fire-and-forget loads (bad system fonts, etc.).
                _ = t.Exception;
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private partial class FontSlotRow : Container
        {
            private readonly EzFontSettingsOverlay owner;
            private readonly string previewIdPrefix;
            private readonly Bindable<string> current;
            private readonly Bindable<string>? syncLocalized;
            private readonly OsuSpriteText previewText;
            private readonly FontFamilyPicker picker;
            private int previewRequestId;

            public bool UseEmojiCatalog { get; }

            public FontSlotRow(
                EzFontSettingsOverlay owner,
                string previewIdPrefix,
                LocalisableString caption,
                LocalisableString preview,
                Bindable<string> current,
                Bindable<string>? syncLocalized = null,
                LocalisableString? noneLabel = null,
                bool emojiCatalog = false)
            {
                this.owner = owner;
                this.previewIdPrefix = previewIdPrefix;
                this.current = current;
                this.syncLocalized = syncLocalized;
                UseEmojiCatalog = emojiCatalog;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                picker = new FontFamilyPicker(owner, previewIdPrefix, current, noneLabel ?? EzSettingsStrings.UI_FONT_NONE)
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                };

                previewText = new OsuSpriteText
                {
                    Text = preview,
                    Font = OsuFont.GetFont(size: 18),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Left = 8 },
                };

                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = caption,
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Relative, 0.62f),
                                new Dimension(),
                            },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    picker,
                                    previewText,
                                }
                            }
                        }
                    }
                };

                current.BindValueChanged(v =>
                {
                    syncLocalized?.Value = v.NewValue;

                    updatePreview(v.NewValue);
                }, true);
            }

            public void SetCatalog(IReadOnlyList<EzSystemFontEntry> entries) => picker.SetCatalog(entries);

            public void ResetDialogFonts()
            {
                Interlocked.Increment(ref previewRequestId);
                previewText.Font = OsuFont.GetFont(size: 18);
                picker.ResetDialogFonts();
            }

            public void RestoreDialogFonts()
            {
                updatePreview(current.Value);
                picker.RestoreDialogFonts();
            }

            private void updatePreview(string family)
            {
                int requestId = Interlocked.Increment(ref previewRequestId);

                if (string.IsNullOrEmpty(family))
                {
                    previewText.Font = OsuFont.GetFont(size: 18);
                    return;
                }

                if (owner.tryGetRegisteredLookupId(family, out string lookupId))
                {
                    previewText.Font = new FontUsage(lookupId, 18);
                    return;
                }

                var entry = EzSystemFontCatalog.FindByFamily(family);

                if (entry == null)
                {
                    previewText.Font = OsuFont.GetFont(size: 18);
                    return;
                }

                previewText.Font = OsuFont.GetFont(size: 18);
                fireAndForget(applyPreviewAsync(requestId, entry.Value.Path, entry.Value.FaceIndex));
            }

            private async Task applyPreviewAsync(int requestId, string path, int faceIndex)
            {
                bool ok = await owner.ensureFontRegisteredAsync(path, faceIndex).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (requestId != previewRequestId)
                        return;

                    previewText.Font = ok
                        ? new FontUsage(owner.getSharedLookupId(path), 18)
                        : OsuFont.GetFont(size: 18);
                });
            }
        }

        private partial class FontFamilyPicker : Container
        {
            private readonly EzFontSettingsOverlay owner;
            private readonly string previewIdPrefix;
            private readonly Bindable<string> current;
            private readonly TruncatingSpriteText headerLabel;
            private readonly Container listPanel;
            private readonly FillFlowContainer listFlow;
            private readonly OsuScrollContainer listScroll;
            private readonly SearchTextBox searchBox;
            private readonly string noneLabel;
            private IReadOnlyList<EzSystemFontEntry> catalog = Array.Empty<EzSystemFontEntry>();
            private readonly List<FontListItem> items = new List<FontListItem>();
            private FontListItem? selectedItem;

            public bool Expanded { get; private set; }

            public FontFamilyPicker(EzFontSettingsOverlay owner, string previewIdPrefix, Bindable<string> current, LocalisableString noneLabelSource)
            {
                this.owner = owner;
                this.previewIdPrefix = previewIdPrefix;
                this.current = current;
                noneLabel = noneLabelSource.ToString();

                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.25f),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new HeaderButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 36,
                                Action = toggleExpanded,
                                Child = headerLabel = new TruncatingSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Margin = new MarginPadding { Horizontal = 10 },
                                    Font = OsuFont.GetFont(size: 16),
                                }
                            },
                            listPanel = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 0,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black.Opacity(0.35f),
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding(6),
                                        Spacing = new Vector2(0, 4),
                                        Children = new Drawable[]
                                        {
                                            searchBox = new SearchTextBox
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Height = 30,
                                            },
                                            listScroll = new OsuScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Height = list_height - 46,
                                                Child = listFlow = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(0, 2),
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                searchBox.Current.BindValueChanged(_ => applyFilter(), true);
                current.BindValueChanged(_ => refreshHeader(), true);
            }

            public void SetCatalog(IReadOnlyList<EzSystemFontEntry> entries)
            {
                if (!ReferenceEquals(catalog, entries) || items.Count == 0)
                {
                    catalog = entries;
                    rebuildList();
                }
                else
                {
                    markSelected();
                }

                refreshHeader();

                if (Expanded)
                    Schedule(scrollToSelected);
            }

            public void SetExpanded(bool expanded)
            {
                if (Expanded == expanded)
                    return;

                Expanded = expanded;
                listPanel.Height = expanded ? list_height : 0;

                if (expanded)
                {
                    owner.notifyPickerExpanded(this);
                    Schedule(() =>
                    {
                        GetContainingFocusManager()?.ChangeFocus(searchBox);
                        scrollToSelected();
                    });
                }
                else if (owner.expandedPicker == this)
                {
                    owner.expandedPicker = null;
                }
            }

            public void ResetDialogFonts()
            {
                foreach (var item in items)
                    item.ResetOwnFont();

                headerLabel.Font = OsuFont.GetFont(size: 16);
            }

            public void RestoreDialogFonts()
            {
                refreshHeader();
                markSelected();
            }

            private void toggleExpanded() => SetExpanded(!Expanded);

            private void refreshHeader()
            {
                string family = current.Value;

                if (string.IsNullOrEmpty(family))
                {
                    headerLabel.Text = noneLabel;
                    headerLabel.Font = OsuFont.GetFont(size: 16);
                    return;
                }

                string display = family;

                foreach (var entry in catalog)
                {
                    if (string.Equals(entry.Family, family, StringComparison.OrdinalIgnoreCase))
                    {
                        display = entry.DisplayName;
                        break;
                    }
                }

                headerLabel.Text = display;

                if (owner.tryGetRegisteredLookupId(family, out string lookupId))
                {
                    headerLabel.Font = new FontUsage(lookupId, 16);
                    return;
                }

                headerLabel.Font = OsuFont.GetFont(size: 16);

                var resolved = EzSystemFontCatalog.FindByFamily(family);
                if (resolved == null)
                    return;

                fireAndForget(loadHeaderFontAsync(resolved.Value.Path, resolved.Value.FaceIndex, family));
            }

            private async Task loadHeaderFontAsync(string path, int faceIndex, string family)
            {
                bool ok = await owner.ensureFontRegisteredAsync(path, faceIndex).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (!ok || !string.Equals(current.Value, family, StringComparison.OrdinalIgnoreCase))
                        return;

                    headerLabel.Font = new FontUsage(owner.getSharedLookupId(path), 16);
                });
            }

            private void rebuildList()
            {
                listFlow.Clear();
                items.Clear();
                selectedItem = null;

                var noneItem = new FontListItem(owner, string.Empty, noneLabel, () => selectFamily(string.Empty));
                items.Add(noneItem);
                listFlow.Add(noneItem);

                foreach (var entry in catalog)
                {
                    var item = new FontListItem(owner, entry.Family, entry.DisplayName, () => selectFamily(entry.Family));
                    items.Add(item);
                    listFlow.Add(item);
                }

                applyFilter();
                markSelected();
            }

            private void applyFilter()
            {
                string filter = searchBox.Current.Value?.Trim() ?? string.Empty;

                foreach (var item in items)
                {
                    bool match = string.IsNullOrEmpty(filter)
                                 || item.Family.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                 || item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                 || (string.IsNullOrEmpty(item.Family) && noneLabel.Contains(filter, StringComparison.CurrentCultureIgnoreCase));

                    item.Alpha = match ? 1 : 0;
                    item.Height = match ? 28 : 0;
                }
            }

            private void selectFamily(string family)
            {
                current.Value = family;
                markSelected();
                SetExpanded(false);
            }

            private void markSelected()
            {
                selectedItem = null;

                foreach (var item in items)
                {
                    bool selected = string.Equals(item.Family, current.Value, StringComparison.OrdinalIgnoreCase);
                    item.SetSelected(selected);

                    if (selected)
                        selectedItem = item;
                }
            }

            private void scrollToSelected()
            {
                markSelected();

                if (selectedItem == null || selectedItem.Alpha < 1)
                    return;

                listScroll.ScrollIntoView(selectedItem, false);
            }

            private partial class HeaderButton : OsuClickableContainer
            {
                public HeaderButton()
                {
                    RelativeSizeAxes = Axes.X;
                }
            }
        }

        private partial class FontListItem : OsuClickableContainer
        {
            private readonly EzFontSettingsOverlay owner;
            private readonly TruncatingSpriteText label;
            private readonly Box background;
            private bool selected;
            private bool fontApplied;
            private int loadRequestId;
            private ScheduledDelegate? hoverFontLoad;

            public string Family { get; }
            public string DisplayName { get; }

            public FontListItem(EzFontSettingsOverlay owner, string family, string displayName, Action action)
            {
                this.owner = owner;
                Family = family;
                DisplayName = displayName;
                Action = action;
                TooltipText = displayName;

                RelativeSizeAxes = Axes.X;
                Height = 28;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    label = new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Margin = new MarginPadding { Horizontal = 8 },
                        Text = displayName,
                        Font = OsuFont.GetFont(size: 15),
                    }
                };

                if (!string.IsNullOrEmpty(family) && owner.tryGetRegisteredLookupId(family, out string lookupId))
                {
                    label.Font = new FontUsage(lookupId, 15);
                    fontApplied = true;
                }
            }

            public void ResetOwnFont()
            {
                hoverFontLoad?.Cancel();
                hoverFontLoad = null;
                Interlocked.Increment(ref loadRequestId);
                fontApplied = false;
                label.Font = OsuFont.GetFont(size: 15);
            }

            public void SetSelected(bool value)
            {
                selected = value;
                background.Alpha = selected ? 0.18f : 0;

                if (selected && !string.IsNullOrEmpty(Family))
                {
                    hoverFontLoad?.Cancel();
                    hoverFontLoad = null;
                    ensureOwnFont();
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!selected)
                    background.Alpha = 0.08f;

                if (!string.IsNullOrEmpty(Family) && !fontApplied)
                {
                    hoverFontLoad?.Cancel();
                    hoverFontLoad = Scheduler.AddDelayed(ensureOwnFont, hover_font_dwell_ms);
                }

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverFontLoad?.Cancel();
                hoverFontLoad = null;
                background.Alpha = selected ? 0.18f : 0;
                base.OnHoverLost(e);
            }

            private void ensureOwnFont()
            {
                if (fontApplied)
                    return;

                if (owner.tryGetRegisteredLookupId(Family, out string lookupId))
                {
                    label.Font = new FontUsage(lookupId, 15);
                    fontApplied = true;
                    return;
                }

                fireAndForget(loadOwnFontAsync());
            }

            private async Task loadOwnFontAsync()
            {
                int requestId = Interlocked.Increment(ref loadRequestId);
                var entry = EzSystemFontCatalog.FindByFamily(Family);

                if (entry == null)
                    return;

                bool ok = await owner.ensureFontRegisteredAsync(entry.Value.Path, entry.Value.FaceIndex).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (requestId != loadRequestId || !ok)
                        return;

                    label.Font = new FontUsage(owner.getSharedLookupId(entry.Value.Path), 15);
                    fontApplied = true;
                });
            }
        }

        // Legacy per-slot lookup ids removed — fonts are cached per file path for the dialog lifetime.
    }
}
