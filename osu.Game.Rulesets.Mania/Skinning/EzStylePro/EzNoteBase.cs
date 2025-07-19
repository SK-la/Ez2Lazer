// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public abstract partial class EzNoteBase : CompositeDrawable
    {
        protected virtual bool BoolUpdateColor => true;
        protected virtual bool UseColorization => true;
        protected virtual bool ShowSeparators => false;
        protected int KeyMode;
        protected int ColumnIndex;

        protected Container? SeparatorsContainer { get; private set; }
        protected Container? MainContainer { get; private set; }

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        protected StageDefinition StageDefinition { get; private set; } = null!;

        [Resolved]
        protected EzSkinSettingsManager EZSkinConfig { get; private set; } = null!;

        [Resolved]
        protected EzLocalTextureFactory Factory { get; private set; } = null!;

        private IBindable<Colour4> columnColorBindable = null!;
        protected Bindable<bool> EnabledColor = null!;
        protected Bindable<Vector2> NoteSize = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            KeyMode = StageDefinition.Columns;
            ColumnIndex = Column.Index;
            EnabledColor = EZSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            columnColorBindable = EZSkinConfig.GetColumnColorBindable(KeyMode, ColumnIndex);
            NoteSize = Factory.GetNoteSize(KeyMode, ColumnIndex);

            createSeparators();
            MainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            AddInternal(MainContainer);
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

            SeparatorsContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Stretch,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = ShowSeparators ? 1f : 0f,
                Children = [noteSeparatorsL, noteSeparatorsR]
            };

            AddInternal(SeparatorsContainer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EnabledColor.BindValueChanged(_ => UpdateColor(), true);
            columnColorBindable.BindValueChanged(_ => UpdateColor(), true);
            NoteSize.BindValueChanged(_ => UpdateSize(), true);

            Factory.OnNoteChanged += OnDrawableChanged;
            // Factory.OnNoteSizeChanged += UpdateSize;
            Scheduler.AddOnce(OnDrawableChanged);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Factory.OnNoteChanged -= OnDrawableChanged;
            // Factory.OnNoteSizeChanged -= UpdateSize;
        }

        protected Colour4 NoteColor
        {
            get
            {
                if (EnabledColor.Value && UseColorization)
                    return EZSkinConfig.GetColumnColor(KeyMode, ColumnIndex);

                return Colour4.White;
            }
        }

        protected virtual string ColorPrefix
        {
            get
            {
                if (EnabledColor.Value) return "white";

                string keyType = EZSkinConfig.GetColumnType(KeyMode, ColumnIndex);

                return keyType switch
                {
                    "A" => "white",
                    "B" => "blue",
                    "S" => "green",
                    "E" => "white",
                    "P" => "green",
                    _ => "white"
                };
            }
        }

        protected void UpdateColor()
        {
            if (BoolUpdateColor)
            {
                if (MainContainer != null)
                    MainContainer.Colour = NoteColor;

                if (SeparatorsContainer?.Children != null)
                {
                    foreach (var child in SeparatorsContainer.Children)
                    {
                        if (child is EzNoteSideLine sideLine)
                            sideLine.UpdateGlowEffect(NoteColor);
                    }
                }
            }
        }

        protected virtual void UpdateSize()
        {
            NoteSize = Factory.GetNoteSize(KeyMode, ColumnIndex);
            UpdateColor();
        }

        protected virtual void OnDrawableChanged() { }
    }
}
