// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Judgements
{
    public abstract partial class LAsJudgementText : CompositeDrawable, ISerialisableDrawable
    {
        protected readonly HitResult Result;

        protected SpriteText JudgementText { get; private set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public bool UsesFixedAnchor { get; set; }

        protected LAsJudgementText(HitResult result)
        {
            Result = result;
        }

        protected LAsJudgementText()
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(JudgementText = CreateJudgementText());

            JudgementText.Colour = colours.ForHitResult(Result);
            JudgementText.Text = Result.GetDescription().ToUpperInvariant();
        }

        protected abstract SpriteText CreateJudgementText();
    }
}
