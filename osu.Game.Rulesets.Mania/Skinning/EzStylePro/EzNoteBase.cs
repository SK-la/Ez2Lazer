// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        // private IBindable<Colour4> columnColorBindable = null!;
        protected Bindable<bool> EnabledColor = null!;
        protected Bindable<Vector2> NoteSize = null!;
        protected Bindable<string> NoteSetName = null!;
        protected int KeyMode;
        protected int ColumnIndex;

        [BackgroundDependencyLoader]
        private void load()
        {
            KeyMode = StageDefinition.Columns;
            ColumnIndex = Column.Index;
            // bindables are assigned in LoadComplete to ensure Column has initialised them

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

            EnabledColor = Column.ColorEnabledBindable;
            // columnColorBindable = EzSkinConfig.GetColumnColorBindable(KeyMode, ColumnIndex);
            NoteSetName = Column.NoteSetBindable;
            NoteSize = Column.NoteSizeBindable;

            Column.NoteSetChanged += OnNoteSetChanged;
            Column.NoteColourChanged += OnColourChanged;
            Column.NoteSizeChanged += OnNoteSizeChanged;

            UpdateSize();
        }

        private Colour4 lastNoteColor = Colour4.White;
        private bool lastNoteColorCached;

        private void OnNoteSetChanged(ValueChangedEvent<string> obj)
        {
            if (string.IsNullOrEmpty(obj.NewValue))
                return;

            MainContainer?.Clear();
            lastNoteColorCached = false;

            Scheduler.AddOnce(OnDrawableChanged);
        }

        protected virtual void UpdateSize()
        {
            lastNoteColorCached = false;
            UpdateColor();
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

            if (LineContainer?.Children != null)
            {
                foreach (var child in LineContainer.Children)
                {
                    if (child is EzNoteSideLine sideLine)
                        sideLine.UpdateGlowEffect(NoteColor);
                }
            }

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
            ? EzSkinConfig.GetColumnColor(KeyMode, ColumnIndex)
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
                Column.NoteSetChanged -= OnNoteSetChanged;
                Column.NoteColourChanged -= OnColourChanged;
                Column.NoteSizeChanged -= OnNoteSizeChanged;
            }

            base.Dispose(isDisposing);
        }
    }
}

