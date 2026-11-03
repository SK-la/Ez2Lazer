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
    /// Routes Mania/internal skin samples separately from external BMS keysounds.
    /// <see cref="ConvertHitObjectParser.FileHitSampleInfo"/> is resolved only from the prepared
    /// <see cref="BmsKeysoundManager"/> (external folder). All other samples go to <see cref="Inner"/> only.
    /// </summary>
    public sealed class BMSExternalSampleSkin : ISkin
    {
        public BMSExternalSampleSkin(ISkin inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>Mania / legacy skin for non-BMS-file samples.</summary>
        public ISkin Inner { get; }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => Inner.GetDrawableComponent(lookup);

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => Inner.GetTexture(componentName, wrapModeS, wrapModeT);

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull => Inner.GetConfig<TLookup, TValue>(lookup);

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            if (sampleInfo is ConvertHitObjectParser.FileHitSampleInfo fileSample)
            {
                var prepared = BmsRuntimeAudioContext.KeysoundManager?.GetPreparedSample(fileSample.Filename);
                if (prepared != null)
                    return prepared;

                // Fallback to the beatmap skin (BMSSkin) when runtime context is not registered yet.
                return Inner.GetSample(sampleInfo);
            }

            return Inner.GetSample(sampleInfo);
        }
    }
}
