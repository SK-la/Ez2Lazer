// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// A column listing the subsection heads of the current section, used to jump to a subsection
    /// while leaving the expanded content in place to the right.
    /// </summary>
    public partial class SettingsSubsectionNavColumn : Container
    {
        /// <summary>
        /// Invoked with the target subsection when an entry is selected.
        /// </summary>
        public Action<SettingsSubsection> EntryClicked { get; init; }

        private FillFlowContainer flow;

        private readonly List<SubsectionNavButton> entries = new List<SubsectionNavButton>();
        private readonly Dictionary<SubsectionNavButton, SettingsSubsection> entryTargets = new Dictionary<SubsectionNavButton, SettingsSubsection>();

        public SettingsSubsectionNavColumn()
        {
            RelativeSizeAxes = Axes.Y;

            // collapsed until the panel decides the column is relevant, so it never reserves space by default.
            Width = 0;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Vertical = 6 },
                },
            });
        }

        /// <summary>
        /// Rebuilds the column for the provided subsections.
        /// </summary>
        public void SetSubsections(IEnumerable<SettingsSubsection> subsections)
        {
            foreach (var entry in entries)
                flow.Remove(entry, false);

            entries.Clear();
            entryTargets.Clear();

            foreach (var subsection in subsections)
            {
                var target = subsection;

                var entry = new SubsectionNavButton
                {
                    Header = target.Title,
                    Action = () =>
                    {
                        SetSelected(target);
                        EntryClicked?.Invoke(target);
                    },
                };

                entries.Add(entry);
                entryTargets[entry] = target;
                flow.Add(entry);
            }
        }

        /// <summary>
        /// Expands or collapses the column. The panel reserves <see cref="Drawable.DrawWidth"/> of space,
        /// so animating the width here also animates the content offset in lockstep.
        /// </summary>
        public void SetExpanded(bool expanded, double duration)
        {
            this.ResizeWidthTo(expanded ? SettingsPanel.SUBSECTION_NAV_WIDTH : 0, duration, Easing.OutQuint);
            this.FadeTo(expanded ? 1 : 0, duration, Easing.OutQuint);
        }

        /// <summary>
        /// Highlights the entry corresponding to the provided subsection.
        /// </summary>
        public void SetSelected(SettingsSubsection subsection)
        {
            foreach (var entry in entries)
                entry.Selected = ReferenceEquals(entryTargets[entry], subsection);
        }
    }
}
