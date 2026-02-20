using System;
using System.Collections;
using System.Collections.Generic;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Skinning
{
    public class SkinScript : IDisposable
    {
        private readonly Script luaScript;
        private readonly ISkinScriptHost host;
        private readonly HashSet<string> subscribedEvents = new HashSet<string>(StringComparer.Ordinal);

        public string ScriptName { get; }

        public string Description { get; private set; } = "No description provided";

        public bool IsActivated { get; private set; }

        public string? ActivationError { get; private set; }

        public bool IsEnabled { get; set; } = true;

        public SkinScript(string scriptContent, string scriptName, ISkinScriptHost host)
        {
            ScriptName = scriptName;
            this.host = host;

            luaScript = new Script(CoreModules.Preset_SoftSandbox);

            UserData.RegisterType<SkinScriptInterface>();
            UserData.RegisterType<LuaDrawableProxy>();
            UserData.RegisterType<LuaTypeInfo>();
            UserData.RegisterType<LuaJudgementResultProxy>();
            UserData.RegisterType<LuaHitObjectProxy>();
            UserData.RegisterType<ManiaSkinScriptExtensions>();
            luaScript.Globals["osu"] = new SkinScriptInterface(host, subscribeToEvent);

            if (string.Equals(host.CurrentRuleset?.Name, "mania", StringComparison.OrdinalIgnoreCase))
                luaScript.Globals["mania"] = new ManiaSkinScriptExtensions(host);

            try
            {
                luaScript.DoString(scriptContent);

                var description = luaScript.Globals.Get("SCRIPT_DESCRIPTION");
                if (description.Type == DataType.String)
                    Description = description.String;
            }
            catch (Exception ex)
            {
                ActivationError = $"SkinScript Load Failed: {ex}";
                return;
            }

            if (!invokeActivation())
                return;

            IsActivated = true;
        }

        public void NotifyComponentLoaded(Drawable component)
        {
            if (!IsEnabled)
                return;

            invoke("onComponentLoaded", component);
        }

        public void Update()
        {
            if (!IsEnabled)
                return;

            invokeNoArg("update");
        }

        public void NotifyJudgement(JudgementResult result)
        {
            if (!IsEnabled)
                return;

            invoke("onJudgement", result);
        }

        public void NotifyGameEvent(string eventName, IReadOnlyDictionary<string, object?>? data = null)
        {
            if (!IsEnabled || !IsSubscribedToEvent(eventName))
                return;

            invoke("onGameEvent", eventName, data ?? new Dictionary<string, object?>());
        }

        public void NotifyInputEvent(IReadOnlyDictionary<string, object?> eventData)
        {
            if (!IsEnabled)
                return;

            invoke("onInputEvent", eventData);
        }

        public bool IsSubscribedToEvent(string eventName) => subscribedEvents.Contains(eventName);

        private void subscribeToEvent(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            subscribedEvents.Add(eventName);
        }

        private void invokeNoArg(string functionName) => invoke(functionName);

        private bool invokeActivation()
        {
            try
            {
                var function = luaScript.Globals.Get("onLoad");
                if (function.Type != DataType.Function)
                    return true;

                luaScript.Call(function);
                return true;
            }
            catch (Exception ex)
            {
                ActivationError = $"SkinScript onLoad Activation Failed: {ex.Message}";
                return false;
            }
        }

        private void invoke(string functionName, params object[] args)
        {
            try
            {
                var function = luaScript.Globals.Get(functionName);
                if (function.Type != DataType.Function)
                    return;

                DynValue[] convertedArgs = new DynValue[args.Length];

                for (int i = 0; i < args.Length; i++)
                    convertedArgs[i] = convertToDynValue(args[i]);

                luaScript.Call(function, convertedArgs);
            }
            catch (Exception ex)
            {
                host.Log($"[SkinScript] Callback error in {ScriptName}.{functionName}: {ex.Message}", SkinScriptLogLevel.Error);
            }
        }

        private DynValue convertToDynValue(object? argument)
        {
            if (argument == null)
                return DynValue.Nil;

            if (argument is Drawable drawable)
                return UserData.Create(new LuaDrawableProxy(drawable));

            if (argument is JudgementResult judgementResult)
                return UserData.Create(new LuaJudgementResultProxy(judgementResult));

            if (argument is HitObject hitObject)
                return UserData.Create(new LuaHitObjectProxy(hitObject));

            if (argument is IReadOnlyDictionary<string, object?> readonlyDictionary)
                return createTableValue(readonlyDictionary);

            if (argument is IDictionary dictionary)
                return createTableValue(dictionary);

            return argument switch
            {
                string str => DynValue.NewString(str),
                bool boolean => DynValue.NewBoolean(boolean),
                byte number => DynValue.NewNumber(number),
                sbyte number => DynValue.NewNumber(number),
                short number => DynValue.NewNumber(number),
                ushort number => DynValue.NewNumber(number),
                int number => DynValue.NewNumber(number),
                uint number => DynValue.NewNumber(number),
                long number => DynValue.NewNumber(number),
                ulong number => DynValue.NewNumber(number),
                float number => DynValue.NewNumber(number),
                double number => DynValue.NewNumber(number),
                decimal number => DynValue.NewNumber((double)number),
                _ => DynValue.NewString(argument.ToString() ?? string.Empty),
            };
        }

        private DynValue createTableValue(IReadOnlyDictionary<string, object?> dictionary)
        {
            Table table = new Table(luaScript);

            foreach (var item in dictionary)
                table.Set(item.Key, convertToDynValue(item.Value));

            return DynValue.NewTable(table);
        }

        private DynValue createTableValue(IDictionary dictionary)
        {
            Table table = new Table(luaScript);

            foreach (DictionaryEntry item in dictionary)
            {
                string key = item.Key.ToString() ?? string.Empty;
                table.Set(key, convertToDynValue(item.Value));
            }

            return DynValue.NewTable(table);
        }

        [MoonSharpUserData]
        private class LuaTypeInfo
        {
            public readonly string Name;

            public override string ToString() => Name;

            public LuaTypeInfo(string name)
            {
                Name = name;
            }
        }

        [MoonSharpUserData]
        private class LuaDrawableProxy
        {
            private readonly Drawable drawable;

            public LuaTypeInfo Type => new LuaTypeInfo(drawable.GetType().Name);

            public double Alpha
            {
                get => drawable.Alpha;
                set => drawable.Alpha = (float)value;
            }

            public int? Column => readNullable<int>("Column");

            public double? StartTime => readNullable<double>("StartTime");

            public double? EndTime => readNullable<double>("EndTime");

            [MoonSharpUserDataMetamethod("__newindex")]
            public void SetValue(string key, DynValue value)
            {
                if (string.Equals(key, "Colour", StringComparison.OrdinalIgnoreCase))
                {
                    if (tryReadColour(value, out var colour))
                        drawable.Colour = colour;

                    return;
                }

                if (string.Equals(key, "Alpha", StringComparison.OrdinalIgnoreCase) && value.Type == DataType.Number)
                {
                    Alpha = value.Number;
                }
            }

            public LuaDrawableProxy(Drawable drawable)
            {
                this.drawable = drawable;
            }

            private T? readNullable<T>(string propertyName)
                where T : struct
            {
                var property = drawable.GetType().GetProperty(propertyName);

                if (property == null)
                    return null;

                object? rawValue = property.GetValue(drawable);
                if (rawValue == null)
                    return null;

                return rawValue switch
                {
                    T typedValue => typedValue,
                    IConvertible convertible => (T)Convert.ChangeType(convertible, typeof(T)),
                    _ => null,
                };
            }

            private static bool tryReadColour(DynValue value, out Colour4 colour)
            {
                colour = Colour4.White;

                if (value.Type != DataType.Table)
                    return false;

                float readChannel(string key, float fallback)
                {
                    DynValue channel = value.Table.Get(key);
                    return channel.Type == DataType.Number ? (float)channel.Number : fallback;
                }

                colour = new Colour4(
                    readChannel("R", 1f),
                    readChannel("G", 1f),
                    readChannel("B", 1f),
                    readChannel("A", 1f));

                return true;
            }
        }

        [MoonSharpUserData]
        private class LuaJudgementResultProxy
        {
            private readonly JudgementResult result;

            public string Type => result.Type.ToString();

            public LuaHitObjectProxy HitObject => new LuaHitObjectProxy(result.HitObject);

            public LuaJudgementResultProxy(JudgementResult result)
            {
                this.result = result;
            }
        }

        [MoonSharpUserData]
        private class LuaHitObjectProxy
        {
            private readonly HitObject hitObject;

            public int? Column => readNullable<int>("Column");

            public double? StartTime => readNullable<double>("StartTime");

            public double? EndTime => readNullable<double>("EndTime");

            public LuaHitObjectProxy(HitObject hitObject)
            {
                this.hitObject = hitObject;
            }

            private T? readNullable<T>(string propertyName)
                where T : struct
            {
                var property = hitObject.GetType().GetProperty(propertyName);

                if (property == null)
                    return null;

                object? rawValue = property.GetValue(hitObject);
                if (rawValue == null)
                    return null;

                return rawValue switch
                {
                    T typedValue => typedValue,
                    IConvertible convertible => (T)Convert.ChangeType(convertible, typeof(T)),
                    _ => null,
                };
            }
        }

        public void Dispose()
        {
            luaScript.Globals.Clear();
        }
    }
}
