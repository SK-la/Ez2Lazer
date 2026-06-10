// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Temporarily applies a configuration snapshot while building preview drawables, then restores live values on dispose.
    /// </summary>
    public partial class EzSkinEditorConfigSnapshotScope : Container
    {
        private readonly EzSkinJsonDocument snapshot;
        private readonly Func<Drawable> createChild;

        private EzSkinJsonDocument? savedDocument;

        [Resolved]
        private Ez2ConfigManager config { get; set; } = null!;

        public EzSkinEditorConfigSnapshotScope(EzSkinJsonDocument snapshot, Func<Drawable> createChild)
        {
            this.snapshot = snapshot;
            this.createChild = createChild;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            savedDocument = EzSkinJsonBridge.Capture(config);
            EzSkinJsonBridge.Apply(snapshot, config);
            Child = createChild();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && savedDocument != null)
                EzSkinJsonBridge.Apply(savedDocument, config);

            base.Dispose(isDisposing);
        }
    }
}
