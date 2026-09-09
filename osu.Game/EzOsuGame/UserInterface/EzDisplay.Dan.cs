// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Dan label container (separate from skill chip). Badge art from <see cref="EzResourceStore"/> (<c>EzResources/Dans</c>).
    /// </summary>
    public partial class EzDisplayDan : CompositeDrawable
    {
        private readonly Box background;
        private readonly Container contentPad;
        private readonly Sprite badge;
        private readonly OsuSpriteText bareText;
        private readonly OsuSpriteText suffixText;

        private EzResourceStore? resources;
        private TextureStore? textures;

        private string? pendingLabel;
        private int pendingKeyCount = 4;
        private EzDanSide pendingSide = EzDanSide.Rc;
        private bool hasPendingLabel;

        private bool preferImage = true;

        /// <summary>When true, prefer badge texture over bare text (retries after DI load).</summary>
        public bool PreferImage
        {
            get => preferImage;
            set
            {
                if (preferImage == value)
                    return;

                preferImage = value;

                if (hasPendingLabel)
                    applyPendingLabel();
            }
        }

        public float BadgeSize { get; set; } = 22f;

        public EzDisplayDan()
        {
            AutoSizeAxes = Axes.Both;

            badge = new Sprite
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = Vector2.Zero,
                Alpha = 0,
                FillMode = FillMode.Fit,
            };

            bareText = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
            };

            suffixText = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                Margin = new MarginPadding { Left = 1 },
            };

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.4f),
                    },
                    contentPad = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 5, Vertical = 2 },
                        Child = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(3, 0),
                            Children = new Drawable[]
                            {
                                badge,
                                bareText,
                                suffixText,
                            }
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(EzResourceStore? resources, TextureStore? textures)
        {
            this.resources = resources;
            this.textures = textures;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (hasPendingLabel)
                applyPendingLabel();
        }

        public void SetLabel(string label, int keyCount = 4, EzDanSide side = EzDanSide.Rc)
        {
            pendingLabel = label;
            pendingKeyCount = keyCount;
            pendingSide = side;
            hasPendingLabel = true;

            applyPendingLabel();
        }

        public void SetLabel(string label, int keyCount, string sideId)
            => SetLabel(label, keyCount, EzDanSideExtensions.ParseOrRc(sideId));

        private void applyPendingLabel()
        {
            if (!hasPendingLabel || string.IsNullOrEmpty(pendingLabel))
                return;

            string bare = EzDanLadders.BareLabel(pendingLabel);
            string suffix = EzDanLadders.TierSuffix(pendingLabel);

            bareText.Text = bare;
            bareText.Alpha = 1;
            suffixText.Text = suffix;

            var tierColour = EzDanLadders.TierColour(suffix);
            bareText.Colour = Colour4.White;
            suffixText.Colour = tierColour ?? Colour4.White.Opacity(0.85f);
            suffixText.Alpha = string.IsNullOrEmpty(suffix) ? 0 : 1;

            updateBadge(pendingKeyCount, pendingSide, bare);
        }

        private void updateBadge(int keyCount, EzDanSide side, string bare)
        {
            badge.Texture = null;
            badge.Size = Vector2.Zero;
            badge.Alpha = 0;
            background.Alpha = 1;
            contentPad.Padding = new MarginPadding { Horizontal = 5, Vertical = 2 };

            if (!PreferImage)
                return;

            // DI may not be ready yet (e.g. SetLabel from a parent ctor); LoadComplete retries.
            if (resources == null && textures == null && LoadState < LoadState.Ready)
                return;

            string? relative = EzDanLadders.TryGetTexturePath(keyCount, side, bare);
            if (relative == null)
                return;

            // User EzResources/Dans/… then bundled Textures/EzResources/Dans/…
            Texture? texture = resources?.Get(relative, EzTextureUsage.Atlas);
            texture ??= textures?.Get($"EzResources/{relative}");

            // MSBuild embeds folders whose names start with a digit as _6k / _7k.
            if (texture == null)
            {
                string? embeddedRelative = EzDanLadders.TryGetEmbeddedTexturePath(relative);
                if (embeddedRelative != null)
                    texture = textures?.Get($"EzResources/{embeddedRelative}");
            }

            if (texture == null)
                return;

            badge.Texture = texture;
            badge.Size = new Vector2(BadgeSize);
            badge.Alpha = 1;
            bareText.Text = string.Empty;
            bareText.Alpha = 0;
            background.Alpha = 0;
            contentPad.Padding = new MarginPadding { Horizontal = 1, Vertical = 0 };
        }
    }
}
