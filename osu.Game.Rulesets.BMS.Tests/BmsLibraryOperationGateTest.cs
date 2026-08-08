// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsLibraryOperationGateTest
    {
        [Test]
        public async Task TestBeginCancelsPreviousAndDisposesHandle()
        {
            var gate = new BmsLibraryOperationGate();

            using BmsLibraryOperationGate.OperationHandle first = gate.Begin();
            Assert.That(gate.IsBusy, Is.True);
            long firstId = first.Id;
            CancellationToken firstToken = first.Token;

            using BmsLibraryOperationGate.OperationHandle second = gate.Begin();
            Assert.That(firstToken.IsCancellationRequested, Is.True);
            Assert.That(first.Token.IsCancellationRequested, Is.True);
            Assert.That(second.Token.IsCancellationRequested, Is.False);
            Assert.That(second.Id, Is.Not.EqualTo(firstId));
            Assert.That(gate.CurrentOperationId, Is.EqualTo(second.Id));

            second.Dispose();
            Assert.That(gate.IsBusy, Is.False);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public void TestCancelCurrentSignalsActiveToken()
        {
            var gate = new BmsLibraryOperationGate();
            using var handle = gate.Begin();
            gate.CancelCurrent();
            Assert.That(handle.Token.IsCancellationRequested, Is.True);
        }
    }
}
