// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzUserModSelectOverlay
    {
        private const float pool_height = 96;
        private const float column_padding_rest = 10;
        private float bottomWidth => 110;

        private static readonly Key[][] hotkey_rows =
        {
            new[] { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P },
            new[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L },
            new[] { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M },
        };

        private readonly Bindable<bool> editing = new BindableBool();
        private readonly List<EzModGroupColumn> groupColumns = new List<EzModGroupColumn>();
        private readonly List<ModState> hiddenStates = new List<ModState>();

        private EzConfigStore configStore = null!;
        private EzModHiddenPool pool = null!;
        private Container hiddenPoolArea = null!;
        private ShearedButton editButton = null!;
        private ShearedButton newGroupButton = null!;
        private EzModLayoutShearControl shearControl = null!;
        private FillFlowContainer editToolbar = null!;
        private DragGhost dragGhost = null!;
        private Box insertionMarker = null!;
        private OsuSpriteText emptyResults = null!;
        internal EzModLayoutModeButton LayoutModeButton { get; private set; } = null!;

        public EzModSelectLayoutMode LayoutMode { get; private set; }
        private readonly double[] scrollPositions = new double[2];
        private ModSelectContent? restoreScrollView;
        private double restoreScrollAt;
        private int restoreScrollFrames;
        private bool dragging => draggingAcronym != null || draggingColumn != null;

        private EzRulesetModLayout? working;
        private string? workingRulesetKey;
        private string? draggingAcronym;
        private EzModGroupColumn? draggingColumn;
        private Vector2 lastDragPosition;
        private bool applyScheduled;

        private string modLayoutRulesetKey =>
            string.IsNullOrEmpty(Ruleset.Value?.ShortName) ? "osu" : Ruleset.Value.ShortName;

        [BackgroundDependencyLoader]
        private void loadLayout(EzConfigStore modLayoutStore)
        {
            configStore = modLayoutStore;
            loadEzSongSelectKeyboardHandler();
        }

        private protected override IEnumerable<ColumnDimContainer> CreateColumns()
        {
            if (!ShowPresets)
                yield break;

            yield return new ColumnDimContainer(new ModPresetColumn
            {
                Margin = new MarginPadding { Right = 10 },
            });
        }

        protected override void AssignModsToColumns() => Scheduler.AddOnce(assignModsToColumns);

        private void assignModsToColumns()
        {
            string key = modLayoutRulesetKey;

            if (working != null && workingRulesetKey != key)
            {
                if (editing.Value && workingRulesetKey != null)
                    configStore.SaveModLayout(workingRulesetKey, working);

                editing.Value = false;
                working = null;
            }

            var entries = new List<EzModLayoutMod>();
            var states = collectStates(entries);
            EzRulesetModLayout? source = editing.Value && working != null ? working : configStore.GetModLayout(modLayoutRulesetKey);

            working = EzModLayoutResolver.Resolve(source, entries, preserveUnavailable: true);
            workingRulesetKey = key;
            applyLayout(states);
        }

        public override void DeselectAll()
        {
            base.DeselectAll();

            foreach (var state in hiddenStates)
            {
                if (state.Active.Value)
                    state.Active.Value = false;
            }
        }

        public override void Hide()
        {
            if (dragging)
                cancelDrag();
            if (editing.Value)
                finishEditing();

            base.Hide();
        }

        protected override void LoadComplete()
        {
            MainAreaContent.Add(hiddenPoolArea = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -1,
                Child = pool = new EzModHiddenPool
                {
                    DragStarted = beginModDrag,
                    Dragged = moveDrag,
                    DragEnded = endModDrag,
                },
            });

            MainAreaContent.Add(dragGhost = new DragGhost());
            MainAreaContent.Add(insertionMarker = new Box
            {
                Alpha = 0,
                Depth = float.MinValue,
                Colour = Colour4.White
            });
            MainAreaContent.Add(emptyResults = new OsuSpriteText
            {
                Text = EzModLayoutStrings.NO_RESULTS,
                Font = OsuFont.TorusAlternate.With(size: 20),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            });

            editToolbar = new FillFlowContainer
            {
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                AutoSizeAxes = Axes.X,
                Height = 42,
                Position = new Vector2(312, 0),
                Children = matchToolbarHeight(new Drawable[]
                {
                    editButton = new ShearedButton
                    {
                        Text = EzModLayoutStrings.EDIT,
                        Width = bottomWidth,
                        TextSize = 16,
                        Action = toggleEditing,
                    },
                    newGroupButton = new ShearedButton
                    {
                        Text = EzModLayoutStrings.NEW_GROUP,
                        Width = bottomWidth,
                        TextSize = 16,
                        Alpha = 0,
                        Action = addGroup,
                    },
                    LayoutModeButton = new EzModLayoutModeButton
                    {
                        Alpha = 0
                    },
                    shearControl = new EzModLayoutShearControl
                    {
                        Width = 200,
                        Alpha = 0
                    },
                }),
            };

            AddAboveColumns(editToolbar);

            LayoutMode = configStore.ModSelectLayoutMode;
            if (LayoutMode == EzModSelectLayoutMode.Vertical)
                ReplaceContentView(new EzModSelectVerticalContent(ShowPresets));

            updateModeButton();

            base.LoadComplete();

            LayoutModeButton.Active.BindValueChanged(change => SetLayoutMode(change.NewValue ? EzModSelectLayoutMode.Vertical : EzModSelectLayoutMode.Horizontal));

            shearControl.Current.BindValueChanged(change =>
            {
                if (working == null)
                    return;

                working.ColumnShear = (float)change.NewValue;
                SetEzColumnFlowShearX(working.ColumnShear.Value);
            });

            editing.BindValueChanged(change =>
            {
                editButton.Text = change.NewValue ? EzModLayoutStrings.DONE : EzModLayoutStrings.EDIT;
                newGroupButton.Alpha = change.NewValue ? 1 : 0;
                LayoutModeButton.Alpha = change.NewValue ? 1 : 0;
                LayoutModeButton.Enabled.Value = change.NewValue && !dragging;
                shearControl.Alpha = change.NewValue ? 1 : 0;
                SetColumnFlowBottomPadding(column_padding_rest);

                if (change.NewValue)
                {
                    shearControl.Current.Value = EzModLayoutResolver.NormaliseColumnShear(working?.ColumnShear);
                    pool.MoveToY(16);
                    pool.FadeIn(180);
                    pool.MoveToY(0, 220, Easing.OutQuint);
                }
                else
                {
                    pool.MoveToY(16, 160, Easing.InQuint).FadeOut(140);
                }
            }, true);

            SearchTextBox.Current.BindValueChanged(term =>
            {
                pool.SearchTerm = term.NewValue;
                restoreScrollView = null;
                Array.Clear(scrollPositions);
                ContentView.RestoreScroll(0);
                RefreshSearchPreselection();
            }, true);

            Ruleset.BindValueChanged(change =>
            {
                cancelDrag();

                foreach (var column in groupColumns)
                    column.CommitRename();

                if (editing.Value && working != null && workingRulesetKey != modLayoutRulesetKey)
                {
                    string oldKey = string.IsNullOrEmpty(change.OldValue?.ShortName) ? "osu" : change.OldValue.ShortName;
                    configStore.SaveModLayout(oldKey, working);
                }

                editing.Value = false;

                if (workingRulesetKey != modLayoutRulesetKey)
                {
                    working = null;
                    workingRulesetKey = null;
                }
                Array.Clear(scrollPositions);
                restoreScrollView = null;
                ContentView.RestoreScroll(0);
            });
        }

        internal void SetLayoutMode(EzModSelectLayoutMode mode)
        {
            if (mode == LayoutMode || dragging)
                return;

            foreach (var column in groupColumns)
                column.CommitRename();

            ContentView.FlushPendingSelections();
            scrollPositions[(int)LayoutMode] = ContentView.ScrollPosition;
            LayoutMode = mode;

            ReplaceContentView(mode == EzModSelectLayoutMode.Vertical
                ? new EzModSelectVerticalContent(ShowPresets)
                : new HorizontalModSelectContent(CreateColumns()));

            if (working == null)
                AssignModsToColumns();
            else
                applyLayout(collectStates(null));

            foreach (var column in ContentView.Columns)
                column.SearchTerm = SearchTerm;

            ContentView.Colour = CustomisationPanel.ExpandedState.Value == ModCustomisationPanel.ModCustomisationPanelState.Collapsed
                ? Colour4.White
                : OsuColour.Gray(0.5f);
            SetColumnFlowBottomPadding(column_padding_rest);
            updateModeButton();
            configStore.SaveModSelectLayoutMode(mode);

            restoreScrollView = ContentView;
            restoreScrollAt = scrollPositions[(int)mode];
            restoreScrollFrames = 0;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (restoreScrollView != ContentView || !groupColumns.All(column => column.IsLoaded && column.ItemsLoaded))
                return;

            if (++restoreScrollFrames < 3)
                return;

            ContentView.RestoreScroll(restoreScrollAt);
            restoreScrollView = null;
            RefreshSearchPreselection();
        }

        private void cancelDrag()
        {
            draggingAcronym = null;
            draggingColumn = null;
            hideGhost();
        }

        private void updateModeButton() => LayoutModeButton.Active.Value = LayoutMode == EzModSelectLayoutMode.Vertical;

        private static Drawable[] matchToolbarHeight(Drawable[] children)
        {
            foreach (var child in children)
            {
                child.RelativeSizeAxes |= Axes.Y;
                child.Height = 1;
            }

            return children;
        }

        protected override void Update()
        {
            base.Update();

            float availableWidth = AboveColumnsContent.DrawWidth - AboveColumnsContent.Padding.TotalHorizontal;
            float remainingWidth = availableWidth - editToolbar.DrawWidth - 28;
            CustomisationPanel.Width = Math.Clamp(remainingWidth * 400 / 700, 220, 400);
            SearchTextBox.Width = Math.Clamp(remainingWidth - CustomisationPanel.Width, 160, 300);
            float headerWidth = SearchTextBox.Width + 12 + editToolbar.DrawWidth + 16 + CustomisationPanel.Width;
            float headerScale = Math.Min(1, Math.Max(1, availableWidth) / headerWidth);
            SearchTextBox.Scale = editToolbar.Scale = CustomisationPanel.Scale = new Vector2(headerScale);
            editToolbar.Position = new Vector2((SearchTextBox.Width + 12) * headerScale, 0);
            CustomisationPanel.Y = 0;
            CustomisationPanel.RelativeSizeAxes = Axes.None;

            float footerTop = DisplayedFooterContent != null
                ? MainAreaContent.ToLocalSpace(DisplayedFooterContent.ScreenSpaceDrawQuad.TopLeft).Y
                : MainAreaContent.DrawHeight - MainAreaContent.Padding.Bottom;
            float footerClearance = Math.Max(0, MainAreaContent.DrawHeight - MainAreaContent.Padding.Bottom - footerTop + AboveColumnsContent.Padding.Bottom);
            float contentBottom = footerClearance + (editing.Value ? pool_height + 16 : 16);
            hiddenPoolArea.Padding = new MarginPadding { Bottom = footerClearance };
            CustomisationPanel.Height = Math.Max(0, MainAreaContent.DrawHeight - MainAreaContent.Padding.TotalVertical - contentBottom) / headerScale;
            float contentTop = Math.Max(SearchTextBox.DrawHeight, editToolbar.DrawHeight) * headerScale + 36;
            SetContentAreaPadding(new MarginPadding { Top = contentTop, Bottom = contentBottom });
            LayoutModeButton.Enabled.Value = editing.Value && !dragging;
            emptyResults.Alpha = !editing.Value && SearchTerm.Length > 0
                && groupColumns.All(column => column.AvailableMods.All(state => !state.Visible))
                && ContentView.Columns.OfType<ModPresetColumn>().All(column => !column.HasMatchingPresets)
                ? 1
                : 0;

            if (dragging)
            {
                ContentView.AutoScroll(lastDragPosition, Time.Elapsed);
                updateInsertionMarker();
            }
        }

        private void toggleEditing()
        {
            if (editing.Value)
                finishEditing();
            else
                editing.Value = true;
        }

        private void finishEditing()
        {
            if (!editing.Value)
                return;

            editing.Value = false;
            saveWorking();
        }

        private void addGroup()
        {
            if (!editing.Value || working == null)
                return;

            EzModLayoutResolver.AddGroup(working, EzModLayoutStrings.NEW_GROUP);
            scheduleApply();
        }

        private void saveWorking()
        {
            if (working == null)
                return;

            var entries = new List<EzModLayoutMod>();
            collectStates(entries);
            working = EzModLayoutResolver.Resolve(working, entries, preserveUnavailable: true);
            configStore.SaveModLayout(modLayoutRulesetKey, working);
        }

        private Dictionary<string, ModState> collectStates(List<EzModLayoutMod>? entries)
        {
            var states = new Dictionary<string, ModState>(StringComparer.OrdinalIgnoreCase);

            foreach (var state in AllAvailableMods)
            {
                if (state.Mod.Type == ModType.System || !state.Mod.HasImplementation || string.IsNullOrEmpty(state.Mod.Acronym))
                    continue;

                if (!states.TryAdd(state.Mod.Acronym, state))
                    continue;

                entries?.Add(new EzModLayoutMod(state.Mod.Acronym, state.Mod.Type));
            }

            return states;
        }

        private void applyLayout(Dictionary<string, ModState> states)
        {
            if (working == null)
                return;

            working.EnsureMutableLists();

            hiddenStates.Clear();

            foreach (string acronym in working.Hidden)
            {
                if (states.TryGetValue(acronym, out var state))
                    hiddenStates.Add(state);
            }

            groupColumns.Clear();
            var next = new List<ModColumn>();
            int hotkeyIndex = 0;

            foreach (var group in working.Groups)
            {
                group.EnsureMutableLists();
                var mods = new List<ModState>();

                foreach (string acronym in group.Acronyms)
                {
                    if (states.TryGetValue(acronym, out var state))
                        mods.Add(state);
                }

                Key[]? keys = hotkeyIndex < hotkey_rows.Length ? hotkey_rows[hotkeyIndex] : null;
                hotkeyIndex++;

                bool vertical = LayoutMode == EzModSelectLayoutMode.Vertical;
                var column = new EzModGroupColumn(group, editing, keys, vertical)
                {
                    Margin = new MarginPadding { Right = vertical ? 0 : 10 },
                    SearchTerm = SearchTextBox.Current.Value,
                    PanelDragStarted = (panel, position) => beginModDrag(panel.Mod.Acronym, position),
                    PanelDragged = moveDrag,
                    PanelDragEnded = endModDrag,
                    ColumnDragStarted = beginColumnDrag,
                    ColumnDragged = moveDrag,
                    ColumnDragEnded = endColumnDrag,
                    DeleteRequested = deleteGroup,
                    RenameRequested = renameGroup,
                };

                column.AvailableMods = mods;
                groupColumns.Add(column);
                next.Add(column);
            }

            ReplaceModColumns(next);
            pool.SetMods(hiddenStates);
            applyWorkingColumnShear();
        }

        private void applyWorkingColumnShear()
        {
            float shear = EzModLayoutResolver.NormaliseColumnShear(working?.ColumnShear);
            SetEzColumnFlowShearX(shear);

            if (!editing.Value)
                return;

            shearControl.Current.Value = shear;
        }

        private void scheduleApply()
        {
            if (applyScheduled)
                return;

            applyScheduled = true;
            Scheduler.Add(() =>
            {
                applyScheduled = false;

                if (working == null)
                    return;

                applyLayout(collectStates(null));
            });
        }

        private void beginModDrag(string acronym, Vector2 screenPosition)
        {
            draggingColumn = null;
            draggingAcronym = acronym;
            showGhost(acronym, screenPosition);
        }

        private void beginColumnDrag(EzModGroupColumn column, Vector2 screenPosition)
        {
            draggingAcronym = null;
            draggingColumn = column;
            showGhost(column.GroupName, screenPosition);
        }

        private void moveDrag(Vector2 screenPosition)
        {
            lastDragPosition = screenPosition;
            dragGhost.Position = toContentSpace(screenPosition);
        }

        private void endModDrag()
        {
            string? acronym = draggingAcronym;
            Vector2 position = lastDragPosition;
            draggingAcronym = null;
            hideGhost();

            if (acronym == null || working == null)
                return;

            if (pool.ReceivePositionalInputAt(position))
            {
                EzModLayoutResolver.MoveMod(working, acronym, null, 0);
                scheduleApply();
                return;
            }

            foreach (var column in groupColumns)
            {
                if (!column.ScreenSpaceDrawQuad.Contains(position))
                    continue;

                EzModLayoutResolver.MoveMod(working, acronym, column.GroupId, column.InsertionIndex(position, acronym));
                scheduleApply();
                return;
            }
        }

        private void endColumnDrag()
        {
            EzModGroupColumn? column = draggingColumn;
            Vector2 position = lastDragPosition;
            draggingColumn = null;
            hideGhost();

            if (column == null || working == null)
                return;

            int from = groupColumns.IndexOf(column);

            if (from < 0)
                return;

            bool vertical = LayoutMode == EzModSelectLayoutMode.Vertical;
            int to = EzModLayoutGeometry.GroupInsertionIndex(groupColumns.Where(other => other != column)
                                                                         .Select(other => vertical ? other.ScreenSpaceDrawQuad.Centre.Y : other.ScreenSpaceDrawQuad.Centre.X), vertical ? position.Y : position.X);

            EzModLayoutResolver.MoveGroup(working, from, to);
            scheduleApply();
        }

        private void deleteGroup(EzModGroupColumn column)
        {
            if (working == null)
                return;

            EzModLayoutResolver.RemoveGroup(working, column.GroupId);
            scheduleApply();
        }

        private void renameGroup(EzModGroupColumn column, string name)
        {
            if (working == null)
                return;

            EzModLayoutResolver.RenameGroup(working, column.GroupId, name);
            working.EnsureMutableLists();

            for (int i = 0; i < working.Groups.Count; i++)
            {
                if (working.Groups[i].Id != column.GroupId)
                    continue;

                column.SetGroupName(working.Groups[i].Name);
                return;
            }
        }

        private void showGhost(string text, Vector2 screenPosition)
        {
            lastDragPosition = screenPosition;
            dragGhost.Text.Text = text;
            dragGhost.Position = toContentSpace(screenPosition);
            dragGhost.Alpha = 1;
        }

        private Vector2 toContentSpace(Vector2 screenPosition) =>
            MainAreaContent.ToLocalSpace(screenPosition) - MainAreaContent.ChildOffset;

        private void hideGhost()
        {
            dragGhost.Alpha = 0;
            insertionMarker.Alpha = 0;
        }

        private void updateInsertionMarker()
        {
            insertionMarker.Alpha = 0;

            foreach (var column in groupColumns)
            {
                if (!column.ScreenSpaceDrawQuad.Contains(lastDragPosition))
                    continue;

                bool vertical = LayoutMode == EzModSelectLayoutMode.Vertical;
                var position = column.ToScreenSpace(Vector2.Zero);

                if (draggingAcronym != null)
                {
                    var marker = column.InsertionMarker(lastDragPosition, draggingAcronym);
                    insertionMarker.Position = toContentSpace(marker.Position);
                    insertionMarker.Size = marker.Size;
                }
                else
                {
                    if (vertical && lastDragPosition.Y > column.ScreenSpaceDrawQuad.Centre.Y)
                        position = column.ToScreenSpace(new Vector2(0, column.DrawHeight));
                    else if (!vertical && lastDragPosition.X > column.ScreenSpaceDrawQuad.Centre.X)
                        position = column.ToScreenSpace(new Vector2(column.DrawWidth, 0));

                    insertionMarker.Position = toContentSpace(position);
                    insertionMarker.Size = vertical ? new Vector2(column.DrawWidth, 3) : new Vector2(3, column.DrawHeight);
                }
                insertionMarker.Alpha = 0.8f;
                break;
            }
        }

        private partial class DragGhost : Container
        {
            public OsuSpriteText Text { get; }

            public override bool HandlePositionalInput => false;

            public DragGhost()
            {
                Size = new Vector2(96, 32);
                Origin = Anchor.Centre;
                Alpha = 0;
                Depth = float.MinValue;
                Masking = true;
                CornerRadius = 8;
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                    },
                    Text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.Black,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    },
                };
            }
        }
    }
}
