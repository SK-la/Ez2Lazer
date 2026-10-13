// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing.Drawables.Steps;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 与 framework <see cref="UntilStepButton"/> 相同，但超时可配置（全谱 Visual 默认 10s 不够）。
    /// </summary>
    public partial class LongUntilStepButton : StepButton
    {
        public required StackTrace CallStack { get; init; }
        public required Func<bool> Assertion { get; init; }
        public Func<string>? GetFailureMessage { get; init; }
        public new Action? Action { get; set; }
        public int TimeoutMilliseconds { get; init; } = 600_000;

        public override int RequiredRepetitions => success ? 0 : int.MaxValue;

        private readonly string text = string.Empty;
        private bool success;
        private int invocations;
        private Stopwatch? elapsedTime;

        public LongUntilStepButton()
        {
            updateText();
            LightColour = Color4.Sienna;
            base.Action = checkAssert;
        }

        public new string Text
        {
            get => text;
            init => base.Text = text = value;
        }

        private void checkAssert()
        {
            invocations++;
            elapsedTime ??= Stopwatch.StartNew();

            updateText();

            if (Assertion())
            {
                elapsedTime = null;
                success = true;
                Success();
            }
            else if (!Debugger.IsAttached && elapsedTime.ElapsedMilliseconds >= TimeoutMilliseconds)
            {
                var builder = new StringBuilder();
                builder.Append($"\"{Text}\" timed out after {TimeoutMilliseconds}ms");

                if (GetFailureMessage != null)
                    builder.Append($": {GetFailureMessage()}");

                throw ExceptionDispatchInfo.SetRemoteStackTrace(new AssertionException(builder.ToString()), CallStack.ToString());
            }

            Action?.Invoke();
        }

        public override void Reset()
        {
            base.Reset();
            invocations = 0;
            elapsedTime = null;
            success = false;
        }

        protected override void Success()
        {
            base.Success();
            Light.FadeColour(Color4.YellowGreen);
        }

        protected override void Failure()
        {
            base.Failure();
            Light.FadeColour(Color4.Red);
        }

        private void updateText() => base.Text = $@"{Text} ({invocations} tries)";

        public override string ToString() => "Until: " + base.ToString();
    }
}
