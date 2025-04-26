// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using OpenTabletDriver.Plugin;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Skinning.Components
{
    public partial class EzCounterText : CompositeDrawable, IHasText
    {
        public readonly EzTextureSprite TextPart;
        public Bindable<string> FontName { get; } = new Bindable<string>("EZ2DJ-4th");

        public FillFlowContainer TextContainer { get; private set; }

        // public float DefaultWidth { get; set; } = 100; // 默认宽度

        public LocalisableString Text
        {
            get => TextPart.Text;
            set => TextPart.Text = value;
        }

        // public object Spacing { get; set; }

        public EzCounterText(Bindable<string>? externalFontName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            if (externalFontName is not null)
                FontName.BindTo(externalFontName);

            InternalChildren = new Drawable[]
            {
                TextContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(2),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,

                    Children = new Drawable[]
                    {
                        TextPart = new EzTextureSprite(textLookup, FontName)
                        {
                            Scale = new Vector2(2.2f),
                            Padding = new MarginPadding(1),
                        }
                    }
                },
            };
        }

        private string textLookup(char c)
        {
            switch (c)
            {
                case '.': return @"dot";

                case '%': return @"percentage";

                case 'c': return @"Combo";

                case 'e': return @"Early";

                case 'l': return @"Late";

                default: return c.ToString();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // textPart.Width = DefaultWidth;

            FontName.BindValueChanged(e =>
            {
                TextPart.FontName.Value = e.NewValue;
                // textPart.LoadAsync(); // **强制重新加载字体**
                TextPart.Invalidate(); // **确保 UI 立即刷新**
            }, true);
        }

        public Vector2 Spacing
        {
            get => TextContainer.Spacing;
            set => TextContainer.Spacing = value;
        }
    }

    public partial class OffsetNumberNameSelector : SettingsDropdown<string>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items = new List<string>
            {
                "Air",
                "Aqua",
                "Cricket",
                "D2D",
                "DarkConcert",
                "DJMAX",
                "EMOTIONAL",
                "ENDLESS",
                "EZ2AC-AE",
                "EZ2AC-CV",
                "EZ2AC-EVOLVE",
                "EZ2AC-TT",
                "EZ2DJ-1thSE",
                "EZ2DJ-2nd",
                "EZ2DJ-4th",
                "EZ2DJ-6th",
                "EZ2DJ-7th",
                "EZ2DJ-Platinum",
                "EZ2ON",
                "F3-ANCIENT",
                "F3-CONTEMP",
                "F3-FUTURE",
                "F3-MODERN",
                "FiND A WAY",
                "FORTRESS2",
                "GC-TYPE2",
                "Gem",
                "Gem2",
                "Gold",
                "HX STD",
                "HX STD黄色",
                "HX-1121",
                "Kings",
                "M250",
                "NIGHTFALL",
                "NIGHTwhite",
                "QTZ-02",
                "REBOOT",
                "REBOOT GOLD",
                "SG-701",
                "SH-512",
                "Star",
                "TCEZ-001",
                "TECHNIKA",
                "Tomato",
                "VariousWays",
            };
            Log.Debug("Items", Items.ToString());
        }
    }

    public enum EffectType
    {
        Scale,
        Bounce,
        None
    }

    public partial class AnchorDropdown : SettingsDropdown<Anchor>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 限制选项范围
            Items = new List<Anchor>
            {
                Anchor.TopCentre,
                Anchor.Centre,
                Anchor.BottomCentre
            };
        }
    }
}
