// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Realms;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The star rating and perfect-play performance points each analysis system gives one chart.
    /// </summary>
    /// <remarks>
    /// One flat field per system and value, so a reader locates a rating through <see cref="GetStar"/> /
    /// <see cref="GetPerformance"/> instead of naming a column. Stored on the beatmap row.
    /// The official pair is mirrored onto the beatmap's own star and pp columns, which stay the authority for
    /// upstream code; every other system lives only here.
    /// <para>
    /// The property names are persisted column names and stay as they are; only the reader-facing API speaks
    /// <see cref="EzAnalysisSystem"/>.
    /// </para>
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

        /// <summary>Reads one system's star rating, or <c>-1</c> when it has none.</summary>
        public double GetStar(EzAnalysisSystem system) => system switch
        {
            EzAnalysisSystem.Official => OfficialSR,
            EzAnalysisSystem.SSR => SunnySR,
            EzAnalysisSystem.PPP => PppSR,
            EzAnalysisSystem.Mina => MinaSR,
            _ => -1,
        };

        /// <summary>Reads one system's perfect-play performance points, or <c>-1</c> when it has none.</summary>
        public double GetPerformance(EzAnalysisSystem system) => system switch
        {
            EzAnalysisSystem.Official => OfficialPP,
            EzAnalysisSystem.SSR => SunnyPP,
            EzAnalysisSystem.PPP => PppPP,
            EzAnalysisSystem.Mina => MinaPP,
            _ => -1,
        };

        /// <summary>Writes one system's star rating. A system with no slot here is ignored.</summary>
        public void SetStar(EzAnalysisSystem system, double value)
        {
            switch (system)
            {
                case EzAnalysisSystem.Official:
                    OfficialSR = value;
                    break;

                case EzAnalysisSystem.SSR:
                    SunnySR = value;
                    break;

                case EzAnalysisSystem.PPP:
                    PppSR = value;
                    break;

                case EzAnalysisSystem.Mina:
                    MinaSR = value;
                    break;
            }
        }

        /// <summary>Writes one system's perfect-play performance points. A system with no slot here is ignored.</summary>
        public void SetPerformance(EzAnalysisSystem system, double value)
        {
            switch (system)
            {
                case EzAnalysisSystem.Official:
                    OfficialPP = value;
                    break;

                case EzAnalysisSystem.SSR:
                    SunnyPP = value;
                    break;

                case EzAnalysisSystem.PPP:
                    PppPP = value;
                    break;

                case EzAnalysisSystem.Mina:
                    MinaPP = value;
                    break;
            }
        }
    }
}
