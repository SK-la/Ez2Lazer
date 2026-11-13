// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Overlays.Mods;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModLayoutPanel : IncompatibilityDisplayingModPanel
    {
        public readonly Bindable<bool> Editing = new BindableBool();

        public Action<Vector2>? DragStarted { get; set; }

        public Action<Vector2>? Dragged { get; set; }

        public Action? DragEnded { get; set; }

        public EzModLayoutPanel(ModState modState)
            : base(modState)
        {
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Editing.Value)
                return true;

            return base.OnClick(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!Editing.Value)
                return false;

            DragStarted?.Invoke(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            Dragged?.Invoke(e.ScreenSpaceMousePosition);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            base.OnDragEnd(e);
            DragEnded?.Invoke();
        }

        protected override void Dispose(bool isDisposing)
        {
            Editing.UnbindBindings();
            base.Dispose(isDisposing);
        }
    }
}
