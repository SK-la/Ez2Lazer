using System;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class DuplicateVirtualTrack : CompositeDrawable
    {
        private readonly Track originalTrack;
        private readonly ManiaModDuplicate mod;
        private ScheduledDelegate? updateDelegate;

        public DuplicateVirtualTrack(Track track, ManiaModDuplicate mod)
        {
            this.originalTrack = track;
            this.mod = mod;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Start();
        }

        public void Start()
        {
            updateDelegate = Scheduler.AddDelayed(UpdateAudio, 16, true);
        }

        public void Stop()
        {
            updateDelegate?.Cancel();
            if (originalTrack.IsRunning)
                originalTrack.Stop();
        }

        private void UpdateAudio()
        {
            double gameTime = Clock.CurrentTime;
            double audioTime = MapTime(gameTime);
            if (audioTime < 0)
            {
                if (originalTrack.IsRunning)
                    originalTrack.Stop();
            }
            else
            {
                if (!originalTrack.IsRunning)
                    originalTrack.Start();
                originalTrack.Seek(audioTime);
            }
        }

        private double MapTime(double gameTime)
        {
            double? cutTimeStart = mod.CutTimeStart.Value * (mod.Millisecond.Value ? 1 : 1000);
            double? cutTimeEnd = mod.CutTimeEnd.Value * (mod.Millisecond.Value ? 1 : 1000);
            if (cutTimeStart == null || cutTimeEnd == null)
            {
                cutTimeStart = 0;
                cutTimeEnd = originalTrack.Length;
            }
            double length = (double)(cutTimeEnd - cutTimeStart);
            double breakTime = mod.BreakTime.Value * 1000;
            int times = mod.Time.Value;
            double segmentLength = length + breakTime;
            int segment = (int)(gameTime / segmentLength);
            if (segment >= times)
                return -1;
            double offset = gameTime % segmentLength;
            if (offset < length)
            {
                return (double)cutTimeStart + offset;
            }
            else
            {
                return -1;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            Stop();
            base.Dispose(isDisposing);
        }
    }
}
