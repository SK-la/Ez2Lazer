using System;
using System.IO;
using MoonSharp.Interpreter;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Scoring;

namespace osu.Game.Skinning.Scripting
{
    /// <summary>
    /// Represents a Lua script that can customize skin behavior.
    /// </summary>
    public class SkinScript : IDisposable
    {
        private readonly Script luaScript;
        private readonly ISkinScriptHost host;

        /// <summary>
        /// Gets the name of the script (usually the filename).
        /// </summary>
        public string ScriptName { get; }

        /// <summary>
        /// Gets the description of the script, if provided.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the script is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkinScript"/> class.
        /// </summary>
        /// <param name="scriptContent">The Lua script content.</param>
        /// <param name="scriptName">The name of the script (usually the filename).</param>
        /// <param name="host">The host interface for the script.</param>
        public SkinScript(string scriptContent, string scriptName, ISkinScriptHost host)
        {
            this.ScriptName = scriptName;
            this.host = host;

            // Configure MoonSharp for maximum safety
            luaScript = new Script(CoreModules.Preset_SoftSandbox);

            // Register our host API with MoonSharp
            var scriptInterface = new SkinScriptInterface(host);
            UserData.RegisterType<SkinScriptInterface>();
            luaScript.Globals["osu"] = scriptInterface;            try
            {
                // Execute the script
                luaScript.DoString(scriptContent);

                // Extract script description if available
                if (luaScript.Globals.Get("SCRIPT_DESCRIPTION").Type != DataType.Nil)
                    Description = luaScript.Globals.Get("SCRIPT_DESCRIPTION").String;
                else
                    Description = "No description provided";

                // Call onLoad function if it exists
                if (luaScript.Globals.Get("onLoad").Type != DataType.Nil)
                {
                    try
                    {
                        luaScript.Call(luaScript.Globals.Get("onLoad"));
                    }
                    catch (Exception ex)
                    {
                        host.Log($"Error in {ScriptName}.onLoad: {ex.Message}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                host.Log($"Error loading script {ScriptName}: {ex.Message}", LogLevel.Error);
            }
        }        /// <summary>
        /// Creates a new skin script from a file.
        /// </summary>
        /// <param name="filePath">The path to the Lua script file.</param>
        /// <param name="host">The host interface for the script.</param>
        /// <returns>A new instance of the <see cref="SkinScript"/> class.</returns>
        public static SkinScript FromFile(string filePath, ISkinScriptHost host)
        {
            string scriptContent = File.ReadAllText(filePath);
            string scriptName = Path.GetFileName(filePath);
            return new SkinScript(scriptContent, scriptName, host);
        }        /// <summary>
        /// Notifies the script that a component has been loaded.
        /// </summary>
        /// <param name="component">The loaded component.</param>
        public void NotifyComponentLoaded(Drawable component)
        {
            if (!IsEnabled)
                return;

            try
            {
                DynValue result = luaScript.Call(luaScript.Globals.Get("onComponentLoaded"), component);
            }
            catch (Exception ex)
            {
                host.Log($"Error in {ScriptName}.onComponentLoaded: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Notifies the script of a game event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="data">The event data.</param>
        public void NotifyGameEvent(string eventName, object data)
        {
            if (!IsEnabled)
                return;

            try
            {
                DynValue result = luaScript.Call(luaScript.Globals.Get("onGameEvent"), eventName, data);
            }
            catch (Exception ex)
            {
                host.Log($"Error in {ScriptName}.onGameEvent: {ex.Message}", LogLevel.Error);
            }
        }        /// <summary>
        /// Notifies the script of a judgement result.
        /// </summary>
        /// <param name="result">The judgement result.</param>
        public void NotifyJudgement(JudgementResult result)
        {
            if (!IsEnabled)
                return;

            try
            {
                DynValue dynResult = luaScript.Call(luaScript.Globals.Get("onJudgement"), result);
            }
            catch (Exception ex)
            {
                host.Log($"Error in {ScriptName}.onJudgement: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Notifies the script of an input event.
        /// </summary>
        /// <param name="inputEvent">The input event.</param>
        public void NotifyInputEvent(InputEvent inputEvent)
        {
            if (!IsEnabled)
                return;

            try
            {
                DynValue result = luaScript.Call(luaScript.Globals.Get("onInputEvent"), inputEvent);
            }
            catch (Exception ex)
            {
                host.Log($"Error in {ScriptName}.onInputEvent: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Updates the script.
        /// </summary>
        public void Update()
        {
            if (!IsEnabled)
                return;

            try
            {
                DynValue result = luaScript.Call(luaScript.Globals.Get("update"));
            }
            catch (Exception ex)
            {
                host.Log($"Error in {ScriptName}.update: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Releases all resources used by the script.
        /// </summary>
        public void Dispose()
        {
            // Release any resources held by the script
            luaScript.Globals.Clear();
        }
    }
}
