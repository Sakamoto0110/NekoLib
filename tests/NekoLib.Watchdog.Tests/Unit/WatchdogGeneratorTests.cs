using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NekoLib.Watchdog.Generators;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogGeneratorTests
    {
        private const string GeneratedEntry = "NekoLib.Watchdog.Generated.ApplicationEntryPoint";
        private const string Infrastructure = @"
namespace NekoLib.Watchdog {
 public enum WatchdogHostingMode { SameExecutable, Sidecar }
 public class WatchdogAutoBootstrapOptions { public WatchdogHostingMode HostingMode {get;set;} }
 public static class WatchdogAutoBootstrap {
  public static bool Prepared, Completed, Ran;
  public static void Prepare(WatchdogAutoBootstrapOptions o) { Prepared=true; }
  public static void Activate() { }
  public static void CompleteApplication() { Completed=true; }
  public static bool TryRunSupervisor(string[] args, out int code) { code=23; return args.Length>0 && args[0]==""supervisor""; }
 }
}
namespace System.Windows.Forms { public static class Application { public static event System.EventHandler Idle; } }
namespace System.Windows.Threading {
 public enum DispatcherPriority { Loaded }
 public class Dispatcher { public static Dispatcher CurrentDispatcher = new Dispatcher(); public void BeginInvoke(DispatcherPriority p, System.Action callback) {} }
}
";

        [Theory]
        [InlineData("private static int Main(string[] args) { return args[0]==\"a b\" ? 17 : 99; }", "WinForms")]
        [InlineData("private static int Main() { return 17; }", "Wpf")]
        [InlineData("private static async System.Threading.Tasks.Task<int> Main(string[] args) { await System.Threading.Tasks.Task.Yield(); return 17; }", "WinForms")]
        [InlineData("private static void Main() { }", "Wpf")]
        public void GeneratedEntry_ExecutesOriginalSignatureAndPreservesReturn(string main, string framework)
        {
            var result = Generate("class Original { " + main + " }" + Infrastructure, framework);
            Assert.Empty(result.Item2.Where(d => d.Severity == DiagnosticSeverity.Error));
            using (var stream = new MemoryStream())
            {
                var emit = result.Item1.Emit(stream);
                Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
                var assembly = Assembly.Load(stream.ToArray());
                var entry = assembly.EntryPoint;
                Assert.Equal(GeneratedEntry, entry.DeclaringType.FullName);
                var exit = (int)entry.Invoke(null, new object[] { new[] { "a b" } });
                Assert.Equal(main.Contains("void Main") ? 0 : 17, exit);
                var bootstrap = assembly.GetType("NekoLib.Watchdog.WatchdogAutoBootstrap");
                Assert.True((bool)bootstrap.GetField("Prepared").GetValue(null));
                Assert.True((bool)bootstrap.GetField("Completed").GetValue(null));
            }
        }

        [Fact]
        public void VoidMain_PreservesApplicationEnvironmentExitCode()
        {
            var previous = Environment.ExitCode;
            try
            {
                var result = Generate("class Original { static void Main() { System.Environment.ExitCode = 19; } }" + Infrastructure);
                using (var stream = new MemoryStream())
                {
                    Assert.True(result.Item1.Emit(stream).Success);
                    Assert.Equal(19, (int)Assembly.Load(stream.ToArray()).EntryPoint.Invoke(null, new object[] { new string[0] }));
                }
            }
            finally { Environment.ExitCode = previous; }
        }

        [Fact]
        public void SupervisorBranch_DoesNotExecuteApplicationEntry()
        {
            var result = Generate("class Original { private static int Main() { throw new System.Exception(\"must not run\"); } }" + Infrastructure);
            using (var stream = new MemoryStream())
            {
                Assert.True(result.Item1.Emit(stream).Success);
                var assembly = Assembly.Load(stream.ToArray());
                Assert.Equal(23, (int)assembly.EntryPoint.Invoke(null, new object[] { new[] { "supervisor" } }));
                var bootstrap = assembly.GetType("NekoLib.Watchdog.WatchdogAutoBootstrap");
                Assert.False((bool)bootstrap.GetField("Prepared").GetValue(null));
            }
        }

        [Fact]
        public void EscapingOriginalException_IsUnwrappedAndDoesNotDetach()
        {
            var result = Generate("class Original { static void Main() { throw new System.InvalidOperationException(\"original failure\"); } }" + Infrastructure);
            using (var stream = new MemoryStream())
            {
                Assert.True(result.Item1.Emit(stream).Success);
                var assembly = Assembly.Load(stream.ToArray());
                var outer = Assert.Throws<TargetInvocationException>(() => assembly.EntryPoint.Invoke(null, new object[] { new string[0] }));
                Assert.IsType<InvalidOperationException>(outer.InnerException);
                Assert.Equal("original failure", outer.InnerException.Message);
                Assert.False((bool)assembly.GetType("NekoLib.Watchdog.WatchdogAutoBootstrap").GetField("Completed").GetValue(null));
            }
        }

        [Fact]
        public void TopLevelStatements_HaveSpecificDiagnostic()
        {
            var result = Generate("System.Console.WriteLine(\"top level\");" + Infrastructure);
            Assert.Contains(result.Item2, d => d.Id == "NEKOWDG002");
        }

        [Fact]
        public void DisabledIntegration_DoesNotChangeEntryPoint()
        {
            var result = Generate("class Original { static void Main() {} }", enabled: false);
            Assert.Empty(result.Item2);
            Assert.Equal("Original", result.Item1.GetEntryPoint(default).ContainingType.Name);
        }

        [Theory]
        [InlineData("Other", "SameExecutable")]
        [InlineData("WinForms", "Other")]
        public void InvalidBuildConfiguration_IsRejectedClearly(string framework, string mode)
        {
            var result = Generate("class Original { static void Main() {} }" + Infrastructure, framework, mode);
            Assert.Contains(result.Item2, d => d.Id == "NEKOWDG001");
        }

        private static Tuple<Compilation, ImmutableArray<Diagnostic>> Generate(string source, string framework = "WinForms", string mode = "SameExecutable", bool enabled = true)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
                ?? new[] { typeof(object).Assembly.Location, typeof(Enumerable).Assembly.Location, typeof(System.Threading.Tasks.Task).Assembly.Location };
            var compilation = CSharpCompilation.Create("WatchdogEntry_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) }, references.Distinct().Select(path => MetadataReference.CreateFromFile(path)),
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, mainTypeName: enabled ? GeneratedEntry : null));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new AutoBootstrapGenerator().AsSourceGenerator() }, optionsProvider: new Options(framework, mode, enabled));
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);
            return Tuple.Create(updated, diagnostics);
        }

        private sealed class Options : AnalyzerConfigOptionsProvider
        {
            private readonly Values _values;
            public Options(string framework, string mode, bool enabled) { _values = new Values(framework, mode, enabled); }
            public override AnalyzerConfigOptions GlobalOptions => _values;
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _values;
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _values;
        }
        private sealed class Values : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _values;
            public Values(string framework, string mode, bool enabled)
            {
                _values = new Dictionary<string, string> {
                    ["build_property.NekoLibWatchdogAutoBootstrap"] = enabled ? "true" : "false",
                    ["build_property.NekoLibWatchdogUiFramework"] = framework,
                    ["build_property.NekoLibWatchdogHostingMode"] = mode,
                    ["build_property.NekoLibWatchdogOriginalStartupObject"] = "Original"
                };
            }
            public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
        }
    }
}
