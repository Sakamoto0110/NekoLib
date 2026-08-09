#nullable enable
using Microsoft.Data.SqlClient;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    /// <summary>
    /// The versions only this scenario can name.
    /// <para/>
    /// The harness records the host, the runtime and the repository, but it
    /// references no product module and no provider package, so which assembly
    /// is "the provider" and which is "the library under test" is knowledge
    /// that has to live here.
    /// </summary>
    internal static class ScenarioFacts
    {
        /// <summary>The concrete provider, read from the assembly that actually loaded.</summary>
        public static string ProviderVersion =>
            RuntimeFacts.DescribeAssembly("Microsoft.Data.SqlClient", typeof(SqlConnection));

        /// <summary>The module under test.</summary>
        public static string LibraryVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Data", typeof(global::NekoLib.Data.Query.QueryBuilder));

        /// <summary>This scenario's own assembly version.</summary>
        public static string ScenarioVersion =>
            RuntimeFacts.AssemblyVersion(typeof(ScenarioFacts));
    }
}
