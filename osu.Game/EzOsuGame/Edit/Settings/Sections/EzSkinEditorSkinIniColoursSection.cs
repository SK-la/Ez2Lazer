// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// Edits the legacy <c>[Colours]</c> section. Independent from Ez colour settings.
    /// </summary>
    public partial class EzSkinEditorSkinIniColoursSection : EzSkinEditorSkinIniSectionBase
    {
        private readonly Dictionary<string, Bindable<Colour4>> colourFields = new Dictionary<string, Bindable<Colour4>>();

        public EzSkinEditorSkinIniColoursSection(EzSkinIniSession? session)
            : base(session)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new OsuSpriteText
            {
                Text = "skin.ini 颜色（[Colours]）。与 Ez 颜色设置无关。",
                Font = OsuFont.Default.With(size: 14),
                Colour = Color4.Gray,
            });

            foreach (string key in EzSkinIniFieldCatalog.ColourKeys)
            {
                var bindable = new Bindable<Colour4>(Colour4.White);
                colourFields[key] = bindable;
                bindable.ValueChanged += e => CommitColourField(key, e.NewValue);
                Add(CreateColourField(key, bindable));
            }

            reloadFromSession();
        }

        private void reloadFromSession()
        {
            WithApplying(() =>
            {
                var document = ParseDocument();

                foreach (var (key, bindable) in colourFields)
                {
                    if (document != null && document.TryGetColourValue(key, out var colour))
                        bindable.Value = colour;
                    else
                        bindable.Value = Colour4.White;
                }
            });
        }
    }
}
