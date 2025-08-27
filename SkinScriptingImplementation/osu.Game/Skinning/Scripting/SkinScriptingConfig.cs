using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Skinning.Scripting
{
    public class SkinScriptingConfig : IniConfigManager<SkinScriptingSettings>
    {
        public SkinScriptingConfig(Storage storage) : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            Set(SkinScriptingSettings.ScriptingEnabled, true);
            Set(SkinScriptingSettings.AllowedScripts, new List<string>());
            Set(SkinScriptingSettings.BlockedScripts, new List<string>());
        }
    }

    public enum SkinScriptingSettings
    {
        // 全局启用/禁用脚本功能
        ScriptingEnabled,

        // 允许的脚本列表
        AllowedScripts,

        // 禁止的脚本列表
        BlockedScripts
    }
}
