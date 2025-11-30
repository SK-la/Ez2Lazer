using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public partial class EzAnalysisOptionsPopover : OsuPopover
    {
        // private readonly BeatmapInfo beatmapInfo;

        public EzAnalysisOptionsPopover(BeatmapInfo beatmapInfo)
            : base(false)
        {
            // this.beatmapInfo = beatmapInfo;

            Body.CornerRadius = 4;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new[]
            {
                new OsuMenu(Direction.Vertical, true)
                {
                    Items = items,
                    MaxHeight = 375,
                },
            };
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);
            Hide();
        }

        private OsuMenuItem[] items => new[]
        {
            new OsuMenuItem("列队选项1", MenuItemType.Standard, () => OnOptionSelected("列队选项1")),
            new OsuMenuItem("列队选项2", MenuItemType.Standard, () => OnOptionSelected("列队选项2")),
            new OsuMenuItem("列队选项3", MenuItemType.Standard, () => OnOptionSelected("列队选项3"))
        };

        private void OnOptionSelected(string option)
        {
            // 处理选项选择逻辑
            Console.WriteLine($"选中: {option}");
        }
    }
}
