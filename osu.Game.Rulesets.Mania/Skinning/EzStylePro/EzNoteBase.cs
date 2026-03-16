// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public abstract partial class EzNoteBase : CompositeDrawable
    {
        protected virtual bool UseColorization => false;
        protected virtual bool ShowSeparators => false;

        protected Container? LineContainer { get; private set; }
        protected Container? MainContainer { get; private set; }

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        protected StageDefinition StageDefinition { get; private set; } = null!;

        [Resolved]
        protected Ez2ConfigManager EzSkinConfig { get; private set; } = null!;

        [Resolved]
        protected EzLocalTextureFactory Factory { get; private set; } = null!;

        protected readonly IBindable<string> NoteSetBindable = new Bindable<string>();
        protected readonly IBindable<bool> EnabledColor = new Bindable<bool>();
        protected readonly IBindable<Vector2> NoteSize = new Bindable<Vector2>();

        protected int KeyMode;
        protected int ColumnIndex;

        private Colour4 lastNoteColor = Colour4.White;
        private bool lastNoteColorCached;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            KeyMode = StageDefinition.Columns;
            ColumnIndex = Column.Index;

            if (ShowSeparators) createSeparators();

            MainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            AddInternal(MainContainer); //允许多个子元素

            NoteSetBindable.BindTo(ezSkinInfo.NoteSetName);
            EnabledColor.BindTo(ezSkinInfo.ColorSettingsEnabled);
            NoteSize.BindTo(Column.EzNoteSizeBindable);
            NoteSize.BindValueChanged(_ => OnNoteSizeChanged());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // Use a per-column watcher to avoid creating an event handler delegate per-note.
            ColumnWatcher.GetOrCreate(Column).Add(this);

            Scheduler.AddOnce(OnNoteSetChanged);
        }

        private void createSeparators()
        {
            var noteSeparatorsL = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
            };

            var noteSeparatorsR = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
            };

            LineContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Stretch,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                // Alpha = ShowSeparators ? 1f : 0f,
                Children = [noteSeparatorsL, noteSeparatorsR]
            };

            AddInternal(LineContainer);
        }

        protected virtual void UpdateTexture() { }
        protected virtual void UpdateDrawable() { }

        protected virtual void UpdateColor()
        {
            // TODO： 命中HMT面条时，考虑是否跳过面头着色，只更新面身。
            // 或者单独做一个着色拓展设置，比如“仅着色面身”“着色面头和面身”“不着色”等等。
            // 或者，不着色时，使用新的开关拓展，在Middle中进一步单独管理着色
            if (MainContainer != null && UseColorization)
            {
                Colour4 targetColor = NoteColor;

                if (!lastNoteColorCached || lastNoteColor != targetColor)
                {
                    MainContainer.Colour = targetColor;
                    lastNoteColor = targetColor;
                    lastNoteColorCached = true;
                }
            }
        }

        private void OnNoteSetChanged()
        {
            if (string.IsNullOrEmpty(NoteSetBindable.Value))
                return;

            MainContainer?.Clear();
            // 纹理切换后会通过 EzLocalTextureFactory.scheduleTextureRefresh() 自动触发 OnNoteSizeChanged
            // 这里只需要更新纹理即可
            Scheduler.AddOnce(UpdateTexture);
            Scheduler.AddOnce(UpdateDrawable);
        }

        private void OnNoteSizeChanged()
        {
            Scheduler.AddOnce(UpdateDrawable);
            OnColourChanged();
        }

        private void OnColourChanged()
        {
            lastNoteColorCached = false;
            Scheduler.AddOnce(UpdateColor);
        }

        protected virtual Colour4 NoteColor
        {
            get
            {
                if (!EnabledColor.Value || !UseColorization)
                    return Colour4.White;

                return Column.EzNoteColourBindable?.Value ?? Colour4.White;
            }
        }

        protected virtual string ColorPrefix
        {
            get
            {
                if (EnabledColor.Value || !UseColorization) return "white";

                EzColumnType keyType = EzSkinConfig.GetColumnTypeFast(KeyMode, ColumnIndex);

                return keyType switch
                {
                    EzColumnType.A => "white",
                    EzColumnType.B => "blue",
                    EzColumnType.S => "green",
                    EzColumnType.E => "white",
                    EzColumnType.P => "green",
                    _ => "white"
                };
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                ColumnWatcher.Remove(Column, this);
            }

            base.Dispose(isDisposing);
        }

        // Forwarders used by ColumnWatcher when broadcasting per-column changes to instances.
        internal void ForwardOnNoteSetChanged() => OnNoteSetChanged();
        internal void ForwardOnNoteSizeChanged() => OnNoteSizeChanged();
        internal void ForwardOnColourChanged() => OnColourChanged();

        private class ColumnWatcher
        {
            private readonly Column column;
            private readonly List<WeakReference<EzNoteBase>> notes = new List<WeakReference<EzNoteBase>>();

            public ColumnWatcher(Column column)
            {
                this.column = column;
                column.NoteSetChanged += watcherNoteSetChanged;
                column.NoteColourChanged += watcherNoteColourChanged;
                column.NoteSizeChanged += watcherNoteSizeChanged;
            }

            public void Add(EzNoteBase note)
            {
                // store weak reference to avoid preventing note GC if removed elsewhere
                notes.Add(new WeakReference<EzNoteBase>(note));
            }

            public void Remove(EzNoteBase note)
            {
                notes.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, note));
            }

            private void watcherNoteSetChanged()
            {
                // iterate backwards and remove dead weak references in-place to avoid
                // allocating a large temporary array when the column has many notes.
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];

                    if (!wr.TryGetTarget(out var target))
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    // If the note is no longer part of a Playfield (e.g. returned to a pool), treat it as removed.
                    if (target.FindClosestParent<Playfield>() == null)
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    target.ForwardOnNoteSetChanged();
                }
            }

            private void watcherNoteColourChanged()
            {
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];

                    if (!wr.TryGetTarget(out var target))
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    if (target.FindClosestParent<Playfield>() == null)
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    target.ForwardOnColourChanged();
                }
            }

            private void watcherNoteSizeChanged()
            {
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];

                    if (!wr.TryGetTarget(out var target))
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    if (target.FindClosestParent<Playfield>() == null)
                    {
                        notes.RemoveAt(i);
                        continue;
                    }

                    target.ForwardOnNoteSizeChanged();
                }
            }

            public void Unsubscribe()
            {
                column.NoteSetChanged -= watcherNoteSetChanged;
                column.NoteColourChanged -= watcherNoteColourChanged;
                column.NoteSizeChanged -= watcherNoteSizeChanged;
            }

            public bool IsEmpty => notes.All(wr => !wr.TryGetTarget(out _));

            private static readonly Dictionary<WeakReference<Column>, ColumnWatcher> watchers = new Dictionary<WeakReference<Column>, ColumnWatcher>();
            private static readonly object watcher_lock = new object();

            public static ColumnWatcher GetOrCreate(Column c)
            {
                lock (watcher_lock)
                {
                    // Clean up dead references first
                    cleanupDeadReferences();

                    // Try to find existing watcher for this column
                    ColumnWatcher? w = null;

                    foreach (var kvp in watchers)
                    {
                        if (kvp.Key.TryGetTarget(out var target) && ReferenceEquals(target, c))
                        {
                            w = kvp.Value;
                            break;
                        }
                    }

                    if (w == null)
                    {
                        w = new ColumnWatcher(c);
                        var weakRef = new WeakReference<Column>(c);
                        watchers[weakRef] = w;
                    }

                    return w;
                }
            }

            public static void Remove(Column c, EzNoteBase note)
            {
                lock (watcher_lock)
                {
                    ColumnWatcher? w = null;
                    WeakReference<Column>? keyToRemove = null;

                    foreach (var kvp in watchers)
                    {
                        if (kvp.Key.TryGetTarget(out var target) && ReferenceEquals(target, c))
                        {
                            w = kvp.Value;
                            keyToRemove = kvp.Key;
                            break;
                        }
                    }

                    if (w == null)
                        return;

                    w.Remove(note);

                    if (w.IsEmpty)
                    {
                        w.Unsubscribe();
                        if (keyToRemove != null)
                            watchers.Remove(keyToRemove);
                    }
                }
            }

            private static void cleanupDeadReferences()
            {
                // Remove entries where the Column has been garbage collected
                var keysToRemove = watchers.Keys.Where(k => !k.TryGetTarget(out _)).ToList();

                foreach (var key in keysToRemove)
                {
                    watchers.Remove(key);
                }
            }
        }
    }
}
