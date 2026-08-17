using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Mapping;
using NekoLib.Data.Query;
#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class PublicApiContractTests
    {
        [Fact]
        public void DatabaseGateway_PublicBoundary_UsesCanonicalNamespaceWithoutLegacyTypes()
        {
            Assembly assembly = typeof(DatabaseGateway).Assembly;

            Assert.Equal("NekoLib.Data.Gateway", typeof(DatabaseGateway).Namespace);
            Assert.Null(assembly.GetType("NekoLib.Data.Internal.Gateway.DatabaseGateway"));
            Assert.Null(assembly.GetType("NekoLib.Data.Gateway.IUniversalQueryGateway"));
            Type readerExtensions = assembly.GetType(
                "NekoLib.Data.Gateway.DbDataReaderExtensions",
                throwOnError: true);
            Assert.False(readerExtensions.IsPublic);
        }

        [Fact]
        public void DatabaseGateway_CapabilityInterfaces_MapOnlyToPublicMethods()
        {
            Type gatewayType = typeof(DatabaseGateway);
            Type[] capabilities = typeof(IDatabaseGateway)
                .GetInterfaces()
                .Concat(new[] { typeof(IDatabaseGateway) })
                .Distinct()
                .ToArray();

            foreach (Type capability in capabilities)
            {
                InterfaceMapping mapping = gatewayType.GetInterfaceMap(capability);
                Assert.All(
                    mapping.TargetMethods,
                    method => Assert.True(
                        method.IsPublic,
                        capability.FullName + "." + method.Name +
                        " maps to non-public " + method.Name + "."));
            }
        }

        [Fact]
        public void IDmlGateway_Delete_HasRawAndBuilderSessionSymmetry()
        {
            MethodInfo[] overloads = typeof(IDmlGateway)
                .GetMethods()
                .Where(method => method.Name == nameof(IDmlGateway.Delete))
                .ToArray();

            Assert.Equal(4, overloads.Length);
            Assert.Equal(2, overloads.Count(method =>
                method.GetParameters()[0].ParameterType == typeof(string)));
            Assert.Equal(2, overloads.Count(method =>
                method.GetParameters()[0].ParameterType == typeof(QueryBuilder)));
            Assert.Equal(2, overloads.Count(method =>
                method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(DbSession))));
        }

        [Fact]
        public void ConcreteExtensionBoundaries_NonExtensibleTypes_AreSealed()
        {
            Assert.True(typeof(DatabaseGateway).IsSealed);
            Assert.True(typeof(QueryExecutionContext).IsSealed);
            Assert.True(typeof(QueryBuilder).IsSealed);
            Assert.True(typeof(RecordItem).IsSealed);
            Assert.True(typeof(DbConnectionAbstractFactory<FakeNonQueryConnection>).IsSealed);
        }

        [Fact]
        public void DatabaseGateway_NullContext_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => new DatabaseGateway(null));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void StreamingCapability_TargetFramework_MatchesImplementedSurface()
        {
            Type streaming = typeof(DatabaseGateway).Assembly.GetType(
                "NekoLib.Data.Gateway.IDqlStreamingGateway");

#if NET9_0_OR_GREATER
            Assert.NotNull(streaming);
            Assert.Contains(streaming, typeof(IDatabaseGateway).GetInterfaces());
#else
            Assert.Null(streaming);
            Assert.DoesNotContain(
                typeof(DatabaseGateway).Assembly.GetReferencedAssemblies(),
                reference => reference.Name == "Microsoft.Bcl.AsyncInterfaces");
#endif
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void DtoReflectionEntryPoints_Net9_DeclareRequiredMembers()
        {
            foreach (MethodInfo method in typeof(IDtoQueryGateway).GetMethods())
                AssertDtoRequirement(method.GetGenericArguments().Single(), method.ToString());

            Type streaming = typeof(DatabaseGateway).Assembly.GetType(
                "NekoLib.Data.Gateway.IDqlStreamingGateway",
                throwOnError: true);
            foreach (MethodInfo method in streaming.GetMethods().Where(candidate => candidate.IsGenericMethod))
                AssertDtoRequirement(method.GetGenericArguments().Single(), method.ToString());

            foreach (MethodInfo method in typeof(DataMapper).GetMethods().Where(candidate => candidate.Name == "Map"))
            {
                if (method.IsGenericMethod)
                {
                    AssertDtoRequirement(method.GetGenericArguments().Single(), method.ToString());
                    continue;
                }

                ParameterInfo targetType = Assert.Single(
                    method.GetParameters().Where(parameter => parameter.Name == "targetType"));
                AssertDtoRequirement(targetType, method.ToString());
            }
        }

        private static void AssertDtoRequirement(ICustomAttributeProvider target, string source)
        {
            DynamicallyAccessedMembersAttribute attribute = Assert.Single(
                target.GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), false)
                    .Cast<DynamicallyAccessedMembersAttribute>());
            DynamicallyAccessedMemberTypes expected =
                DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
                DynamicallyAccessedMemberTypes.PublicProperties;
            Assert.Equal(expected, attribute.MemberTypes);
        }
#endif
    }
}
