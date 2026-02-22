// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osuTK;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public abstract partial class EzNoteBase : CompositeDrawable
    {
        protected virtual bool BoolUpdateColor => true;
        protected virtual bool UseColorization => true;
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

        protected readonly IBindable<Colour4> ColumnColorBindable = new Bindable<Colour4>();
        protected readonly IBindable<string> NoteSetName = new Bindable<string>();
        protected readonly IBindable<bool> EnabledColor = new Bindable<bool>();
        protected readonly IBindable<Vector2> NoteSize = new Bindable<Vector2>();
        protected int KeyMode;
        protected int ColumnIndex;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            KeyMode = StageDefinition.Columns;
            ColumnIndex = Column.Index;

            createSeparators();
            MainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            AddInternal(MainContainer); //允许多个子元素
            Scheduler.AddOnce(OnDrawableChanged);
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
                Alpha = ShowSeparators ? 1f : 0f,
                Children = [noteSeparatorsL, noteSeparatorsR]
            };

            AddInternal(LineContainer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EnabledColor.BindTo(EzSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled));
            NoteSetName.BindTo(Column.NoteSetBindable);
            NoteSize.BindTo(Column.NoteSizeBindable);

            // Use a per-column watcher to avoid creating an event handler delegate per-note.
            ColumnWatcher.GetOrCreate(Column).Add(this);

            UpdateSize();
        }

        private Colour4 lastNoteColor = Colour4.White;
        private bool lastNoteColorCached;

        private void OnNoteSetChanged()
        {
            if (string.IsNullOrEmpty(NoteSetName.Value))
                return;

            MainContainer?.Clear();
            lastNoteColorCached = false;

            Scheduler.AddOnce(OnDrawableChanged);
        }

        protected virtual void UpdateSize()
        {
            lastNoteColorCached = false;
            UpdateColor();

            // if (LineContainer?.Children != null)
            // {
            //     foreach (var child in LineContainer.Children)
            //     {
            //         if (child is EzNoteSideLine sideLine)
            //             sideLine.UpdateTrackLineHeight();
            //     }
            // }
        }

        protected virtual void UpdateColor()
        {
            // TODO： 命中HMT面条时，考虑是否跳过面头着色，只更新面身。
            // 或者单独做一个着色拓展设置，比如“仅着色面身”“着色面头和面身”“不着色”等等。
            // 或者，不着色时，使用新的开关拓展，在Middle中进一步单独管理着色
            if (MainContainer != null)
            {
                Colour4 targetColor = NoteColor;

                if (!lastNoteColorCached || lastNoteColor != targetColor)
                {
                    MainContainer.Colour = targetColor;
                    lastNoteColor = targetColor;
                    lastNoteColorCached = true;
                }
            }

            // if (LineContainer?.Children != null)
            // {
            //     foreach (var child in LineContainer.Children)
            //     {
            //         if (child is EzNoteSideLine sideLine)
            //             sideLine.UpdateGlowEffect(NoteColor);
            //     }
            // }
        }

        private void OnColourChanged()
        {
            lastNoteColorCached = false;
            UpdateColor();
        }

        private void OnNoteSizeChanged()
        {
            UpdateSize();
        }

        protected virtual Colour4 NoteColor => (EnabledColor.Value && UseColorization)
            ? Column.EzColumnColourBindable.Value
            : Colour4.White;

        protected virtual string ColorPrefix
        {
            get
            {
                if (EnabledColor.Value) return "white";

                EzColumnType keyType = EzSkinConfig.GetColumnType(KeyMode, ColumnIndex);

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

        protected virtual void OnDrawableChanged() { }

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
        internal void ForwardOnColourChanged() => OnColourChanged();
        internal void ForwardOnNoteSizeChanged() => OnNoteSizeChanged();

        private class ColumnWatcher
        {
            private readonly Column column;
            private readonly List<WeakReference<EzNoteBase>> notes = new List<WeakReference<EzNoteBase>>();

            public ColumnWatcher(Column column)
            {
                this.column = column;
                column.NoteSetChanged += onNoteSetChanged;
                column.NoteColourChanged += onNoteColourChanged;
                column.NoteSizeChanged += onNoteSizeChanged;
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

            private void onNoteSetChanged()
            {
                // iterate backwards and remove dead weak references in-place to avoid
                // allocating a large temporary array when the column has many notes.
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];
                    if (wr.TryGetTarget(out var target))
                        target.ForwardOnNoteSetChanged();
                    else
                        notes.RemoveAt(i);
                }
            }

            private void onNoteColourChanged()
            {
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];
                    if (wr.TryGetTarget(out var target))
                        target.ForwardOnColourChanged();
                    else
                        notes.RemoveAt(i);
                }
            }

            private void onNoteSizeChanged()
            {
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var wr = notes[i];
                    if (wr.TryGetTarget(out var target))
                        target.ForwardOnNoteSizeChanged();
                    else
                        notes.RemoveAt(i);
                }
            }

            public void Unsubscribe()
            {
                column.NoteSetChanged -= onNoteSetChanged;
                column.NoteColourChanged -= onNoteColourChanged;
                column.NoteSizeChanged -= onNoteSizeChanged;
            }

            public bool IsEmpty => notes.All(wr => !wr.TryGetTarget(out _));

            private static readonly Dictionary<Column, ColumnWatcher> watchers = new Dictionary<Column, ColumnWatcher>();
            private static readonly object watcher_lock = new object();

            public static ColumnWatcher GetOrCreate(Column c)
            {
                lock (watcher_lock)
                {
                    if (!watchers.TryGetValue(c, out var w))
                    {
                        w = new ColumnWatcher(c);
                        watchers[c] = w;
                    }

                    return w;
                }
            }

            public static void Remove(Column c, EzNoteBase note)
            {
                lock (watcher_lock)
                {
                    if (!watchers.TryGetValue(c, out var w))
                        return;

                    w.Remove(note);

                    if (w.IsEmpty)
                    {
                        w.Unsubscribe();
                        watchers.Remove(c);
                    }
                }
            }
        }
    }
}

