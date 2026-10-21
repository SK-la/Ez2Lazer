// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// Draws the replay input notes of a single column.
    /// </summary>
    /// <remarks>
    /// This is a nested <see cref="ScrollingPlayfield"/>, not a hand-drawn overlay: the objects go through the same
    /// <see cref="ScrollingHitObjectContainer"/> as the beatmap's notes, so position, hold length and lifetime are produced
    /// by the framework and cannot drift away from the playfield. It takes no input and raises no results.
    /// </remarks>
    public partial class EzReplayInputNotePlayfield : ScrollingPlayfield
    {
        private readonly EzReplayInputNoteModel model;
        private readonly int column;
        private readonly List<HitObject> added = new List<HitObject>();

        public EzReplayInputNotePlayfield(EzReplayInputNoteModel model, int column)
        {
            this.model = model;
            this.column = column;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0.75f;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(model);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Unbounded: the pool is only asked for notes that are currently alive, which the lifetime window bounds.
            // A hard cap would throw once a dense section or a long hold pushes past it.
            RegisterPool<EzReplayInputTapNote, EzReplayInputTapNoteDrawable>(8);
            RegisterPool<EzReplayInputHoldNote, EzReplayInputHoldNoteDrawable>(8);
        }

        protected override void OnHitObjectAdded(HitObject hitObject)
        {
            // These objects carry no samples; base would pre-build a sample pool per audio name for nothing.
        }

        /// <summary>
        /// Replaces the drawn notes with <paramref name="notes"/>. Cheap to call again on a mode switch: every object is
        /// pooled and the container rebuilds lifetimes itself.
        /// </summary>
        public void SetNotes(IReadOnlyList<EzReplayInputNote> notes)
        {
            foreach (var hitObject in added)
                Remove(hitObject);

            added.Clear();

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                HitObject hitObject;

                if (note.IsTapStyle)
                {
                    hitObject = new EzReplayInputTapNote
                    {
                        StartTime = note.StartTime,
                        Kind = note.Kind,
                        HeadResult = note.HeadResult,
                        NonHeadResult = note.NonHeadResult,
                        NonHeadRevealTime = note.NonHeadRevealTime,
                        BodyBroken = note.BodyBroken,
                    };
                }
                else
                {
                    hitObject = new EzReplayInputHoldNote
                    {
                        StartTime = note.StartTime,
                        EndTime = note.EndTime,
                        Kind = note.Kind,
                        HeadResult = note.HeadResult,
                        NonHeadResult = note.NonHeadResult,
                        NonHeadRevealTime = note.NonHeadRevealTime,
                        BodyBroken = note.BodyBroken,
                    };
                }

                added.Add(hitObject);
                Add(hitObject);
            }
        }

        public int Column => column;
    }
}
