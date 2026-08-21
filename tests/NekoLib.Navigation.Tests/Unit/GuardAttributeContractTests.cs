using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class GuardAttributeContractTests
    {
        public static IEnumerable<object[]> RedirectingAttributes()
        {
            yield return new object[] { new RequireAuthenticatedAttribute() };
            yield return new object[] { new RequireRoleAttribute("admin") };
            yield return new object[] { new RequirePermissionAttribute("orders.read") };
            yield return new object[] { new RequireAllPermissionsAttribute("orders.read") };
            yield return new object[] { new RequireAnyPermissionsAttribute("orders.read") };
        }

        [Theory]
        [MemberData(nameof(RedirectingAttributes))]
        public async Task CreateGuard_DeniedWithRedirectTo_Redirects(GuardAttribute attribute)
        {
            attribute.RedirectTo = typeof(StubA);

            var result = await attribute.CreateGuard().EvaluateAsync(
                new GuardContext(typeof(StubB), new EmptyUserContext()));

            Assert.False(result.Allowed);
            Assert.Equal(typeof(StubA), result.RedirectPage);
            Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        }

        [Fact]
        public void RequirePermission_OneArgumentConstructor_DoesNotRequireRedirect()
        {
            var attribute = new RequirePermissionAttribute("orders.read");

            Assert.Null(attribute.RedirectTo);
        }

        [Fact]
        public void GuardInputs_BlankNames_Throw()
        {
            Assert.Throws<ArgumentException>(() => new RequireRoleAttribute(" "));
            Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute(" "));
            Assert.Throws<ArgumentException>(() => new RequireAllPermissionsAttribute("valid", " "));
            Assert.Throws<ArgumentException>(() => new RequireAnyPermissionsAttribute("valid", " "));
        }

        [Fact]
        public void Redirect_InvalidPageType_Throws()
        {
            Assert.Throws<ArgumentException>(() => GuardResult.Redirect(typeof(string)));
        }

        private sealed class EmptyUserContext : IUserContext
        {
            private static readonly string[] Empty = new string[0];

            public bool IsAuthenticated => false;
            public IReadOnlyCollection<string> Roles => Empty;
            public IReadOnlyCollection<string> Permissions => Empty;
        }
    }
}
