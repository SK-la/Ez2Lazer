// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Camera;

namespace osu.Game.Tests.EzOsuGame.Camera
{
    [TestFixture]
    public class EzCameraHostTest
    {
        [Test]
        public void DisabledDoesNotCreateBackend()
        {
            int created = 0;
            var enabled = new Bindable<bool>(false);
            var host = new EzCameraHost(enabled, () =>
            {
                created++;
                return new FakeBackend();
            });

            Assert.That(host.TryGetBackend(), Is.Null);
            Assert.That(created, Is.EqualTo(0));
            Assert.That(host.IsPlatformSupported, Is.True);

            host.Dispose();
            Assert.That(created, Is.EqualTo(0));
        }

        [Test]
        public void EnablingCreatesBackendOnceUntilDisabled()
        {
            int created = 0;
            FakeBackend? latest = null;
            var enabled = new Bindable<bool>(false);
            var host = new EzCameraHost(enabled, () =>
            {
                created++;
                return latest = new FakeBackend();
            });

            enabled.Value = true;

            IEzCameraBackend? first = host.TryGetBackend();
            IEzCameraBackend? second = host.TryGetBackend();

            Assert.That(first, Is.SameAs(second));
            Assert.That(created, Is.EqualTo(1));
            Assert.That(latest!.Disposed, Is.False);

            enabled.Value = false;

            Assert.That(latest.Disposed, Is.True);
            Assert.That(host.TryGetBackend(), Is.Null);
            Assert.That(created, Is.EqualTo(1));

            enabled.Value = true;
            Assert.That(host.TryGetBackend(), Is.Not.Null);
            Assert.That(created, Is.EqualTo(2));

            host.Dispose();
        }

        [Test]
        public void MissingPlatformNeverCallsFactory()
        {
            var host = new EzCameraHost(new Bindable<bool>(true));

            Assert.That(host.IsPlatformSupported, Is.False);
            Assert.That(host.TryGetBackend(), Is.Null);
        }

        private sealed class FakeBackend : IEzCameraBackend
        {
            public bool Disposed { get; private set; }

            public Task<IReadOnlyList<EzCameraDevice>> ListDevicesAsync() => Task.FromResult<IReadOnlyList<EzCameraDevice>>(Array.Empty<EzCameraDevice>());

            public IEzCameraSession Open(EzCameraOpenRequest request) => throw new NotSupportedException();

            public void Dispose() => Disposed = true;
        }
    }
}
