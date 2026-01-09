// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Configuration
{
    /// <summary>
    /// A helper class for tracking changes to the settings of a set of <see cref="Mod"/>s.
    /// </summary>
    /// <remarks>
    /// Ensure to dispose when usage is finished.
    /// </remarks>
    public class ModSettingChangeTracker : IDisposable
    {
        /// <summary>
        /// Notifies that the setting of a <see cref="Mod"/> has changed.
        /// </summary>
        public Action<Mod> SettingChanged;

        private readonly List<(object bindable, EventInfo eventInfo, Delegate handler)> subscriptions = new List<(object, EventInfo, Delegate)>();
        private readonly List<object> keepAlive = new List<object>();

        /// <summary>
        /// Creates a new <see cref="ModSettingChangeTracker"/> for a set of <see cref="Mod"/>s.
        /// </summary>
        /// <param name="mods">The set of <see cref="Mod"/>s whose settings need to be tracked.</param>
        public ModSettingChangeTracker(IEnumerable<Mod> mods)
        {
            foreach (var mod in mods)
            {
                // Prefer binding directly to the setting source bindables.
                // This catches both UI-driven and programmatic changes (e.g. hotkeys / code paths).
                try
                {
                    foreach (var (_, property) in mod.GetSettingsSourceProperties())
                    {
                        object value;

                        try
                        {
                            value = property.GetValue(mod);
                        }
                        catch
                        {
                            continue;
                        }

                        if (value == null)
                            continue;

                        trySubscribeValueChanged(mod, value);
                    }
                }
                catch
                {
                    // If reflection fails for some mod, ignore.
                    // Mod list changes (enable/disable) are handled elsewhere.
                }
            }
        }

        private void trySubscribeValueChanged(Mod mod, object bindable)
        {
            // Find IBindable<T> interface.
            var bindableInterface = bindable.GetType().GetInterfaces()
                                            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IBindable<>));

            if (bindableInterface == null)
                return;

            var genericArg = bindableInterface.GetGenericArguments()[0];
            var handlerOwner = new valueChangedProxy(this, mod);
            keepAlive.Add(handlerOwner);

            var method = typeof(valueChangedProxy).GetMethod(nameof(valueChangedProxy.OnValueChanged), BindingFlags.Instance | BindingFlags.NonPublic);
            var genericMethod = method?.MakeGenericMethod(genericArg);
            if (genericMethod == null)
                return;

            var valueChangedEventType = typeof(ValueChangedEvent<>).MakeGenericType(genericArg);
            var actionType = typeof(Action<>).MakeGenericType(valueChangedEventType);
            var handler = Delegate.CreateDelegate(actionType, handlerOwner, genericMethod);

            // Prefer subscribing via the actual runtime event (Bindable<T>.ValueChanged).
            var evt = bindable.GetType().GetEvent(nameof(IBindable<int>.ValueChanged));
            if (evt == null)
                return;

            evt.AddEventHandler(bindable, handler);
            subscriptions.Add((bindable, evt, handler));
        }

        public void Dispose()
        {
            SettingChanged = null;

            foreach (var (bindable, eventInfo, handler) in subscriptions)
            {
                try
                {
                    eventInfo.RemoveEventHandler(bindable, handler);
                }
                catch
                {
                    // ignore
                }
            }

            subscriptions.Clear();
            keepAlive.Clear();
        }

        private sealed class valueChangedProxy
        {
            private readonly ModSettingChangeTracker tracker;
            private readonly Mod mod;

            public valueChangedProxy(ModSettingChangeTracker tracker, Mod mod)
            {
                this.tracker = tracker;
                this.mod = mod;
            }

            // Kept public so the outer tracker can create a delegate.
            // ReSharper disable once UnusedMember.Local
            public void OnValueChanged<T>(ValueChangedEvent<T> _)
            {
                tracker.SettingChanged?.Invoke(mod);
            }
        }
    }
}
