namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.LAsMods
{
    public interface IEzOscillator
    {
        double NextSigned();
        double Next();
        void Reset(long start = 0);
    }
}
