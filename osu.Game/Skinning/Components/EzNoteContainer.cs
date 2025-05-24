// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Skinning.Components
{
    public partial class EzNoteContainer : CompositeDrawable
    {
        public readonly EzGetNoteTexture NotePart;
        public Bindable<EzSelectorNameSet> ThemeName { get; } = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)7);
        public Bindable<string> NoteType { get; } = new Bindable<string>("default");

        public EzNoteContainer(Bindable<EzSelectorNameSet>? externalThemeName = null, Bindable<string>? externalNoteType = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalThemeName is not null)
                ThemeName.BindTo(externalThemeName);

            if (externalNoteType is not null)
                NoteType.BindTo(externalNoteType);

            var themeNameString = new Bindable<string>();
            ThemeName.BindValueChanged(e => themeNameString.Value = e.NewValue.ToString(), true);

            NotePart = new EzGetNoteTexture(noteLookup, themeNameString, NoteType);

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = NotePart
                },
            };
        }

        private string noteLookup(string noteType)
        {
            switch (noteType)
            {
                case "hold": return "hold";
                case "hold_head": return "hold_head";
                case "hold_tail": return "hold_tail";
                case "hold_body": return "hold_body";
                default: return "normal";
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 可以在这里添加缩放或其他初始化逻辑
            ThemeName.BindValueChanged(e =>
            {
                NotePart.ThemeName.Value = e.NewValue.ToString();
                NotePart.Invalidate();
            }, true);

            NoteType.BindValueChanged(e =>
            {
                NotePart.NoteName.Value = e.NewValue;
                NotePart.Invalidate();
            }, true);
        }
    }
}
