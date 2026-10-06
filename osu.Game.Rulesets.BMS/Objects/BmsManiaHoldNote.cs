// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.BMS.Objects
{
    /// <summary>
    /// Mania hold note with BMS keysound samples attached.
    /// </summary>
    public class BmsManiaHoldNote : HoldNote, IBmsKeysoundProvider
    {
        public bool IsScratch { get; init; }

        // 私有字段 + init 属性：对外仍是初始化后不可替换，但 Clone() 能给副本换上自己的列表。
        private List<HitSampleInfo> keysoundSamples = new List<HitSampleInfo>();

        public List<HitSampleInfo> KeysoundSamples
        {
            get => keysoundSamples;
            init => keysoundSamples = value;
        }

        IReadOnlyList<HitSampleInfo> IBmsKeysoundProvider.KeysoundSamples => KeysoundSamples;

        public override IList<HitSampleInfo> AuxiliarySamples => KeysoundSamples;

        public override BmsManiaHoldNote Clone()
        {
            var clone = (BmsManiaHoldNote)base.Clone();

            // 元素是不可变的 HitSampleInfo，共享元素即可，但不能共享列表本身。
            clone.keysoundSamples = new List<HitSampleInfo>(keysoundSamples);

            return clone;
        }
    }
}
