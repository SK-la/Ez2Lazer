// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.WarmUp;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// 进图预热登记/等待中心的行为：空实现不阻塞、登记后才等、后到的登记也算、异常与取消都不卡门控。
    /// </summary>
    [TestFixture]
    public class EzGameplayWarmUpServiceTest
    {
        [Test]
        public async Task TestNoRegistrationsDoesNotBlock()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            await service.WarmUpAllAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestRegistrationOutsideSessionIsIgnored()
        {
            var service = new EzGameplayWarmUpService();
            var warmUp = new BlockingWarmUp();

            // 选歌 / 编辑器等树里的组件会在无会话时登记，不能被算进进图门控。
            service.Register(warmUp);
            service.BeginSession();

            await service.WarmUpAllAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(warmUp.Started, Is.False);
        }

        [Test]
        public async Task TestWaitsForRegisteredWarmUp()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            var warmUp = new BlockingWarmUp();
            service.Register(warmUp);

            Task waitTask = service.WarmUpAllAsync(CancellationToken.None);

            await warmUp.WaitUntilStarted();

            Assert.That(waitTask.IsCompleted, Is.False, "预热未完成时不应提前放行");

            warmUp.Complete();

            await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(waitTask.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public async Task TestWaitsForLateRegistration()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            var first = new BlockingWarmUp();
            service.Register(first);

            Task waitTask = service.WarmUpAllAsync(CancellationToken.None);

            await first.WaitUntilStarted();

            // 模拟 Player 加载期间才把自己登记进来的 HUD 组件。
            var late = new BlockingWarmUp();
            service.Register(late);

            first.Complete();
            await late.WaitUntilStarted();

            Assert.That(waitTask.IsCompleted, Is.False, "后到的登记也必须被等到");

            late.Complete();

            await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(waitTask.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public async Task TestFailingWarmUpDoesNotBlockRelease()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            var failing = new BlockingWarmUp { ExceptionToThrow = new InvalidOperationException("坏皮肤") };
            service.Register(failing);

            await service.WarmUpAllAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestUnregisteredWarmUpIsNotWaited()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            var warmUp = new BlockingWarmUp();
            service.Register(warmUp);
            service.Unregister(warmUp);

            await service.WarmUpAllAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(warmUp.Started, Is.False);
        }

        [Test]
        public void TestCancellationReleasesWait()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            // 一个无视取消令牌、永远不结束的实现，也必须能被取消放行（否则只能干等超时）。
            service.Register(new BlockingWarmUp { IgnoreCancellation = true });

            using var cts = new CancellationTokenSource();
            Task waitTask = service.WarmUpAllAsync(cts.Token);

            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public async Task TestEndSessionDropsRegistrations()
        {
            var service = new EzGameplayWarmUpService();
            service.BeginSession();

            service.Register(new BlockingWarmUp());
            service.EndSession();
            service.BeginSession();

            await service.WarmUpAllAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }

        private class BlockingWarmUp : IEzGameplayWarmUp
        {
            public bool IgnoreCancellation { get; init; }

            public Exception? ExceptionToThrow { get; init; }

            public bool Started => started.Task.IsCompleted;

            private readonly TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task WarmUpAsync(CancellationToken cancellationToken)
            {
                started.TrySetResult(true);

                if (ExceptionToThrow != null)
                    throw ExceptionToThrow;

                if (IgnoreCancellation)
                    await completion.Task.ConfigureAwait(false);
                else
                    await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            public Task WaitUntilStarted() => started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            public void Complete() => completion.TrySetResult(true);
        }
    }
}
