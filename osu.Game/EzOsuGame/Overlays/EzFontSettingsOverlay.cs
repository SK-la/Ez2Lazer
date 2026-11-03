// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Fonts;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

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

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private FillFlowContainer? rows;
        private OsuSpriteText statusText = null!;
        private IDisposable? duckOperation;
        private IReadOnlyList<EzSystemFontEntry> catalogEntries = Array.Empty<EzSystemFontEntry>();
        private IReadOnlyList<EzSystemFontEntry> emojiCatalogEntries = Array.Empty<EzSystemFontEntry>();
        private readonly HashSet<string> failedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Faces currently loaded into <see cref="dialogFonts"/>, keyed by font file path. A face stays loaded while at
        /// least one <see cref="FontLease"/> holds it, which keeps the resident face count proportional to what is on
        /// screen rather than to everything the user has scrolled past.
        /// </summary>
        private readonly Dictionary<string, FaceLease> activeFaces = new Dictionary<string, FaceLease>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Every lease handed out for this dialog, so their held families can be dropped once the dialog store is torn
        /// down on close.
        /// </summary>
        private readonly HashSet<FontLease> activeLeases = new HashSet<FontLease>();

        private readonly Lock fontRegisterLock = new Lock();
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

        // 弹窗挂在 topMostOverlayContent，取不到 SettingsPanel 缓存的 provider，下拉表头/列表会退化成纯黑。
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

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
            CornerRadius = EzUIStyleV1.CORNER_RADIUS_WEDGE;
        }

        /// <summary>
        /// Open from settings: hide the settings sidebar first so the modal matches collection / BMS managers.
        /// </summary>
        public void ShowFromSettings()
        {
            settings?.Hide();
            Show();
        }

        private OsuColour colours = null!;
        private bool slotsBuilt;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            // The slot/picker tree is only needed once the dialog is opened, so it is built on first PopIn.
            // This keeps the startup path free of ~40 drawables that are not visible on the first screen.
            this.colours = colours;
        }

        private bool ensureSlotsBuilt()
        {
            if (slotsBuilt)
                return true;

            if (!IsLoaded)
                return false;

            slotsBuilt = true;
            buildSlots();
            return true;
        }

        private void buildSlots()
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
                                    CornerRadius = EzUIStyleV1.CORNER_RADIUS_WEDGE,
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
            if (rows == null)
                return;

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
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiEn,
                    syncLocalized: uiLoc));
                rows.Add(new FontSlotRow(
                    this,
                    EzSettingsStrings.UI_FONT_SLOT_TITLE,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleEn,
                    syncLocalized: titleLoc));
            }
            else
            {
                rows.Add(new FontSlotRow(
                    this,
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT_EN,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiEn));
                rows.Add(new FontSlotRow(
                    this,
                    EzSettingsStrings.UI_FONT_SLOT_DEFAULT_LOC,
                    EzSettingsStrings.UI_FONT_PREVIEW_DEFAULT,
                    uiLoc));
                rows.Add(new FontSlotRow(
                    this,
                    EzSettingsStrings.UI_FONT_SLOT_TITLE_EN,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleEn));
                rows.Add(new FontSlotRow(
                    this,
                    EzSettingsStrings.UI_FONT_SLOT_TITLE_LOC,
                    EzSettingsStrings.UI_FONT_PREVIEW_TITLE,
                    titleLoc));
            }

            rows.Add(new FontSlotRow(
                this,
                EzSettingsStrings.UI_FONT_SLOT_NUMERIC,
                EzSettingsStrings.UI_FONT_PREVIEW_NUMERIC,
                numeric));
            rows.Add(new FontSlotRow(
                this,
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
            // Build the slot tree on first open; PopIn may be invoked before it exists.
            if (!ensureSlotsBuilt())
            {
                Schedule(PopIn);
                return;
            }

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
            collapsePickerMenus();
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
                foreach (var face in activeFaces.Values)
                    dialogFonts?.RemoveTextureStore(face.Store);

                activeFaces.Clear();
            }

            if (dialogFonts != null)
            {
                // The atlas owns every glyph texture, so disposing it releases the faces' memory in one go.
                fonts.RemoveStore(dialogFonts);
                fonts.EvictGlyphCache(name => name.StartsWith("EzDlg-", StringComparison.Ordinal));
                dialogFonts.ClearCache(disposeAtlas: true);
                dialogFonts.Dispose();
                dialogFonts = null;
            }

            // The store is gone, so no lease can still hold a face: drop their held families without releasing again.
            foreach (var lease in activeLeases.ToArray())
                lease.Invalidate();
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

        private void collapsePickerMenus()
        {
            // PopOut runs from LoadAsyncComplete, before the slot tree exists.
            if (rows == null)
                return;

            foreach (var child in rows)
            {
                if (child is FontSlotRow row)
                    row.CollapsePicker();
            }
        }

        private void applyCatalogsToRows()
        {
            if (rows == null)
                return;

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
                    restoreDialogFontPreviews();
                    statusText.Text = $"{catalogEntries.Count} / emoji {emojiCatalogEntries.Count}";
                }));
        }

        /// <summary>
        /// Leases the dialog face for a system family. The first lease of a file pays the FreeType load; later leases
        /// reuse it. Returns null when the family is unknown, already failed, or the dialog is closed.
        /// </summary>
        /// <remarks>Must be called on the update thread. Every non-null result must be balanced by <see cref="releaseFace"/>.</remarks>
        private Task<string?>? leaseFace(string family)
        {
            if (string.IsNullOrWhiteSpace(family) || !dialogFontsActive)
                return null;

            var entry = EzSystemFontCatalog.FindByFamily(family);

            if (entry == null)
                return null;

            lock (fontRegisterLock)
            {
                if (failedFontPaths.Contains(entry.Value.Path))
                    return null;

                if (!activeFaces.TryGetValue(entry.Value.Path, out var face))
                    activeFaces[entry.Value.Path] = face = new FaceLease($"EzDlg-{Interlocked.Increment(ref sharedLookupSerial)}", entry.Value.Path);

                face.RefCount++;

                return face.LoadTask ??= loadFaceAsync(entry.Value.Path, entry.Value.FaceIndex);
            }
        }

        private async Task<string?> loadFaceAsync(string path, int faceIndex)
        {
            FaceLease? lease;

            lock (fontRegisterLock)
            {
                if (!activeFaces.TryGetValue(path, out lease))
                    return null;
            }

            var store = new OutlineGlyphStore(path, lease.LookupId, faceIndex);

            try
            {
                await store.LoadFontAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return await discardFaceAsync(path, lease, store).ConfigureAwait(false);
            }

            return await handOverToDialog(lease, store).ConfigureAwait(false);
        }

        /// <summary>
        /// Records an unloadable face and disposes the store it built.
        /// </summary>
        private Task<string?> discardFaceAsync(string path, FaceLease lease, OutlineGlyphStore store)
        {
            lock (fontRegisterLock)
            {
                failedFontPaths.Add(path);

                if (activeFaces.TryGetValue(path, out var current) && ReferenceEquals(current, lease))
                    activeFaces.Remove(path);
            }

            store.Dispose();
            return Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Hands a loaded face over to the dialog on the update thread, which ends in the face being either taken over
        /// by <see cref="attachFace"/> or disposed by it.
        /// </summary>
        /// <remarks>
        /// A disposed overlay refuses the schedule, so the face is abandoned here in that case. A handover that is
        /// scheduled but never runs (the overlay closed in the meantime) leaves the face to its finalizer, which closes
        /// the FreeType handle.
        /// </remarks>
        private Task<string?> handOverToDialog(FaceLease lease, OutlineGlyphStore store)
        {
            var tcs = new TaskCompletionSource<string?>();

            try
            {
                // The lambda owns the store from here on, so no captured variable is disposed in this scope.
                Schedule(() => tcs.TrySetResult(attachFace(lease, store)));
            }
            catch (Exception)
            {
                return abandonFace(store);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Drops a face that was never handed over. Nothing else holds it, so disposing off the update thread is safe.
        /// </summary>
        private static Task<string?> abandonFace(OutlineGlyphStore store)
        {
            store.Dispose();
            return Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Takes ownership of <paramref name="store"/> when the lease still wants it, and disposes it otherwise.
        /// Runs on the update thread.
        /// </summary>
        private string? attachFace(FaceLease lease, OutlineGlyphStore store)
        {
            lock (fontRegisterLock)
            {
                if (dialogFontsActive
                    && activeFaces.TryGetValue(lease.Path, out var current)
                    && ReferenceEquals(current, lease))
                {
                    ensureDialogFontStore();

                    if (dialogFonts != null)
                    {
                        lease.Store = store;
                        dialogFonts.AddTextureSource(store);
                        return lease.LookupId;
                    }
                }
            }

            store.Dispose();
            return null;
        }

        /// <summary>
        /// Drops one lease on a family. The face is unloaded once the last lease is gone.
        /// </summary>
        /// <remarks>Must be called on the update thread.</remarks>
        private void releaseFace(string? family)
        {
            if (string.IsNullOrWhiteSpace(family))
                return;

            var entry = EzSystemFontCatalog.FindByFamily(family);

            if (entry == null)
                return;

            FaceLease? released = null;

            lock (fontRegisterLock)
            {
                if (!activeFaces.TryGetValue(entry.Value.Path, out var face))
                    return;

                if (--face.RefCount > 0)
                    return;

                activeFaces.Remove(entry.Value.Path);
                released = face;
            }

            var store = released.Store;

            // A face still loading has no store yet; its own load continuation disposes what it built.
            if (store == null)
                return;

            dialogFonts?.RemoveTextureStore(store);
            fonts.EvictGlyphCache(name => name == released.LookupId);
            released.Store = null;
            store.Dispose();
        }

        private bool tryGetRegisteredLookupId(string family, out string lookupId)
        {
            lookupId = string.Empty;

            var entry = EzSystemFontCatalog.FindByFamily(family);

            if (entry == null)
                return false;

            lock (fontRegisterLock)
            {
                if (!activeFaces.TryGetValue(entry.Value.Path, out var face) || face.Store == null)
                    return false;

                lookupId = face.LookupId;
                return true;
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

        /// <summary>
        /// A system face loaded into the dialog store, kept alive by the number of leases on it.
        /// </summary>
        private sealed class FaceLease
        {
            public readonly string LookupId;
            public readonly string Path;

            public OutlineGlyphStore? Store;
            public Task<string?>? LoadTask;
            public int RefCount;

            public FaceLease(string lookupId, string path)
            {
                LookupId = lookupId;
                Path = path;
            }
        }

        /// <summary>
        /// Holds one dialog face for one visual target, so a target's typeface follows its family without every target
        /// loading a face of its own. Long-lived targets (slot previews, dropdown headers) hold a lease while they show
        /// a family; viewport-driven targets (menu rows) hold one only while they are on screen.
        /// </summary>
        private sealed class FontLease
        {
            private readonly EzFontSettingsOverlay owner;
            private readonly Action<FontUsage> applyFont;
            private readonly float size;
            private string? heldFamily;
            private int requestId;

            public FontLease(EzFontSettingsOverlay owner, Action<FontUsage> applyFont, float size)
            {
                this.owner = owner;
                this.applyFont = applyFont;
                this.size = size;

                owner.activeLeases.Add(this);
            }

            /// <summary>
            /// Leases <paramref name="family"/> and applies the resulting face. An empty family applies the fallback.
            /// </summary>
            public void Set(string? family)
            {
                family ??= string.Empty;

                if (string.Equals(heldFamily, family, StringComparison.OrdinalIgnoreCase))
                    return;

                int id = Interlocked.Increment(ref requestId);

                owner.releaseFace(heldFamily);
                heldFamily = family;

                Task<string?>? lease = owner.leaseFace(family);

                if (lease == null)
                {
                    applyFallbackFont();
                    return;
                }

                if (lease.IsCompletedSuccessfully)
                {
                    apply(lease.GetResultSafely());
                    return;
                }

                applyFallbackFont();
                fireAndForget(applyWhenLoadedAsync(id, lease));
            }

            /// <summary>
            /// Re-leases the held family. Used when the dialog font store was released and rebuilt underneath a target
            /// whose family never changed.
            /// </summary>
            public void Reapply()
            {
                string? family = heldFamily;
                Clear();

                if (family != null)
                    Set(family);
            }

            /// <summary>Drops the lease and goes back to the fallback face.</summary>
            public void Clear()
            {
                if (heldFamily == null)
                    return;

                Interlocked.Increment(ref requestId);
                owner.releaseFace(heldFamily);
                heldFamily = null;
                applyFallbackFont();
            }

            /// <summary>
            /// Drops the held family without releasing it, for when the dialog store has already been torn down.
            /// </summary>
            public void Invalidate()
            {
                Interlocked.Increment(ref requestId);
                heldFamily = null;
                applyFallbackFont();
            }

            public void Dispose() => owner.activeLeases.Remove(this);

            private async Task applyWhenLoadedAsync(int id, Task<string?> lease)
            {
                string? lookupId;

                try
                {
                    lookupId = await lease.ConfigureAwait(false);
                }
                catch
                {
                    lookupId = null;
                }

                owner.Schedule(() =>
                {
                    if (Volatile.Read(ref requestId) != id)
                        return;

                    apply(lookupId);
                });
            }

            private void apply(string? lookupId)
            {
                if (string.IsNullOrEmpty(lookupId))
                {
                    applyFallbackFont();
                    return;
                }

                applyFont(new FontUsage(lookupId, size));
            }

            private void applyFallbackFont() => applyFont(OsuFont.GetFont(size: size));
        }

        private partial class FontSlotRow : Container
        {
            private readonly FontLease previewLease;
            private readonly FontFamilyPicker picker;

            public bool UseEmojiCatalog { get; }

            public FontSlotRow(
                EzFontSettingsOverlay owner,
                LocalisableString caption,
                LocalisableString preview,
                Bindable<string> current,
                Bindable<string>? syncLocalized = null,
                LocalisableString? noneLabel = null,
                bool emojiCatalog = false)
            {
                UseEmojiCatalog = emojiCatalog;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                picker = new FontFamilyPicker(owner, current, (noneLabel ?? EzSettingsStrings.UI_FONT_NONE).ToString())
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                };

                var previewText1 = new OsuSpriteText
                {
                    Text = preview,
                    Font = OsuFont.GetFont(size: 18),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Left = 8 },
                };

                previewLease = new FontLease(owner, font => previewText1.Font = font, 18);

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
                                    previewText1,
                                }
                            }
                        }
                    }
                };

                current.BindValueChanged(v =>
                {
                    syncLocalized?.Value = v.NewValue;
                    previewLease.Set(v.NewValue);
                }, true);
            }

            public void SetCatalog(IReadOnlyList<EzSystemFontEntry> entries) => picker.SetCatalog(entries);

            public void ResetDialogFonts()
            {
                previewLease.Clear();
                picker.ReleaseDialogFonts();
            }

            public void RestoreDialogFonts()
            {
                previewLease.Reapply();
                picker.RestoreDialogFonts();
            }

            public void CollapsePicker() => picker.Collapse();

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                previewLease.Dispose();
            }
        }

        private partial class FontFamilyPicker : OsuDropdown<string>
        {
            private readonly Bindable<string> current;
            private readonly string noneLabel;
            private readonly BindableList<string> families = new BindableList<string>();

            /// <summary>
            /// The picker's own selection, kept separate from config so rebuilding the item list can never write a
            /// transient value (the framework auto-selects the first item whenever the current value matches nothing).
            /// </summary>
            private readonly Bindable<string> selection = new Bindable<string>(string.Empty);

            private IReadOnlyList<EzSystemFontEntry> catalog = Array.Empty<EzSystemFontEntry>();
            private bool rebuildingItems;

            public FontFamilyPicker(EzFontSettingsOverlay owner, Bindable<string> current, string noneLabelSource)
            {
                this.current = current;
                noneLabel = noneLabelSource;

                RelativeSizeAxes = Axes.X;

                ((FontFamilyHeader)Header).Initialise(owner, selection);
                ((FontFamilyMenu)Menu).Initialise(owner);

                AlwaysShowSearchBar = true;

                Current.BindTo(selection);
                selection.BindValueChanged(v =>
                {
                    if (!rebuildingItems)
                        this.current.Value = v.NewValue;
                });
                this.current.BindValueChanged(v => applySelection(v.NewValue), true);

                ItemSource = families;
            }

            protected override DropdownHeader CreateHeader() => new FontFamilyHeader();

            protected override DropdownMenu CreateMenu() => new FontFamilyMenu();

            protected override LocalisableString GenerateItemText(string item)
            {
                if (string.IsNullOrEmpty(item))
                    return noneLabel;

                foreach (var entry in catalog)
                {
                    if (string.Equals(entry.Family, item, StringComparison.OrdinalIgnoreCase))
                        return entry.DisplayName;
                }

                return item;
            }

            public void SetCatalog(IReadOnlyList<EzSystemFontEntry> entries)
            {
                if (ReferenceEquals(catalog, entries))
                    return;

                catalog = entries;

                rebuildingItems = true;

                try
                {
                    families.Clear();
                    families.Add(string.Empty);

                    foreach (var entry in entries)
                        families.Add(entry.Family);
                }
                finally
                {
                    rebuildingItems = false;
                }

                applySelection(current.Value);
            }

            public void ReleaseDialogFonts() => ((FontFamilyHeader)Header).ReleaseFace();

            public void RestoreDialogFonts() => ((FontFamilyHeader)Header).RestoreFace();

            public void Collapse() => Menu.State = MenuState.Closed;

            /// <summary>
            /// Points the picker at <paramref name="family"/>, keeping an unavailable saved family selectable rather
            /// than letting the framework rewrite the config to the first item.
            /// </summary>
            private void applySelection(string? family)
            {
                family ??= string.Empty;

                bool missing = !families.Any(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase));

                if (missing)
                {
                    rebuildingItems = true;

                    try
                    {
                        families.Add(family);
                    }
                    finally
                    {
                        rebuildingItems = false;
                    }
                }

                if (!string.Equals(selection.Value, family, StringComparison.OrdinalIgnoreCase))
                    selection.Value = family;
            }

            public partial class FontFamilyHeader : OsuDropdownHeader
            {
                private FontLease? lease;

                public void Initialise(EzFontSettingsOverlay owner, Bindable<string> family)
                {
                    lease = new FontLease(owner, font => Text.Font = font, 16);
                    family.BindValueChanged(v => lease.Set(v.NewValue), true);
                }

                public void ReleaseFace() => lease?.Clear();

                public void RestoreFace() => lease?.Reapply();

                protected override void Dispose(bool isDisposing)
                {
                    base.Dispose(isDisposing);
                    lease?.Dispose();
                }
            }

            public partial class FontFamilyMenu : OsuDropdownMenu
            {
                private EzFontSettingsOverlay? owner;

                public FontFamilyMenu()
                {
                    MaxHeight = list_height;

                    StateChanged += state =>
                    {
                        bool open = state == MenuState.Open;

                        foreach (var item in Children.OfType<FontPreviewMenuItem>())
                            item.LoadingActive = open;
                    };
                }

                public void Initialise(EzFontSettingsOverlay fontOwner) => owner = fontOwner;

                protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item)
                {
                    var drawable = new FontPreviewMenuItem(owner!, item)
                    {
                        LoadingActive = State == MenuState.Open,
                    };

                    return drawable;
                }

                public partial class FontPreviewMenuItem : DrawableOsuDropdownMenuItem
                {
                    private readonly FontLease lease;
                    private Content? labelContent;
                    private bool loadingActive;

                    public FontPreviewMenuItem(EzFontSettingsOverlay owner, MenuItem item)
                        : base(item)
                    {
                        lease = new FontLease(owner, applyFont, 15);
                    }

                    public string Family => ((DropdownMenuItem<string>)Item).Value;

                    /// <summary>
                    /// Whether this row should hold its face. Driven by the menu state, so rows release as soon as the
                    /// menu closes.
                    /// </summary>
                    public bool LoadingActive
                    {
                        get => loadingActive;
                        set
                        {
                            if (loadingActive == value)
                                return;

                            loadingActive = value;
                            updateLease();
                        }
                    }

                    protected override Drawable CreateContent() => labelContent = (Content)base.CreateContent();

                    protected override void Update()
                    {
                        base.Update();

                        if (loadingActive)
                            updateLease();
                    }

                    /// <summary>
                    /// Holds a face only while this row is inside the scroll viewport, so a long list does not load a
                    /// face per entry. Repeats the framework's masking test, which is not visible outside the
                    /// framework assembly.
                    /// </summary>
                    private void updateLease()
                    {
                        bool onScreen = loadingActive && Precision.AlmostIntersects(ComputeMaskingBounds(), ScreenSpaceDrawQuad.AABBFloat);

                        if (onScreen)
                            lease.Set(Family);
                        else
                            lease.Clear();
                    }

                    private void applyFont(FontUsage font)
                    {
                        labelContent?.Label.Font = font;
                    }

                    protected override void Dispose(bool isDisposing)
                    {
                        base.Dispose(isDisposing);
                        lease.Dispose();
                    }
                }
            }
        }
    }
}
