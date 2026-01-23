using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Audio;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 提供预览覆写参数给选曲界面使用（歌曲预览）。
    /// </summary>
    public interface IApplyToLoopPlay
    {
        OverrideSettings GetOverrides(IWorkingBeatmap beatmap);
    }
}
