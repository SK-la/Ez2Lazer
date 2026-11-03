// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// L/R 双转盘状态对，从 <see cref="ScratchAxisDeviceTracker"/> 按设备绑定取样。
    /// </summary>
    public class ScratchAxisPair
    {
        public ScratchAxisProcessor Left { get; } = new ScratchAxisProcessor();
        public ScratchAxisProcessor Right { get; } = new ScratchAxisProcessor();

        public Bindable<string> LeftBinding { get; } = new Bindable<string>(string.Empty);
        public Bindable<string> RightBinding { get; } = new Bindable<string>(string.Empty);

        public void BindTuning(Bindable<double> deadzone, Bindable<int> stopThresholdMs)
        {
            Left.Deadzone.BindTo(deadzone);
            Right.Deadzone.BindTo(deadzone);
            Left.StopThresholdMs.BindTo(stopThresholdMs);
            Right.StopThresholdMs.BindTo(stopThresholdMs);
        }

        public void UpdateFrom(ScratchAxisDeviceTracker tracker, double currentTime)
        {
            updateOne(Left, ScratchAxisBinding.Parse(LeftBinding.Value), tracker, currentTime);
            updateOne(Right, ScratchAxisBinding.Parse(RightBinding.Value), tracker, currentTime);
        }

        public void Reset()
        {
            Left.Reset();
            Right.Reset();
        }

        private static void updateOne(ScratchAxisProcessor processor, ScratchAxisBinding binding, ScratchAxisDeviceTracker tracker, double currentTime)
        {
            if (tracker.TryGetValue(binding, out float value))
                processor.Update(value, currentTime);
            else
                processor.UpdateMissing(currentTime);
        }
    }
}
