// Provide a simple ordering mechanism for post-conversion mod application.
namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Optional interface to indicate the apply order when executing
    /// <see cref="IApplicableAfterBeatmapConversion"/> implementations.
    /// Lower values are applied earlier. Default for mods not implementing
    /// this interface is treated as 0.
    /// </summary>
    public interface IHasApplyOrder
    {
        int ApplyOrder { get; }
    }
}
