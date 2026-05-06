// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    internal static class TestReflectionHelpers
    {
        public static BmsKeysoundManager CreateUninitialisedBmsKeysoundManager()
        {
#pragma warning disable SYSLIB0050
            var manager = (BmsKeysoundManager)FormatterServices.GetUninitializedObject(typeof(BmsKeysoundManager));
#pragma warning restore SYSLIB0050

            SetField(manager, "keysoundCache", new Dictionary<string, osu.Framework.Audio.Sample.ISample>());
            SetField(manager, "keysoundPlayTimes", new Dictionary<string, double>());
            SetField(manager, "backgroundEvents", new List<BmsBackgroundSoundEvent>());
            SetField(manager, "sampleVolume", 1d);
            SetField(manager, "currentOffset", 0d);
            SetField(manager, "gameplayTime", 0d);
            SetField(manager, "nextBackgroundIndex", 0);
            SetField(manager, "lastBackgroundUpdateTime", double.MinValue);
            SetField(manager, "loggedMissingBackgroundEvents", false);
            SetField(manager, "loggedFirstBackgroundUpdate", false);
            SetField(manager, "isDisposed", false);

            return manager;
        }

        public static void SetField<T>(object target, string fieldName, T value)
            => target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                     .SetValue(target, value);

        public static T GetField<T>(object target, string fieldName)
            => (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                          .GetValue(target)!;
    }
}
