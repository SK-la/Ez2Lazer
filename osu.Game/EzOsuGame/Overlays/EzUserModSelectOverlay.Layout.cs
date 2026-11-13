// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        private OsuGameBase game = null!;
        private EzModHiddenPool pool = null!;
        private ShearedButton editButton = null!;
        private ShearedButton newGroupButton = null!;
        private DragGhost dragGhost = null!;

        private EzRulesetModLayout? working;
        private string? loadedRuleset;
        private string? draggingAcronym;
        private EzModGroupColumn? draggingColumn;
        private Vector2 lastDragPosition;
        private bool applyScheduled;

        [BackgroundDependencyLoader]
        private void loadLayout(EzConfigStore modLayoutStore, OsuGameBase osuGame)
        {
            configStore = modLayoutStore;
            game = osuGame;
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

        protected override void AssignModsToColumns()
        {
            string? ruleset = currentRuleset();

            if (editing.Value && loadedRuleset != null && loadedRuleset != ruleset)
                saveWorking();

            var entries = new List<EzModLayoutMod>();
            var states = collectStates(entries);
            bool sameRuleset = loadedRuleset != null && loadedRuleset == ruleset;
            EzRulesetModLayout? source = sameRuleset ? working : ruleset == null ? null : configStore.GetModLayout(ruleset);

            working = EzModLayoutResolver.Resolve(source, entries);
            loadedRuleset = ruleset;
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
            if (editing.Value)
                finishEditing();

            base.Hide();
        }

        protected override void LoadComplete()
        {
            pool = new EzModHiddenPool
            {
                DragStarted = beginModDrag,
                Dragged = moveDrag,
                DragEnded = endModDrag,
            };
            MainAreaContent.Add(pool);

            dragGhost = new DragGhost();
            MainAreaContent.Add(dragGhost);

            editButton = new ShearedButton
            {
                Text = EzModLayoutStrings.EDIT,
                Width = 110,
                Height = 42,
                TextSize = 16,
                Position = new Vector2(312, 0),
                Action = toggleEditing,
            };
            newGroupButton = new ShearedButton
            {
                Text = EzModLayoutStrings.NEW_GROUP,
                Width = 140,
                Height = 42,
                TextSize = 16,
                Position = new Vector2(430, 0),
                Alpha = 0,
                Action = addGroup,
            };
            AddAboveColumns(editButton);
            AddAboveColumns(newGroupButton);

            base.LoadComplete();

            editing.BindValueChanged(change =>
            {
                editButton.Text = change.NewValue ? EzModLayoutStrings.DONE : EzModLayoutStrings.EDIT;
                newGroupButton.Alpha = change.NewValue ? 1 : 0;
                SetColumnFlowBottomPadding(change.NewValue ? pool_height + 8 : column_padding_rest);

                if (change.NewValue)
                {
                    pool.MoveToY(16);
                    pool.FadeIn(180);
                    pool.MoveToY(0, 220, Easing.OutQuint);
                }
                else
                {
                    pool.MoveToY(16, 160, Easing.InQuint).FadeOut(140);
                }
            }, true);

            SearchTextBox.Current.BindValueChanged(term => pool.SearchTerm = term.NewValue, true);
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

            EzModLayoutResolver.AddGroup(working, EzModLayoutStrings.NEW_GROUP.ToString());
            scheduleApply();
        }

        private void saveWorking()
        {
            if (loadedRuleset == null || working == null)
                return;

            configStore.SaveModLayout(loadedRuleset, working);
        }

        private string? currentRuleset()
        {
            var ruleset = game.Ruleset.Value;

            if (ruleset == null || string.IsNullOrEmpty(ruleset.ShortName))
                return null;

            return ruleset.ShortName;
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

            hiddenStates.Clear();

            foreach (string acronym in working.Hidden)
            {
                if (states.TryGetValue(acronym, out var state))
                    hiddenStates.Add(state);
            }

            groupColumns.Clear();
            var next = new List<ColumnDimContainer>();
            int hotkeyIndex = 0;

            foreach (var group in working.Groups)
            {
                var mods = new List<ModState>(group.Acronyms.Count);

                foreach (string acronym in group.Acronyms)
                {
                    if (states.TryGetValue(acronym, out var state))
                        mods.Add(state);
                }

                Key[]? keys = hotkeyIndex < hotkey_rows.Length ? hotkey_rows[hotkeyIndex] : null;
                hotkeyIndex++;

                var column = new EzModGroupColumn(group, editing, keys)
                {
                    Margin = new MarginPadding { Right = 10 },
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
                next.Add(new ColumnDimContainer(column));
            }

            ReplaceModColumns(next);
            pool.SetMods(hiddenStates);
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

            int to = 0;

            foreach (var other in groupColumns)
            {
                if (other == column)
                    continue;

                if (position.X < other.ScreenSpaceDrawQuad.Centre.X)
                    break;

                to++;
            }

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

        private void hideGhost() => dragGhost.Alpha = 0;

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
