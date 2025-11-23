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
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

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
        protected EzSkinSettingsManager EzSkinConfig { get; private set; } = null!;

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
            EnabledColor = EzSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            // columnColorBindable = EzSkinConfig.GetColumnColorBindable(KeyMode, ColumnIndex);
            NoteSetName = EzSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);

            createSeparators();
            MainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            AddInternal(MainContainer); //允许多个子元素

            UpdateSize();
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

            NoteSetName.BindValueChanged(OnNoteChanged);
            NoteSize.BindValueChanged(_ => UpdateSize(), true);
            EnabledColor.BindValueChanged(_ => UpdateColor(), true);
            // columnColorBindable.BindValueChanged(_ => UpdateColor(), true);
        }

        private void OnNoteChanged(ValueChangedEvent<string> obj)
        {
            if (string.IsNullOrEmpty(obj.NewValue))
                return;

            MainContainer?.Clear();

            Scheduler.AddOnce(OnDrawableChanged);
        }

        protected virtual void UpdateSize()
        {
            NoteSize = Factory.GetNoteSize(KeyMode, ColumnIndex);
            UpdateColor();
        }

        protected void UpdateColor()
        {
            if (BoolUpdateColor)
            {
                if (MainContainer != null)
                    MainContainer.Colour = NoteColor;

                if (LineContainer?.Children != null)
                {
                    foreach (var child in LineContainer.Children)
                    {
                        if (child is EzNoteSideLine sideLine)
                            sideLine.UpdateGlowEffect(NoteColor);
                    }
                }
            }
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
            base.Dispose(isDisposing);

            EzLocalTextureFactory.ClearGlobalCache();
        }
    }
}
