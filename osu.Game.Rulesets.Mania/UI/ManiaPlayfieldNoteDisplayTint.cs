// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Which note parts receive display colour. Tail uses <see cref="COLOUR_HOLD_BODY"/>.
    /// </summary>
    public static class ManiaNoteDisplayColourParts
    {
        public const bool COLOUR_NOTE = true;
        public const bool COLOUR_HOLD_HEAD = true;
        public const bool COLOUR_HOLD_BODY = true;
        public const bool COLOUR_HIT_TARGET = true;
    }

    public enum ManiaNoteDisplayColourPart
    {
        None,
        Note,
        HoldHead,
        HoldBody,

        /// <summary>
        /// 命中靶。只有列色，没有时刻，因此不参与节拍线着色。
        /// </summary>
        HitTarget,
    }

    /// <summary>
    /// Playfield-scoped note display colour: one divisor-switch and palette subscription, shared snap caches for all columns.
    /// </summary>
    [Cached]
    public partial class ManiaPlayfieldNoteDisplayTint : Component
    {
        public event Action? Changed;

        /// <summary>
        /// Editor compose uses <see cref="Ez2Setting.EditorDivisorNoteColouring"/>; gameplay uses <see cref="Ez2Setting.GameplayDivisorNoteColouring"/>.
        /// </summary>
        public bool DivisorNoteColouringEnabled { get; private set; }

        public bool ColumnNoteColouringEnabled { get; private set; }

        /// <summary>
        /// <see cref="Ez2Setting.ManiaSkipEmptyEdgeColumns"/> as used by Ez2 built-in note colours. Layout still reads the setting itself.
        /// </summary>
        public bool BuiltInLayoutTemplateColourEnabled { get; private set; }

        /// <summary>
        /// Column colouring or divisor colouring is supplying an RGB tint. Default and Argon keep accent colour only while this is false.
        /// </summary>
        public bool DisplayTintActive => DivisorNoteColouringEnabled || ColumnNoteColouringEnabled;

        public bool IsEditorCompose => editorBeatmap != null;

        [Resolved]
        private EzConfigStore configStore { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private EditorBeatmap? editorBeatmap { get; set; }

        [Resolved(CanBeNull = true)]
        private BindableBeatDivisor? beatDivisor { get; set; }

        private ManiaEditorDivisorPaletteBinding paletteBinding = null!;
        private Action? onPaletteChanged;
        private Action<ValueChangedEvent<bool>>? onDivisorNoteColouringChanged;

        private Bindable<bool> editorDivisorNoteColouring = null!;
        private Bindable<bool> gameplayDivisorNoteColouring = null!;
        private Bindable<bool> colorSettingsEnabled = null!;
        private Bindable<bool> builtInColourTemplate = null!;
        private Bindable<EzColouringMode> colouringMode = null!;
        private Action<ValueChangedEvent<bool>>? onDisplaySettingsChanged;
        private Action<ValueChangedEvent<EzColouringMode>>? onColouringModeChanged;

        private readonly Dictionary<long, int> lineDivisorBySnapMs = new Dictionary<long, int>();
        private readonly Dictionary<int, Color4> tintByLineDivisor = new Dictionary<int, Color4>();

        private int cachedGridBeatDivisor = -1;
        private bool cachedFixedGrid;

        /// <summary>
        /// Display colour for <paramref name="part"/>. White when that part is disabled or <paramref name="column"/> is missing.
        /// </summary>
        public Color4 GetDisplayColour(ManiaNoteDisplayColourPart part, Column column, double startTime)
        {
            if (!AllowsDisplayColour(part))
                return Color4.White;

            // 命中靶没有命中时刻：只吃列色，节拍线着色对它是无定义的。
            if (part == ManiaNoteDisplayColourPart.HitTarget)
                return ColumnNoteColouringEnabled ? column.NoteDisplayColour : Color4.White;

            if (DivisorNoteColouringEnabled)
                return getNoteTint(startTime);

            if (ColumnNoteColouringEnabled)
                return column.NoteDisplayColour;

            return Color4.White;
        }

        public bool UseTintBaseTexture(ManiaNoteDisplayColourPart part, Column column)
            => AllowsDisplayColour(part) && DisplayTintActive;

        public string GetEzProTextureColourPrefix(ManiaNoteDisplayColourPart part, Column column)
        {
            if (UseTintBaseTexture(part, column))
                return EzColumnResourceLookup.TINT_BASE_TEXTURE_AFFIX;

            return EzColumnResourceLookup.EzProNoteTexturePrefix(column.EzNoteTypeBindable.Value);
        }

        public static bool AllowsDisplayColour(ManiaNoteDisplayColourPart part) => part switch
        {
            ManiaNoteDisplayColourPart.Note => ManiaNoteDisplayColourParts.COLOUR_NOTE,
            ManiaNoteDisplayColourPart.HoldHead => ManiaNoteDisplayColourParts.COLOUR_HOLD_HEAD,
            ManiaNoteDisplayColourPart.HoldBody => ManiaNoteDisplayColourParts.COLOUR_HOLD_BODY,
            ManiaNoteDisplayColourPart.HitTarget => ManiaNoteDisplayColourParts.COLOUR_HIT_TARGET,
            _ => false,
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            editorDivisorNoteColouring = ezConfig.GetBindable<bool>(Ez2Setting.EditorDivisorNoteColouring);
            gameplayDivisorNoteColouring = ezConfig.GetBindable<bool>(Ez2Setting.GameplayDivisorNoteColouring);
            applyDivisorNoteColouring();
            onDivisorNoteColouringChanged = syncDivisorNoteColouring;
            editorDivisorNoteColouring.BindValueChanged(onDivisorNoteColouringChanged);
            gameplayDivisorNoteColouring.BindValueChanged(onDivisorNoteColouringChanged);

            colorSettingsEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            builtInColourTemplate = ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);
            colouringMode = ezConfig.GetBindable<EzColouringMode>(Ez2Setting.ColouringMode);
            applyColumnNoteColouring();
            applyBuiltInLayoutTemplate();

            onDisplaySettingsChanged = syncDisplaySettings;
            colorSettingsEnabled.BindValueChanged(onDisplaySettingsChanged);
            builtInColourTemplate.BindValueChanged(onDisplaySettingsChanged);

            onColouringModeChanged = syncColouringMode;
            colouringMode.BindValueChanged(onColouringModeChanged);

            paletteBinding = new ManiaEditorDivisorPaletteBinding(configStore);
            onPaletteChanged = invalidateTintColours;
            paletteBinding.Changed += onPaletteChanged;

            beatDivisor?.BindValueChanged(_ => invalidateLineDivisors(), true);

            editorBeatmap?.FixedGridEnabled.BindValueChanged(_ => invalidateLineDivisors(), true);

            invalidateLineDivisors();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (onPaletteChanged != null)
                    paletteBinding.Changed -= onPaletteChanged;

                if (onDivisorNoteColouringChanged != null)
                {
                    editorDivisorNoteColouring.ValueChanged -= onDivisorNoteColouringChanged;
                    gameplayDivisorNoteColouring.ValueChanged -= onDivisorNoteColouringChanged;
                }

                if (onDisplaySettingsChanged != null)
                {
                    colorSettingsEnabled.ValueChanged -= onDisplaySettingsChanged;
                    builtInColourTemplate.ValueChanged -= onDisplaySettingsChanged;
                }

                if (onColouringModeChanged != null)
                    colouringMode.ValueChanged -= onColouringModeChanged;
            }

            base.Dispose(isDisposing);
        }

        private void syncDivisorNoteColouring(ValueChangedEvent<bool> _)
        {
            applyDivisorNoteColouring();
            Changed?.Invoke();
        }

        private void syncDisplaySettings(ValueChangedEvent<bool> _)
        {
            applyColumnNoteColouring();
            applyBuiltInLayoutTemplate();
            Changed?.Invoke();
        }

        private void applyColumnNoteColouring() => ColumnNoteColouringEnabled = colorSettingsEnabled.Value;

        private void applyBuiltInLayoutTemplate() => BuiltInLayoutTemplateColourEnabled = builtInColourTemplate.Value;

        private void syncColouringMode(ValueChangedEvent<EzColouringMode> _)
        {
            // 缓存里存的是按旧算法算出的节拍线色，换算法必须整批丢弃。
            invalidateTintColours();
        }

        private void applyDivisorNoteColouring()
        {
            DivisorNoteColouringEnabled = ManiaDivisorNoteColouring.IsEnabled(
                IsEditorCompose,
                gameplayDivisorNoteColouring.Value,
                editorDivisorNoteColouring.Value);
        }

        private Color4 getNoteTint(double startTime)
        {
            refreshGridStateIfNeeded();

            ManiaEditorBeatLinePalette palette = configStore.BeatmapEditor.Colouring.BeatLines;
            double snappedTime = beatmap.ControlPointInfo.GetClosestSnappedTime(startTime, cachedGridBeatDivisor);
            long snapKey = (long)Math.Round(snappedTime);

            if (!lineDivisorBySnapMs.TryGetValue(snapKey, out int lineDivisor))
            {
                lineDivisor = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(
                    beatmap.ControlPointInfo,
                    startTime,
                    cachedGridBeatDivisor,
                    cachedFixedGrid);

                lineDivisorBySnapMs[snapKey] = lineDivisor;
            }

            if (!tintByLineDivisor.TryGetValue(lineDivisor, out Color4 tint))
            {
                tint = ManiaNoteDisplayColour.ForBeatLineDisplayColour(lineDivisor, colours, palette, colouringMode.Value);
                tintByLineDivisor[lineDivisor] = tint;
            }

            return tint;
        }

        private void refreshGridStateIfNeeded()
        {
            int gridBeatDivisor = resolveGridBeatDivisor();
            bool fixedGrid = IsEditorCompose && editorBeatmap != null && editorBeatmap.FixedGridEnabled.Value;

            if (gridBeatDivisor == cachedGridBeatDivisor && fixedGrid == cachedFixedGrid)
                return;

            cachedGridBeatDivisor = gridBeatDivisor;
            cachedFixedGrid = fixedGrid;
            lineDivisorBySnapMs.Clear();
            tintByLineDivisor.Clear();
        }

        private int resolveGridBeatDivisor()
        {
            if (IsEditorCompose)
                return beatDivisor?.Value ?? 4;

            return beatmap.BeatmapInfo.BeatDivisor;
        }

        private void invalidateLineDivisors()
        {
            lineDivisorBySnapMs.Clear();
            invalidateTintColours();
        }

        private void invalidateTintColours()
        {
            tintByLineDivisor.Clear();
            Changed?.Invoke();
        }
    }
}
