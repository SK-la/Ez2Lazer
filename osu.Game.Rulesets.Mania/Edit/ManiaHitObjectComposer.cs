// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mania.Edit.Extension;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit
{
    [Cached]
    public partial class ManiaHitObjectComposer : ScrollingHitObjectComposer<ManiaHitObject, ManiaAction>
    {
        public override Bindable<TernaryState>? SelectionNewComboState => null;

        private DrawableManiaEditorRuleset drawableRuleset = null!;

        [Resolved]
        private EditorScreenWithTimeline? screenWithTimeline { get; set; }

        [Resolved(CanBeNull = true)]
        private EzEditorColumnEditState? columnEditState { get; set; }

        [Resolved(CanBeNull = true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        private ExtensionColumnSetStateContributor? extensionStateContributor;

        public ManiaHitObjectComposer(Ruleset ruleset)
            : base(ruleset)
        {
        }

        private Bindable<bool> limitPlacementToCurrentTime = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            limitPlacementToCurrentTime = config.GetBindable<bool>(OsuSetting.EditorLimitedDistanceSnap);
        }

        public override SnapResult FindSnappedPositionAndTime(Vector2 screenSpacePosition)
        {
            if (limitPlacementToCurrentTime.Value
                && BlueprintContainer.CurrentHitObjectPlacement?.PlacementActive == PlacementBlueprint.PlacementState.Waiting)
            {
                if (PlayfieldAtScreenSpacePosition(screenSpacePosition) is ScrollingPlayfield playfield)
                {
                    double time = BeatSnapProvider.SnapTime(EditorClock.CurrentTime);
                    return base.FindSnappedPositionAndTime(playfield.ScreenSpacePositionAtTime(time));
                }
            }

            return base.FindSnappedPositionAndTime(screenSpacePosition);
        }

        public new ManiaPlayfield Playfield => drawableRuleset.Playfield;

        public IScrollingInfo ScrollingInfo => drawableRuleset.ScrollingInfo;

        protected override Playfield PlayfieldAtScreenSpacePosition(Vector2 screenSpacePosition) =>
            Playfield.GetColumnByPosition(screenSpacePosition);

        protected override DrawableRuleset<ManiaHitObject> CreateDrawableRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods) =>
            drawableRuleset = new DrawableManiaEditorRuleset(ruleset, beatmap, mods);

        protected override ComposeBlueprintContainer CreateBlueprintContainer()
            => new ManiaBlueprintContainer(this);

        protected override BeatSnapGrid CreateBeatSnapGrid() => beatSnapGrid = new ManiaBeatSnapGrid();

        private ManiaBeatSnapGrid? beatSnapGrid;

        protected override Drawable CreatePlayfieldContentRoot(Drawable defaultContent)
            => PlayfieldContent = new ManiaEditorPlayfieldContent(defaultContent, Playfield, ScrollingInfo, TOOLBOX_EXPANDED_SIZE_RIGHT - TOOLBOX_CONTRACTED_SIZE_RIGHT,
                // Placement follows the active composition tool, like the playfield: note -> taps, hold -> holds.
                () => BlueprintContainer.CurrentTool switch
                {
                    NoteCompositionTool => ExtensionPlacementMode.Tap,
                    HoldNoteCompositionTool => ExtensionPlacementMode.Hold,
                    _ => ExtensionPlacementMode.None,
                },
                // Column insert/remove is its own mode, owned by the editor's switch rather than by the current tool.
                () => columnEditState?.ColumnEditMode.Value == true);

        /// <summary>
        /// The playfield content root, which also hosts the extension columns.
        /// </summary>
        internal ManiaEditorPlayfieldContent? PlayfieldContent { get; private set; }

        /// <summary>
        /// Hands the current selection over to the extension columns, when a completed drag ended on them.
        /// </summary>
        /// <returns>Whether the objects were moved out of the playfield.</returns>
        /// <remarks>
        /// The removal joins the ongoing drag transaction, so editor undo brings the notes back to the playfield and takes
        /// the extension notes out again, because the dock's state is part of the same snapshot.
        /// </remarks>
        internal bool TryDropSelectionIntoExtensionColumns(Vector2 screenSpacePosition)
        {
            if (PlayfieldContent == null || !PlayfieldContent.ContainsExtensionColumns(screenSpacePosition))
                return false;

            var dropped = EditorBeatmap.SelectedHitObjects.OfType<ManiaHitObject>().ToArray();

            if (dropped.Length == 0 || !PlayfieldContent.DropIntoExtensionColumns(dropped, screenSpacePosition))
                return false;

            foreach (var hitObject in dropped)
                EditorBeatmap.Remove(hitObject);

            return true;
        }

        protected override IReadOnlyList<CompositionTool<ManiaAction>> CompositionTools => new CompositionTool<ManiaAction>[]
        {
            new NoteCompositionTool(),
            new HoldNoteCompositionTool()
        };

        public override string ConvertSelectionToString()
            => string.Join(',', EditorBeatmap.SelectedHitObjects.Cast<ManiaHitObject>().OrderBy(h => h.StartTime)
                                             .Select(h => FormattableString.Invariant($"{Math.Round(h.StartTime)}|{h.Column}")));

        // 123|0,456|1,789|2 ...
        private static readonly Regex selection_regex = new Regex(@"^\d+\|\d+(,\d+\|\d+)*$", RegexOptions.Compiled);

        public override void SelectFromTimestamp(double timestamp, string objectDescription)
        {
            if (!selection_regex.IsMatch(objectDescription))
                return;

            List<ManiaHitObject> remainingHitObjects = EditorBeatmap.HitObjects.Cast<ManiaHitObject>().Where(h => h.StartTime >= timestamp).ToList();
            string[] objectDescriptions = objectDescription.Split(',');

            for (int i = 0; i < objectDescriptions.Length; i++)
            {
                string[] split = objectDescriptions[i].Split('|');
                if (split.Length != 2)
                    continue;

                if (!int.TryParse(split[0], out int time) || !int.TryParse(split[1], out int column))
                    continue;

                ManiaHitObject? current = remainingHitObjects.FirstOrDefault(h => Precision.AlmostEquals(h.StartTime, time, 0.5) && h.Column == column);

                if (current == null)
                    continue;

                EditorBeatmap.SelectedHitObjects.Add(current);

                if (i < objectDescriptions.Length - 1)
                    remainingHitObjects = remainingHitObjects.Where(h => h != current && h.StartTime >= current.StartTime).ToList();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (screenWithTimeline?.TimelineArea.Timeline != null)
                drawableRuleset.TimelineTimeRange = EditorClock.TrackLength / screenWithTimeline.TimelineArea.Timeline.CurrentZoom.Value / 2;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // The dock draws its own beat lines (it cannot share the playfield's, which lives in a scaled space), so it
            // only needs to be told which range owns them.
            if (PlayfieldContent != null)
                PlayfieldContent.ExtensionSelectionTimeRange = extensionSelectionTimeRange();

            // Leaving the selection tool drops the dock's selection, matching the playfield's own selection behaviour.
            if (PlayfieldContent != null && BlueprintContainer.CurrentTool is not SelectTool)
                PlayfieldContent.ExtensionOverlay.ClearSelection();
        }

        /// <summary>
        /// The time range the dock's own beat lines should highlight, or <c>null</c> to leave them to the cursor.
        /// </summary>
        private (double start, double end)? extensionSelectionTimeRange()
        {
            var notes = PlayfieldContent?.ExtensionOverlay.SelectedNotes;

            if (notes is not { Count: > 0 })
                return null;

            double start = double.MaxValue;
            double end = double.MinValue;

            foreach (var note in notes)
            {
                start = Math.Min(start, note.Time);
                end = Math.Max(end, note.Time + note.Duration);
            }

            return (start, end);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Selection belongs to one region at a time. The columns report when they take it; the playfield is
            // observed through its own selection list, which also covers selections made without a click on it.
            if (PlayfieldContent != null)
                PlayfieldContent.ExtensionOverlay.SelectionChanged += onExtensionSelectionChanged;

            EditorBeatmap.SelectedHitObjects.CollectionChanged += onPlayfieldSelectionChanged;

            // The columns live outside the encoded beatmap, so they contribute their own block to the shared snapshots
            // and every undo step restores both regions together.
            if (changeHandler is EditorChangeHandler handler && PlayfieldContent != null)
            {
                extensionStateContributor = new ExtensionColumnSetStateContributor(PlayfieldContent.ColumnSet);
                handler.AddContributor(extensionStateContributor);
            }

            // Only a composer with extension columns shows the editor's column-edit switch.
            columnEditState?.Available.Value = true;
        }

        private void onExtensionSelectionChanged()
        {
            if (PlayfieldContent?.ExtensionOverlay.HasSelection != true)
                return;

            SetSelectTool();
            EditorBeatmap.SelectedHitObjects.Clear();
        }

        private void onPlayfieldSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (EditorBeatmap.SelectedHitObjects.Count > 0)
                PlayfieldContent?.ExtensionOverlay.ClearSelection();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (extensionStateContributor != null && changeHandler is EditorChangeHandler handler)
                handler.RemoveContributor(extensionStateContributor);

            if (PlayfieldContent.IsNotNull())
                PlayfieldContent.ExtensionOverlay.SelectionChanged -= onExtensionSelectionChanged;

            if (EditorBeatmap.IsNotNull())
                EditorBeatmap.SelectedHitObjects.CollectionChanged -= onPlayfieldSelectionChanged;

            columnEditState?.Available.Value = false;

            base.Dispose(isDisposing);
        }
    }
}
