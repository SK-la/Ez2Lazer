namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public interface IEzOscillator
    {
        double NextSigned();
        double Next();
        void Reset(long start = 0);
    }
}
