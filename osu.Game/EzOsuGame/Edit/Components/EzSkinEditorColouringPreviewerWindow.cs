// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skinning;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Draggable colouring previewer. Same chrome and horizontal drag as
    /// <see cref="EzSkinEditorComponentPickerWindow"/>; the body is a 3×5 grid of the current note
    /// set (algorithms × ranges). Clicking a cell writes that combination into the colouring settings.
    /// </summary>
    public partial class EzSkinEditorColouringPreviewerWindow : CompositeDrawable
    {
        private const float width = 300;
        private const float snap_threshold = 24;
        private const float header_height = 30;
        private const float corner_radius = 5;
        private const float cell_width = 64;
        private const float label_width = 72;
        private const float header_row_height = 22;
        private const float cell_height_min = 48;
        private const float cell_height_max = 96;
        private const string note_folder_suffix = "note";
        private const double max_white_strength = 255;

        private static readonly EzColouringMode[] modes =
        {
            EzColouringMode.Direct,
            EzColouringMode.Tint,
            EzColouringMode.True,
        };

        public BindableBool Open { get; } = new BindableBool();

        public BindableBool Expanded { get; } = new BindableBool(true);

        public Func<float>? RightReserve { get; set; }

        public Bindable<float> RememberedX { get; }

        private readonly Bindable<string> prefix = new Bindable<string>(EzColumnResourceLookup.TINT_BASE_TEXTURE_AFFIX);

        private Box background = null!;
        private SettingsDropdown<string> prefixDropdown = null!;
        private Container gridHost = null!;
        private InputManager inputManager = null!;
        private OverlayColourProvider colourProvider = null!;
        private EzLocalTextureFactory factory = null!;
        private EzResourceStore resources = null!;
        private Bindable<string> noteSetName = null!;
        private readonly Dictionary<EzColumnType, Bindable<Colour4>> columnTypeTints = new Dictionary<EzColumnType, Bindable<Colour4>>();
        private Bindable<EzColouringMode> colouringMode = null!;
        private Bindable<EzColouringRange> colouringRange = null!;
        private Bindable<double> colouringStrength = null!;

        private PreviewCell[] cells = Array.Empty<PreviewCell>();
        private Action? onFactoryNoteColourChanged;
        private Action? onFactoryNoteDrawableChanged;
        private bool dragging;
        private bool positionRestored;

        public EzSkinEditorColouringPreviewerWindow(Bindable<float> rememberedX)
        {
            RememberedX = rememberedX;

            Width = width;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Y;
            Alpha = 0;
            Masking = true;
            AlwaysPresent = true;
        }

        public override bool PropagatePositionalInputSubTree => Open.Value;

        public override bool PropagateNonPositionalInputSubTree => Open.Value;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, EzLocalTextureFactory factory, EzResourceStore resources, Ez2ConfigManager ezConfig)
        {
            this.colourProvider = colourProvider;
            this.factory = factory;
            this.resources = resources;

            noteSetName = ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);

            foreach (EzColumnType type in Enum.GetValues<EzColumnType>())
                columnTypeTints[type] = ezConfig.GetColumnColorBindableByType(type);

            colouringMode = ezConfig.GetBindable<EzColouringMode>(Ez2Setting.ColouringMode);
            colouringRange = ezConfig.GetBindable<EzColouringRange>(Ez2Setting.ColouringRange);
            colouringStrength = ezConfig.GetBindable<double>(Ez2Setting.ColouringStrength);

            CornerRadius = corner_radius;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.1f,
                    Colour = colourProvider.Background4,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, header_height),
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
                                Height = header_height,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = EzEditorStrings.COLOURING_PREVIEWER_WINDOW_TITLE.ToUpper(),
                                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 17),
                                        Padding = new MarginPadding { Left = 10, Right = 30 },
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.Centre,
                                        Position = new Vector2(-15, 0),
                                        Icon = FontAwesome.Solid.Bars,
                                        Scale = new Vector2(0.75f),
                                        Action = () => Expanded.Toggle(),
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Depth = float.MinValue,
                                Padding = new MarginPadding { Horizontal = 0, Top = 5 },
                                Child = prefixDropdown = new SettingsDropdown<string>
                                {
                                    LabelText = EzEditorStrings.COLOURING_PREVIEWER_BASE,
                                    ShowsDefaultIndicator = false,
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Child = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    ScrollbarVisible = false,
                                    Margin = new MarginPadding { Left = 10, Right = 10, Top = 5, Bottom = 10 },
                                    Child = gridHost = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            refreshPrefixItems();
            prefixDropdown.Current = prefix;

            Open.BindValueChanged(_ =>
            {
                this.FadeTo(Open.Value ? 1 : 0, 200, Easing.OutQuint);
                requestRebuild();
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            Expanded.BindValueChanged(v =>
            {
                updateExpandedState(v.NewValue);
                requestRebuild();
            }, true);
            noteSetName.BindValueChanged(_ =>
            {
                refreshPrefixItems();
                requestRebuild();
            }, true);
            prefix.BindValueChanged(_ =>
            {
                requestRebuild();
                applyColours();
            });

            foreach (var tint in columnTypeTints.Values)
                tint.BindValueChanged(_ => applyColours());

            onFactoryNoteColourChanged = applyColours;
            factory.OnNoteColourChanged += onFactoryNoteColourChanged;
            onFactoryNoteDrawableChanged = () => Scheduler.AddOnce(applyColours);
            factory.OnNoteDrawableChanged += onFactoryNoteDrawableChanged;

            colouringMode.BindValueChanged(_ => updateSelection());
            colouringRange.BindValueChanged(_ => updateSelection());
            colouringStrength.BindValueChanged(_ =>
            {
                applyColours();
                updateSelection();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (onFactoryNoteColourChanged != null)
                    factory.OnNoteColourChanged -= onFactoryNoteColourChanged;

                if (onFactoryNoteDrawableChanged != null)
                    factory.OnNoteDrawableChanged -= onFactoryNoteDrawableChanged;
            }

            base.Dispose(isDisposing);
        }

        private void requestRebuild() => Scheduler.AddOnce(rebuildGrid);

        private void refreshPrefixItems()
        {
            var items = listPrefixes();
            string next = items.Contains(prefix.Value, StringComparer.OrdinalIgnoreCase)
                ? prefix.Value
                : items[0];

            if (!items.Contains(next, StringComparer.OrdinalIgnoreCase))
                items.Add(next);

            prefixDropdown.Items = items;
            prefix.Value = next;
        }

        private List<string> listPrefixes()
        {
            var result = new List<string>();
            string set = noteSetName.Value;

            if (!string.IsNullOrEmpty(set))
            {
                foreach (string dir in resources.ListSubdirectories($"note/{set}"))
                {
                    if (!dir.EndsWith(note_folder_suffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (dir.Equals($"long{note_folder_suffix}", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string value = dir.Substring(0, dir.Length - note_folder_suffix.Length);

                    if (value.Length == 0)
                        continue;

                    if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                        result.Add(value);
                }
            }

            if (result.Count == 0)
                result.Add(EzColumnResourceLookup.TINT_BASE_TEXTURE_AFFIX);

            result.Sort((a, b) =>
            {
                bool aWhite = a.Equals(EzColumnResourceLookup.TINT_BASE_TEXTURE_AFFIX, StringComparison.OrdinalIgnoreCase);
                bool bWhite = b.Equals(EzColumnResourceLookup.TINT_BASE_TEXTURE_AFFIX, StringComparison.OrdinalIgnoreCase);

                if (aWhite != bWhite)
                    return aWhite ? -1 : 1;

                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private void rebuildGrid()
        {
            gridHost.Clear(true);
            cells = Array.Empty<PreviewCell>();

            if (!Open.Value || !Expanded.Value)
                return;

            refreshPrefixItems();

            float ratio = factory.GetRatio();
            float cellHeight = Math.Clamp(cell_width * (ratio > 0 ? ratio : 1f), cell_height_min, cell_height_max);
            var built = new List<PreviewCell>(modes.Length * 5);

            var rowDimensions = new Dimension[6];
            rowDimensions[0] = new Dimension(GridSizeMode.Absolute, header_row_height);

            for (int i = 1; i < rowDimensions.Length; i++)
                rowDimensions[i] = new Dimension(GridSizeMode.Absolute, cellHeight);

            var content = new Drawable[6][];
            content[0] = new Drawable[4];
            content[0][0] = Empty();

            for (int column = 0; column < modes.Length; column++)
            {
                content[0][column + 1] = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    Text = modes[column].GetLocalisableDescription(),
                };
            }

            string component = EzColumnResourceLookup.FormatEzProNoteResource(prefix.Value, EzColumnResourceLookup.NOTE_COMPONENT);

            for (int row = 0; row < 5; row++)
            {
                content[row + 1] = new Drawable[4];
                content[row + 1][0] = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: 12),
                    Text = rangeLabel(row),
                };

                EzColouringRange range = row switch
                {
                    0 => EzColouringRange.All,
                    1 => EzColouringRange.Gray,
                    2 => EzColouringRange.KeepWhite,
                    _ => EzColouringRange.White,
                };
                bool maxStrength = row == 4;

                for (int column = 0; column < modes.Length; column++)
                {
                    var cell = new PreviewCell(colourProvider, modes[column], range, maxStrength, applyFromCell);
                    Drawable? note = factory.CreateAnimation(component, recolour: true);

                    if (note != null)
                    {
                        note.RelativeSizeAxes = Axes.Both;
                        note.FillMode = FillMode.Fit;
                        note.Anchor = Anchor.Centre;
                        note.Origin = Anchor.Centre;
                        cell.SetNote(note);
                    }

                    built.Add(cell);
                    content[row + 1][column + 1] = cell;
                }
            }

            cells = built.ToArray();
            gridHost.Child = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = header_row_height + cellHeight * 5,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, label_width),
                    new Dimension(GridSizeMode.Absolute, cell_width),
                    new Dimension(GridSizeMode.Absolute, cell_width),
                    new Dimension(GridSizeMode.Absolute, cell_width),
                },
                RowDimensions = rowDimensions,
                Content = content,
            };

            applyColours();
            updateSelection();
        }

        private void applyFromCell(EzColouringMode mode, EzColouringRange range, bool maxWhiteStrength)
        {
            colouringMode.Value = mode;
            colouringRange.Value = range;

            if (maxWhiteStrength)
                colouringStrength.Value = max_white_strength;
            else if (range == EzColouringRange.White && colouringStrength.Value >= max_white_strength)
                colouringStrength.SetDefault();
        }

        private bool isCellSelected(PreviewCell cell)
        {
            if (cell.Mode != colouringMode.Value || cell.Range != colouringRange.Value)
                return false;

            if (cell.Range != EzColouringRange.White)
                return true;

            bool atMax = colouringStrength.Value >= max_white_strength;
            return cell.MaxWhiteStrength == atMax;
        }

        private void updateSelection()
        {
            foreach (var cell in cells)
                cell.SetSelected(isCellSelected(cell));
        }

        private static LocalisableString rangeLabel(int row) => row switch
        {
            0 => EzEnumStrings.COLOURING_RANGE_ALL,
            1 => EzEnumStrings.COLOURING_RANGE_GRAY,
            2 => EzEnumStrings.COLOURING_RANGE_KEEP_WHITE,
            3 => EzEnumStrings.COLOURING_RANGE_WHITE,
            _ => EzEditorStrings.COLOURING_PREVIEWER_WHITE_MAX,
        };

        private Colour4 resolvePreviewTint()
        {
            if (EzColumnResourceLookup.TryGetColumnTypeForTexturePrefix(prefix.Value, out var type)
                && columnTypeTints.TryGetValue(type, out var bindable))
            {
                return bindable.Value;
            }

            return columnTypeTints[EzColumnType.B].Value;
        }

        private void applyColours()
        {
            Colour4 tint = resolvePreviewTint();
            float strength = (float)colouringStrength.Value;

            foreach (var cell in cells)
                cell.Apply(tint, strength);
        }

        protected override void Update()
        {
            base.Update();

            if (!positionRestored && Parent?.DrawWidth > 0)
            {
                positionRestored = true;
                X = fractionToX(Math.Clamp(RememberedX.Value, 0, 1));
            }

            background.FadeTo(Contains(inputManager.CurrentState.Mouse.Position) ? 1 : 0.1f, 500, Easing.OutQuint);

            if (!dragging)
                clampToBounds();
        }

        private void updateExpandedState(bool expanded)
        {
            if (expanded)
            {
                RelativeSizeAxes = Axes.Y;
                Height = 1;
            }
            else
            {
                RelativeSizeAxes = Axes.None;
                Height = header_height;
            }
        }

        private float freeTravel => Math.Max(0, (Parent?.DrawWidth ?? 0) - (RightReserve?.Invoke() ?? 0) - DrawWidth);

        private float fractionToX(float fraction) => freeTravel * fraction;

        private float xToFraction(float x) => freeTravel <= 0 ? 0 : Math.Clamp(x / freeTravel, 0, 1);

        private void clampToBounds()
        {
            if (Parent == null)
                return;

            float clamped = Math.Clamp(X, 0, freeTravel);

            if (clamped != X)
                X = clamped;
        }

        private void snapToEdge()
        {
            const float left = 0;
            float right = freeTravel;

            if (Math.Abs(X - left) <= snap_threshold)
                X = left;
            else if (Math.Abs(X - right) <= snap_threshold)
                X = right;

            clampToBounds();
            RememberedX.Value = xToFraction(X);
        }

        private bool overHeader(Vector2 screenSpacePos)
        {
            float localY = ToLocalSpace(screenSpacePos).Y;
            return localY >= 0 && localY <= header_height;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
            => e.Button == MouseButton.Left && overHeader(e.ScreenSpaceMousePosition);

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            dragging = true;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!dragging)
                return;

            X += e.Delta.X;
            clampToBounds();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (!dragging)
                return;

            dragging = false;
            snapToEdge();
        }

        private partial class PreviewCell : Container
        {
            public EzColouringMode Mode { get; }
            public EzColouringRange Range { get; }
            public bool MaxWhiteStrength { get; }

            private readonly Action<EzColouringMode, EzColouringRange, bool> onApply;
            private readonly OverlayColourProvider colourProvider;
            private readonly Container noteHost;
            private readonly Box stateBox;
            private bool selected;

            public PreviewCell(
                OverlayColourProvider colourProvider,
                EzColouringMode mode,
                EzColouringRange range,
                bool maxWhiteStrength,
                Action<EzColouringMode, EzColouringRange, bool> onApply)
            {
                this.colourProvider = colourProvider;
                this.onApply = onApply;
                Mode = mode;
                Range = range;
                MaxWhiteStrength = maxWhiteStrength;

                RelativeSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3;
                Padding = new MarginPadding(2);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.85f,
                    },
                    stateBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Highlight1,
                        Alpha = 0,
                    },
                    noteHost = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }

            public void SetNote(Drawable note) => noteHost.Child = note;

            public void Apply(Colour4 tint, float currentStrength)
            {
                float strength = MaxWhiteStrength ? (float)max_white_strength : currentStrength;
                EzNoteRecolour.ApplyToDrawable(noteHost, tint, Mode, Range, strength);
            }

            public void SetSelected(bool value)
            {
                selected = value;
                updateState();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                onApply(Mode, Range, MaxWhiteStrength);
                return true;
            }

            private void updateState()
            {
                float targetAlpha = selected ? (IsHovered ? 0.45f : 0.3f) : (IsHovered ? 0.15f : 0f);
                stateBox.FadeTo(targetAlpha, 120, Easing.OutQuint);
            }
        }
    }
}
