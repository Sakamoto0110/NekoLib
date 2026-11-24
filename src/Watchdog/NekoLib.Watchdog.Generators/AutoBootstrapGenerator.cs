using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace NekoLib.Watchdog.Generators
{
    /// <summary>Build-only generator for explicitly enabled desktop application entry routing.</summary>
    [Generator]
    public sealed class AutoBootstrapGenerator : IIncrementalGenerator
    {
        internal const string EntryType = "NekoLib.Watchdog.Generated.ApplicationEntryPoint";
        private static readonly DiagnosticDescriptor InvalidIntegration = new DiagnosticDescriptor(
            "NEKOWDG001", "Invalid automatic Watchdog integration", "{0}", "NekoLib.Watchdog",
            DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor InvalidEntry = new DiagnosticDescriptor(
            "NEKOWDG002", "Unsupported automatic Watchdog entry point", "{0}", "NekoLib.Watchdog",
            DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ManualBootstrapConflict = new DiagnosticDescriptor(
            "NEKOWDG003", "Manual and automatic Watchdog startup overlap",
            "Automatic bootstrap is enabled. Remove the manual EnsureStarted call or disable auto bootstrap to keep manual startup ownership.",
            "NekoLib.Watchdog", DiagnosticSeverity.Warning, true);

        /// <summary>Registers compilation and consumer-property inputs.</summary>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider),
                (production, pair) => Generate(production, pair.Left, pair.Right.GlobalOptions));
        }

        private static string Property(AnalyzerConfigOptions options, string name)
            => options.TryGetValue("build_property." + name, out var value) ? value.Trim() : "";

        private static void Generate(SourceProductionContext output, Compilation compilation, AnalyzerConfigOptions options)
        {
            if (!string.Equals(Property(options, "NekoLibWatchdogAutoBootstrap"), "true", StringComparison.OrdinalIgnoreCase)) return;
            // WPF's temporary markup compilation is a library, not the application's final entry point.
            if (Property(options, "NekoLibWatchdogMarkupTemporary") == "true") return;
            if (!(compilation is CSharpCompilation csharp) ||
                (compilation.Options.OutputKind != OutputKind.ConsoleApplication && compilation.Options.OutputKind != OutputKind.WindowsApplication))
            {
                Error(output, InvalidIntegration, "Automatic Watchdog bootstrap is opt-in for C# executable projects only."); return;
            }
            var ui = Property(options, "NekoLibWatchdogUiFramework");
            if (ui != "Wpf" && ui != "WinForms")
            {
                Error(output, InvalidIntegration, "Select Wpf or WinForms with NekoLibWatchdogUiFramework; exactly one desktop framework is required."); return;
            }
            if (csharp.Options.MainTypeName != EntryType)
            {
                Error(output, InvalidIntegration, "The package-generated StartupObject was overridden. Remove that override or disable NekoLibWatchdogAutoBootstrap."); return;
            }
            var mode = Property(options, "NekoLibWatchdogHostingMode");
            if (mode != "SameExecutable" && mode != "Sidecar")
            {
                Error(output, InvalidIntegration, "NekoLibWatchdogHostingMode must be SameExecutable or Sidecar."); return;
            }
            if (compilation.SyntaxTrees.Any(tree => tree.GetRoot(output.CancellationToken).DescendantNodes().OfType<GlobalStatementSyntax>().Any()))
            {
                Error(output, InvalidEntry, "Automatic bootstrap requires an explicit Main or WPF App.xaml entry. Top-level statements cannot select StartupObject."); return;
            }
            var originalType = Property(options, "NekoLibWatchdogOriginalStartupObject");
            var application = csharp.WithOptions(csharp.Options.WithMainTypeName(string.IsNullOrWhiteSpace(originalType) ? null : originalType));
            var main = application.GetEntryPoint(output.CancellationToken);
            if (main == null || main.ContainingType.IsGenericType)
            {
                Error(output, InvalidEntry, "Cannot identify one original application entry point. Set StartupObject to its type before the package targets are imported."); return;
            }
            foreach (var tree in application.SyntaxTrees)
            {
                var model = application.GetSemanticModel(tree);
                foreach (var call in tree.GetRoot(output.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(call, output.CancellationToken).Symbol is IMethodSymbol method &&
                        method.Name == "EnsureStarted" && method.ContainingType.ToDisplayString() == "NekoLib.Watchdog.WatchdogBootstrap")
                        output.ReportDiagnostic(Diagnostic.Create(ManualBootstrapConflict, call.GetLocation()));
                }
            }
            var type = MetadataName(main.ContainingType);
            var hasArgs = main.Parameters.Length == 1;
            var nullable = ((CSharpParseOptions)compilation.SyntaxTrees.First().Options).LanguageVersion >= LanguageVersion.CSharp8
                ? "#nullable disable\n" : "";
            var source = new StringBuilder("// <auto-generated/>\n" + nullable);
            source.Append(@"
namespace NekoLib.Watchdog.Generated
{
    internal static class ApplicationEntryPoint
    {
        [global::System.STAThread]
        public static int Main(string[] args)
        {
            int supervisorExit;
            if (global::NekoLib.Watchdog.WatchdogAutoBootstrap.TryRunSupervisor(args, out supervisorExit))
                return supervisorExit;
            global::NekoLib.Watchdog.WatchdogAutoBootstrap.Prepare(new global::NekoLib.Watchdog.WatchdogAutoBootstrapOptions
            {
                HostingMode = global::NekoLib.Watchdog.WatchdogHostingMode.");
            source.Append(mode).Append("\n            });\n");
            if (ui == "WinForms")
                source.Append("            global::System.Windows.Forms.Application.Idle += OnIdle;\n");
            else
                source.Append("            global::System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(global::System.Windows.Threading.DispatcherPriority.Loaded, new global::System.Action(global::NekoLib.Watchdog.WatchdogAutoBootstrap.Activate));\n");
            source.Append("            var applicationType = typeof(ApplicationEntryPoint).Assembly.GetType(")
                .Append(SymbolDisplay.FormatLiteral(type, true)).Append(@", true);
            var method = applicationType.GetMethod(""Main"", global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic,
                null, ");
            source.Append(hasArgs ? "new global::System.Type[] { typeof(string[]) }" : "global::System.Type.EmptyTypes");
            source.Append(", null);\n            object result;\n            try { result = method.Invoke(null, ")
                .Append(hasArgs ? "new object[] { args }" : "null").Append(@"); }
            catch (global::System.Reflection.TargetInvocationException exception)
            {
                global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
            var task = result as global::System.Threading.Tasks.Task;
            if (task != null) task.GetAwaiter().GetResult();
            int exitCode = result is int ? (int)result : result is global::System.Threading.Tasks.Task<int> ? ((global::System.Threading.Tasks.Task<int>)result).Result : global::System.Environment.ExitCode;
            global::NekoLib.Watchdog.WatchdogAutoBootstrap.CompleteApplication();
            return exitCode;
        }
");
            if (ui == "WinForms") source.Append(@"
        private static void OnIdle(object sender, global::System.EventArgs args)
        {
            global::System.Windows.Forms.Application.Idle -= OnIdle;
            global::NekoLib.Watchdog.WatchdogAutoBootstrap.Activate();
        }
");
            source.Append("    }\n}\n");
            output.AddSource("NekoLib.Watchdog.ApplicationEntryPoint.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static string MetadataName(INamedTypeSymbol type)
        {
            if (type.ContainingType != null) return MetadataName(type.ContainingType) + "+" + type.MetadataName;
            return type.ContainingNamespace.IsGlobalNamespace ? type.MetadataName : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
        }

        private static void Error(SourceProductionContext context, DiagnosticDescriptor descriptor, string message)
            => context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, message));
    }
}
