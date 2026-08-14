// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Moq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;

namespace osu.Game.Tests.EzOsuGame.Online
{
    [TestFixture]
    public class LocalOnlyRequestHandlerTest
    {
        [Test]
        public void TestFriendsRequestCompletesSuccessfully()
        {
            var request = attach(new GetFriendsRequest());

            Assert.That(new LocalOnlyRequestHandler().Handle(request), Is.True);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
        }

        [Test]
        public void TestChatAckCompletesSuccessfully()
        {
            var request = attach(new ChatAckRequest());

            Assert.That(new LocalOnlyRequestHandler().Handle(request), Is.True);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
        }

        [Test]
        public void TestUnhandledLookupFailsWithoutUsingApiRequestFail()
        {
            var request = attach(new GetBeatmapSetRequest(1));

            Assert.That(new LocalOnlyRequestHandler().Handle(request), Is.True);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
        }

        [Test]
        public void TestListTagsFailsAsExpectedLocalUnavailable()
        {
            var request = attach(new ListTagsRequest());
            Exception? failure = null;
            request.Failure += e => failure = e;

            Assert.That(new LocalOnlyRequestHandler().Handle(request), Is.True);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
            Assert.That(failure, Is.InstanceOf<LocalOnlyUnavailableException>());
        }

        private static T attach<T>(T request) where T : APIRequest
        {
            var api = new Mock<IAPIProvider>();
            api.Setup(a => a.Schedule(It.IsAny<Action>())).Callback<Action>(a => a());
            request.AttachAPI(api.Object);
            return request;
        }
    }
}
