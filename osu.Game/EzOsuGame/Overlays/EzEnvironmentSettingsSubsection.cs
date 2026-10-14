// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 判定/计分环境设置子节基类：跟踪一组设置项，任一偏离默认值时提示成绩上传已被锁定。
    /// </summary>
    public abstract partial class EzEnvironmentSettingsSubsection : SettingsSubsection
    {
        private readonly Bindable<SettingsNote.Data?> submitWarning = new Bindable<SettingsNote.Data?>();
        private readonly List<IBindable> tracked = new List<IBindable>();

        /// <summary>创建本子节顶部共用的上传锁定提示条。</summary>
        protected Container CreateSubmitWarningNote() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = SettingsPanel.CONTENT_PADDING,
            Child = new SettingsNote
            {
                RelativeSizeAxes = Axes.X,
                Current = { BindTarget = submitWarning },
            },
        };

        /// <summary>把设置项纳入成绩上传锁定提示的判定。</summary>
        protected void TrackSubmitWarning<T>(Bindable<T> bindable)
        {
            tracked.Add(bindable);
            bindable.BindValueChanged(_ => Schedule(refreshSubmitWarning));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshSubmitWarning();
        }

        private void refreshSubmitWarning()
        {
            submitWarning.Value = tracked.Any(b => !b.IsDefault)
                ? new SettingsNote.Data(EzSettingsStrings.SCORE_SUBMIT_WARNING, SettingsNote.Type.Warning)
                : null;
        }
    }
}
