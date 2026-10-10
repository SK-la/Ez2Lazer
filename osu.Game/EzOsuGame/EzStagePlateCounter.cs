// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame
{
    // 个位锚在 layout 坐标上，更高位沿 +spacing。分数读 TotalScore，combo 读 HighestCombo。
    internal partial class EzStagePlateCounter : CompositeDrawable
    {
        internal enum Source
        {
            Score,
            MaxCombo,
        }

        private const int max_places = 12;

        private readonly EzStagePlateDigits layout;
        private readonly Source source;
        private readonly Sprite[] places;
        private readonly Texture?[] glyphs = new Texture?[10];

        private long shownValue;
        private Bindable<EzEnumGameThemeName>? theme;
        private IBindable<long>? score;
        private IBindable<int>? maxCombo;

        [Resolved]
        private EzResourceStore resources { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager config { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        public EzStagePlateCounter(EzStagePlateDigits layout, Source source)
        {
            this.layout = layout;
            this.source = source;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            int count = layout.Digits > max_places ? max_places : layout.Digits;
            places = new Sprite[count];

            for (int place = 0; place < count; place++)
            {
                places[place] = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = EzStagePlateSprite.ToOsu(layout.X + place * layout.Spacing, layout.Y),
                };
            }

            InternalChildren = places;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            theme = config.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName).GetBoundCopy();
            theme.BindValueChanged(_ =>
            {
                loadGlyphs();
                apply(shownValue);
            }, true);

            if (scoreProcessor == null)
            {
                apply(0);
                return;
            }

            if (source == Source.Score)
            {
                score = scoreProcessor.TotalScore.GetBoundCopy();
                score.BindValueChanged(e => apply(e.NewValue), true);
            }
            else
            {
                maxCombo = scoreProcessor.HighestCombo.GetBoundCopy();
                maxCombo.BindValueChanged(e => apply(e.NewValue), true);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            theme?.UnbindAll();
            score?.UnbindAll();
            maxCombo?.UnbindAll();

            base.Dispose(isDisposing);
        }

        private void loadGlyphs()
        {
            string theme = config.Get<EzEnumGameThemeName>(Ez2Setting.GameThemeName).ToString().Replace(' ', '_');
            string root = $"GameTheme/{theme}/number/";

            for (int digit = 0; digit < glyphs.Length; digit++)
                glyphs[digit] = loadDigit(root, digit);
        }

        private Texture? loadDigit(string root, int digit)
        {
            if (source == Source.Score)
            {
                return resources.Get($"{root}score/{digit}", EzTextureUsage.Glyph)
                       ?? resources.Get($"{root}{digit}", EzTextureUsage.Glyph);
            }

            return resources.Get($"{root}maxcombo/{digit}", EzTextureUsage.Glyph)
                   ?? resources.Get($"{root}combo/{digit}", EzTextureUsage.Glyph)
                   ?? resources.Get($"{root}{digit}", EzTextureUsage.Glyph);
        }

        private void apply(long value)
        {
            if (value < 0)
                value = 0;

            shownValue = value;

            for (int place = 0; place < places.Length; place++)
            {
                places[place].Texture = glyphs[value % 10];
                value /= 10;
            }
        }
    }
}
