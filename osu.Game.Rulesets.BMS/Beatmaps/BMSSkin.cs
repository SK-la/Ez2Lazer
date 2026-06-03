// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// External BMS folder sample skin. Only resolves <see cref="ConvertHitObjectParser.FileHitSampleInfo"/>
    /// from a prepared <see cref="BmsKeysoundManager"/> cache — no internal/user skin fallback.
    /// </summary>
    public class BMSSkin : ISkin
    {
        private readonly BmsKeysoundManager keysoundManager;

        public BMSSkin(BmsKeysoundManager keysoundManager)
        {
            this.keysoundManager = keysoundManager ?? throw new ArgumentNullException(nameof(keysoundManager));
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            if (sampleInfo is not ConvertHitObjectParser.FileHitSampleInfo fileSample)
                return null;

            return keysoundManager.GetPreparedSample(fileSample.Filename);
        }

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            return null;
        }
    }
}
