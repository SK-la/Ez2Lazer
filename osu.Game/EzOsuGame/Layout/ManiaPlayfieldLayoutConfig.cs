// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Runtime mania playfield layout, persisted as <see cref="ManiaPlayfieldLayout"/> through <see cref="EzLayoutStore"/>.
    /// Shared by the playfield and by HUD elements that match the judgement line layout.
    /// </summary>
    public class ManiaPlayfieldLayoutConfig
    {
        public const float MAX_OFFSET = 1f;

        private const int save_debounce_ms = 200;

        /// <summary>
        /// Stage horizontal offset as a percentage. ±100 puts the stage flush against the corresponding screen edge.
        /// </summary>
        public BindableFloat StageX { get; } = new BindableFloat
        {
            MinValue = -100,
            MaxValue = 100,
            Precision = 0.5f,
        };

        /// <summary>
        /// Stage vertical offset as a percentage of half the screen height. Positive is up.
        /// </summary>
        public BindableFloat StageY { get; } = new BindableFloat
        {
            MinValue = -100,
            MaxValue = 100,
            Precision = 0.5f,
        };

        /// <summary>
        /// <see cref="StageX"/> as a fraction of the free space beside the stage.
        /// </summary>
        public float StageXFraction => StageX.Value / PERCENT_SCALE;

        /// <summary>
        /// <see cref="StageY"/> as a fraction of half the container height.
        /// </summary>
        public float StageYFraction => StageY.Value / PERCENT_SCALE;

        /// <summary>
        /// Stage centre offset from the screen centre as a fraction of the screen width, written by the playfield each frame.
        /// Lets HUD elements follow the stage without knowing the playfield container size.
        /// </summary>
        public float StageOffsetFractionX { get; set; }

        private const float PERCENT_SCALE = 100;

        private readonly EzLayoutStore store;
        private readonly Lock saveLock = new Lock();
        private int lastSave;

        public ManiaPlayfieldLayoutConfig(Storage storage)
        {
            store = new EzLayoutStore(storage);

            var saved = store.LoadManiaPlayfieldLayout();
            StageX.Value = Math.Clamp(saved.Stage.X * PERCENT_SCALE, -PERCENT_SCALE * MAX_OFFSET, PERCENT_SCALE * MAX_OFFSET);
            StageY.Value = Math.Clamp(saved.Stage.Y * PERCENT_SCALE, -PERCENT_SCALE * MAX_OFFSET, PERCENT_SCALE * MAX_OFFSET);

            StageX.BindValueChanged(_ => queueSave());
            StageY.BindValueChanged(_ => queueSave());
        }

        public void Save()
        {
            lock (saveLock)
            {
                // Read back first so sections written by other components survive this save.
                var layout = store.LoadManiaPlayfieldLayout();
                layout.Stage.X = StageXFraction;
                layout.Stage.Y = StageYFraction;
                store.SaveManiaPlayfieldLayout(layout);
            }
        }

        private void queueSave()
        {
            int current = Interlocked.Increment(ref lastSave);

            Task.Delay(save_debounce_ms).ContinueWith(_ =>
            {
                if (current == lastSave)
                    Save();
            });
        }
    }
}
