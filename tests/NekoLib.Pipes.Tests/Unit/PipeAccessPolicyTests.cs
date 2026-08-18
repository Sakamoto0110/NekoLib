using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

#if NETFRAMEWORK
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
#endif

namespace NekoLib.Pipes.Tests.Unit
{
    public sealed class PipeAccessPolicyTests
    {
        [Fact]
        public void Options_DefaultToPlatformSecurity()
        {
            var options = new PipeServerOptions();

            Assert.Equal(PipeAccessPolicy.PlatformDefault, options.AccessPolicy);
        }

        [Fact]
        public void Server_InvalidAccessPolicy_Throws()
        {
            var options = new PipeServerOptions
            {
                PipeName = PipeTestUtil.UniqueName(),
                AccessPolicy = (PipeAccessPolicy)int.MaxValue
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => new PipeServer(options));
        }

        [Fact]
        public async Task CurrentUserOnly_SameUserRpcRoundTripSucceeds()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                AccessPolicy = PipeAccessPolicy.CurrentUserOnly
            }))
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = name,
                    ConnectTimeout = TimeSpan.FromSeconds(3),
                    RequestTimeout = TimeSpan.FromSeconds(3)
                });
                server.Map(
                    "ping",
                    (request, cancellationToken) =>
                        Task.FromResult(new PipeMessage { Ok = true }));
                server.Start();

                var response = await client.SendAsync("ping");

                Assert.True(response.Ok);
                Assert.Equal(PipeAccessPolicy.CurrentUserOnly, server.AccessPolicy);
            }
        }

        [Fact]
        public async Task CurrentUserOnly_SameUserEventSubscriptionSucceeds()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true,
                AccessPolicy = PipeAccessPolicy.CurrentUserOnly
            }))
            {
                server.Start();

                var received = new ManualResetEventSlim(false);
                using (var client = new PipeEventClient(name))
                {
                    client.OnEvent += message => received.Set();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server.Events.SubscriberCount == 1, 5000),
                        "same-user event client did not connect");

                    for (var i = 0; i < 3 && !received.IsSet; i++)
                    {
                        await server.Events.PublishAsync("ready", new { value = true });
                        received.Wait(1000);
                    }

                    Assert.True(received.IsSet, "same-user event was not delivered");
                }
            }
        }

        [Fact]
        public void CurrentUserOnly_UsesTargetSpecificServerProtection()
        {
#if NETFRAMEWORK
            using (var pipe = PipeServerStreamFactory.Create(
                PipeTestUtil.UniqueName(),
                PipeDirection.InOut,
                PipeAccessPolicy.CurrentUserOnly))
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var user = identity.User;
                var security = pipe.GetAccessControl();
                var rules = security
                    .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
                    .Cast<PipeAccessRule>();

                Assert.True(security.AreAccessRulesProtected);
                Assert.Contains(rules, rule =>
                    rule.AccessControlType == AccessControlType.Allow &&
                    Equals(rule.IdentityReference, user) &&
                    (rule.PipeAccessRights & PipeAccessRights.FullControl) == PipeAccessRights.FullControl);
            }
#else
            var options = PipeServerStreamFactory.ResolveOptions(PipeAccessPolicy.CurrentUserOnly);

            Assert.True((options & PipeOptions.CurrentUserOnly) != 0);
#endif
        }
    }
}
