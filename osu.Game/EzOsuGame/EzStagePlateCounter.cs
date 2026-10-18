// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;
using osuTK;

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
        private string? digitBase;
        private Bindable<string>? stageName;
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

            // 数字组绕支点转，数字写相对支点的偏移。没有声明 pivot 时支点就是自身位置，
            // 与旧行为逐像素一致（此时旋转为 0，或绕个位数字转）。
            Vector2 pivot = layout.ToOsuPivot();

            var digits = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = pivot,
                Rotation = layout.Rotation,
            };

            for (int place = 0; place < count; place++)
            {
                places[place] = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = EzStagePlateElement.ToOsu(layout.X + place * layout.Spacing, layout.Y) - pivot,
                };
            }

            digits.AddRange(places);
            InternalChild = digits;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stageName = config.GetBindable<string>(Ez2Setting.StageName);
            stageName.BindValueChanged(_ => reloadGlyphs(), true);

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

        private void reloadGlyphs()
        {
            loadGlyphs();
            apply(shownValue);
        }

        // 字形在 Stage/{舞台名} 下。layout 的 path 可能仍是 Unity 旧目录名，取不到时按
        // number/score、number/combo、number 逐级回退（本地整理过的库把 maxcombo 统一成了 combo）。
        private void loadGlyphs()
        {
            digitBase = chooseDigitBase();

            for (int digit = 0; digit < glyphs.Length; digit++)
                glyphs[digit] = digitBase == null ? null : resources.Get($"{digitBase}{digit}", EzTextureUsage.Glyph);
        }

        private string? chooseDigitBase()
        {
            string? body = stageName?.Value;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            string? declared = layout.Path?.Trim().Trim('/');

            if (!string.IsNullOrEmpty(declared))
            {
                string? resolved = digitBaseIf(body, declared);

                if (resolved != null)
                    return resolved;
            }

            return digitBaseIf(body, source == Source.Score ? "number/score" : "number/combo")
                   ?? digitBaseIf(body, "number");
        }

        // 只有 0 号图存在才算该目录可用，避免把空目录当成字源。
        private string? digitBaseIf(string stage, string path)
        {
            string root = $"Stage/{stage}/";
            return resources.Get($"{root}{path}/0", EzTextureUsage.Glyph) != null ? $"{root}{path}/" : null;
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
