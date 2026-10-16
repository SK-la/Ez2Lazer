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
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
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
            => playfieldContent = new ManiaEditorPlayfieldContent(defaultContent, Playfield, TOOLBOX_EXPANDED_SIZE_RIGHT - TOOLBOX_CONTRACTED_SIZE_RIGHT,
                // The column controls and the playfield's selection box are both selection-mode concerns; the columns
                // handle placement and their own notes themselves.
                () => BlueprintContainer.CurrentTool is SelectTool);

        private ManiaEditorPlayfieldContent? playfieldContent;

        /// <summary>
        /// The playfield content root, which also hosts the extension columns.
        /// </summary>
        internal ManiaEditorPlayfieldContent? PlayfieldContent => playfieldContent;

        /// <summary>
        /// Hands the current selection over to the extension columns, when a completed drag ended on them.
        /// </summary>
        /// <returns>Whether the objects were moved out of the playfield.</returns>
        /// <remarks>
        /// The removal joins the ongoing drag transaction, so editor undo brings the notes back to the playfield.
        /// The extension notes created here are session state without history yet, so undo does not discard them again.
        /// </remarks>
        internal bool TryDropSelectionIntoExtensionColumns(Vector2 screenSpacePosition)
        {
            if (playfieldContent == null || !playfieldContent.ContainsExtensionColumns(screenSpacePosition))
                return false;

            var dropped = EditorBeatmap.SelectedHitObjects.OfType<ManiaHitObject>().ToArray();

            if (dropped.Length == 0 || !playfieldContent.DropIntoExtensionColumns(dropped, screenSpacePosition))
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

            // The columns' beat lines mirror the playfield's, so a selection on either side lights up both.
            var range = activeSelectionTimeRange();

            if (playfieldContent != null)
                playfieldContent.SelectionTimeRange = range;

            if (range is not null && beatSnapGrid != null)
                beatSnapGrid.SelectionTimeRange = range;
        }

        /// <summary>
        /// The time range currently selected, across the playfield and the extension columns.
        /// </summary>
        /// <remarks>
        /// The two regions never hold a selection at the same time, so the playfield's wins whenever it is non-empty.
        /// </remarks>
        private (double start, double end)? activeSelectionTimeRange()
        {
            if (EditorBeatmap.SelectedHitObjects.Count > 0)
                return (EditorBeatmap.SelectedHitObjects.Min(h => h.StartTime), EditorBeatmap.SelectedHitObjects.Max(h => h.GetEndTime()));

            var notes = playfieldContent?.SelectedExtensionNotes;

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
            if (playfieldContent != null)
                playfieldContent.ExtensionSelectionTaken += onExtensionSelectionTaken;

            EditorBeatmap.SelectedHitObjects.CollectionChanged += onPlayfieldSelectionChanged;
        }

        private void onExtensionSelectionTaken()
        {
            SetSelectTool();
            EditorBeatmap.SelectedHitObjects.Clear();
        }

        private void onPlayfieldSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (EditorBeatmap.SelectedHitObjects.Count > 0)
                playfieldContent?.ClearExtensionSelection();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (playfieldContent.IsNotNull())
                playfieldContent.ExtensionSelectionTaken -= onExtensionSelectionTaken;

            if (EditorBeatmap.IsNotNull())
                EditorBeatmap.SelectedHitObjects.CollectionChanged -= onPlayfieldSelectionChanged;

            base.Dispose(isDisposing);
        }
    }
}
