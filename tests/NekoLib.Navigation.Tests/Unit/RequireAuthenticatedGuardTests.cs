using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Guards;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    // NAV-002: the guard denied without a reason, leaving GuardDeniedEvent, Logging
    // and Inspection with nothing to explain the refusal, while the role and
    // permission guards next to it always report one.
    public class RequireAuthenticatedGuardTests
    {
        private const string ExpectedReason = "Authentication required.";

        [Fact]
        public async Task EvaluateAsync_AuthenticatedUser_Allows()
        {
            var guard = new RequireAuthenticatedGuard();

            var result = await guard.EvaluateAsync(
                new GuardContext(typeof(StubAuthenticated), new FakeUser(true)));

            Assert.True(result.Allowed);
            Assert.Null(result.RedirectPage);
        }

        [Fact]
        public async Task EvaluateAsync_UnauthenticatedUser_DeniesWithStableReason()
        {
            var guard = new RequireAuthenticatedGuard();

            var result = await guard.EvaluateAsync(
                new GuardContext(typeof(StubAuthenticated), new FakeUser(false)));

            Assert.False(result.Allowed);
            Assert.Null(result.RedirectPage);
            Assert.Equal(ExpectedReason, result.Reason);
        }

        [Fact]
        public void GuardContext_WithoutUserContext_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new GuardContext(typeof(StubAuthenticated), null));
        }

        [Fact]
        public async Task NavigateAsync_UnauthenticatedGuardedPage_ReportsReasonToGuardDenied()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated));
            var denied = new List<GuardDeniedEvent>();
            fixture.Context.Events.GuardDenied += denied.Add;

            await fixture.Runtime.NavigateAsync(
                typeof(StubAuthenticated),
                NavigationArgs.Default());

            var guardDenied = Assert.Single(denied);
            Assert.Equal(ExpectedReason, guardDenied.Reason);
            Assert.Equal(typeof(StubAuthenticated), guardDenied.TargetPage);
            Assert.Null(guardDenied.RedirectPage);
        }

        [Fact]
        public async Task NavigateAsync_AuthenticatedGuardedPage_IsNotDenied()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated));
            var denied = new List<GuardDeniedEvent>();
            fixture.Context.Events.GuardDenied += denied.Add;
            fixture.Context.Session.SignIn("operator");

            await fixture.Runtime.NavigateAsync(
                typeof(StubAuthenticated),
                NavigationArgs.Default());

            Assert.Empty(denied);
            Assert.IsType<StubAuthenticated>(fixture.Runtime.Current);
        }

        [Fact]
        public void LoggingSink_ForwardsTheDenialReason()
        {
            var logger = new CapturingLogger();
            var sink = new LoggingNavigationSink(logger);

            sink.OnGuardDenied(new GuardDeniedEvent(
                fromPage: null,
                targetPage: typeof(StubAuthenticated),
                redirectPage: null,
                reason: ExpectedReason));

            var message = Assert.Single(logger.Messages);
            Assert.Contains("reason=" + ExpectedReason, message);
        }

        private sealed class CapturingLogger : NekoLib.Core.Logging.ILogger
        {
            public List<string> Messages { get; } = new List<string>();

            public void Log(
                NekoLib.Core.Logging.LogLevel level,
                string message,
                System.Exception exception = null,
                string category = null)
                => Messages.Add(message);
        }

        private sealed class FakeUser : IUserContext
        {
            private static readonly string[] None = new string[0];

            public FakeUser(bool isAuthenticated)
                => IsAuthenticated = isAuthenticated;

            public bool IsAuthenticated { get; }
            public IReadOnlyCollection<string> Roles => None;
            public IReadOnlyCollection<string> Permissions => None;
        }
    }
}
