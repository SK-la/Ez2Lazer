// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Realms;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The star rating (SR) and perfect-play performance points (PP) each analysis system gives one chart.
    /// </summary>
    /// <remarks>
    /// One flat field per system and value, so a reader locates a rating through
    /// <see cref="Get"/> instead of naming a column. Stored on the beatmap row.
    /// The official pair is mirrored onto the beatmap's own star and pp columns, which stay the authority for
    /// upstream code; every other system lives only here.
    /// </remarks>
    [MapTo("EzRatingData")]
    public class EzRatingData : EmbeddedObject
    {
        public double OfficialSR { get; set; } = -1;

        public double OfficialPP { get; set; } = -1;

        public double SunnySR { get; set; } = -1;

        public double SunnyPP { get; set; } = -1;

        public double PppSR { get; set; } = -1;

        public double PppPP { get; set; } = -1;

        public double MinaSR { get; set; } = -1;

        public double MinaPP { get; set; } = -1;

        /// <summary>An independent copy, so a cloned beatmap does not share this record with its source.</summary>
        public EzRatingData Copy() => new EzRatingData
        {
            OfficialSR = OfficialSR,
            OfficialPP = OfficialPP,
            SunnySR = SunnySR,
            SunnyPP = SunnyPP,
            PppSR = PppSR,
            PppPP = PppPP,
            MinaSR = MinaSR,
            MinaPP = MinaPP,
        };

        /// <summary>Reads one rating, or <c>-1</c> when this system has no such value.</summary>
        public double Get(EzRatingSystem system, EzRatingField field) => (system, field) switch
        {
            (EzRatingSystem.Official, EzRatingField.SR) => OfficialSR,
            (EzRatingSystem.Official, EzRatingField.PP) => OfficialPP,
            (EzRatingSystem.Sunny, EzRatingField.SR) => SunnySR,
            (EzRatingSystem.Sunny, EzRatingField.PP) => SunnyPP,
            (EzRatingSystem.Ppp, EzRatingField.SR) => PppSR,
            (EzRatingSystem.Ppp, EzRatingField.PP) => PppPP,
            (EzRatingSystem.Mina, EzRatingField.SR) => MinaSR,
            (EzRatingSystem.Mina, EzRatingField.PP) => MinaPP,
            _ => -1,
        };

        /// <summary>Writes one rating. A system/field pair this type does not carry is ignored.</summary>
        public void Set(EzRatingSystem system, EzRatingField field, double value)
        {
            switch (system, field)
            {
                case (EzRatingSystem.Official, EzRatingField.SR):
                    OfficialSR = value;
                    break;

                case (EzRatingSystem.Official, EzRatingField.PP):
                    OfficialPP = value;
                    break;

                case (EzRatingSystem.Sunny, EzRatingField.SR):
                    SunnySR = value;
                    break;

                case (EzRatingSystem.Sunny, EzRatingField.PP):
                    SunnyPP = value;
                    break;

                case (EzRatingSystem.Ppp, EzRatingField.SR):
                    PppSR = value;
                    break;

                case (EzRatingSystem.Ppp, EzRatingField.PP):
                    PppPP = value;
                    break;

                case (EzRatingSystem.Mina, EzRatingField.SR):
                    MinaSR = value;
                    break;

                case (EzRatingSystem.Mina, EzRatingField.PP):
                    MinaPP = value;
                    break;
            }
        }
    }

    /// <summary>Analysis systems a chart rating is stored for.</summary>
    public enum EzRatingSystem
    {
        None,

        Official,

        Sunny,

        Ppp,

        Mina,
    }

    /// <summary>Which half of a system's rating is addressed.</summary>
    public enum EzRatingField
    {
        None,

        /// <summary>Star rating, on the same scale the panel prints beside the official star.</summary>
        SR,

        /// <summary>Perfect-play performance points.</summary>
        PP,
    }
}
