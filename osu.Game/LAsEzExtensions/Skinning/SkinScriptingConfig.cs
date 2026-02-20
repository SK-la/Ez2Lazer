using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.LAsEzExtensions.Skinning
{
    public class SkinScriptingConfig : IniConfigManager<SkinScriptingSetting>
    {
        protected override string Filename => "skin-scripting.ini";

        public SkinScriptingConfig(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(SkinScriptingSetting.ScriptingEnabled, true);
            SetDefault(SkinScriptingSetting.LastImportDirectory, string.Empty);
        }
    }

    public enum SkinScriptingSetting
    {
        ScriptingEnabled,
        LastImportDirectory,
    }
}
