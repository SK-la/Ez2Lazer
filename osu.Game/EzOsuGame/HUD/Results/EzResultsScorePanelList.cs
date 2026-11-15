// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Layout;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.HUD.Results
{
    public partial class EzResultsScorePanelList : ScorePanelList, ISerialisableDrawable, IEzSceneHudComponent
    {
        public bool UsesFixedAnchor { get; set; }

        public GlobalSkinnableContainers LayoutTarget => GlobalSkinnableContainers.Results;
    }
}
