using System;
using NekoLib.Navigation.Runtime.Session;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class NavigationSessionTests
    {
        [Fact]
        public void SignIn_ParamsRoles_CopiesCallerArray()
        {
            var roles = new[] { "operator" };
            var session = new NavigationSession();

            session.SignIn(roles);
            roles[0] = "mutated";

            Assert.Equal("operator", Assert.Single(session.Roles));
        }

        [Fact]
        public void SessionChanges_WhenFirstSubscriberThrows_NotifiesLaterSubscriber()
        {
            var session = new NavigationSession();
            var notifications = 0;
            session.Changed += () => throw new InvalidOperationException();
            session.Changed += () => notifications++;

            session.SignIn("operator");
            session.SignOut();

            Assert.Equal(2, notifications);
            Assert.False(session.IsAuthenticated);
            Assert.Empty(session.Roles);
            Assert.Empty(session.Permissions);
        }
    }
}
