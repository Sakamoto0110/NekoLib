#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NekoLib.Inspection;
using NekoLib.Navigation;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    internal sealed class NavigationSamples : IScenarioSamples
    {
        public static readonly string[] ColumnNamesForHeader =
        {
            "scenario_page_instances_alive",
            "scenario_pages_attached",
            "scenario_pages_visible",
            "scenario_background_active",
            "native_views_live",
            "modal_views_live",
            "history_back",
            "history_forward",
            "inspection_retained",
            "inspection_providers",
            "inspection_actions",
            "scenario_active_requests",
            "navigation_cache_count",
            "navigation_queue_depth",
            "navigation_active_attempts",
            "navigation_idle_running",
            "navigation_gate_depth"
        };

        private readonly ScenarioState _state;
        private readonly IScenarioPlatform _platform;
        private readonly InspectionRuntime _inspection;

        public NavigationSamples(
            ScenarioState state,
            IScenarioPlatform platform,
            InspectionRuntime inspection)
        {
            _state = state;
            _platform = platform;
            _inspection = inspection;
        }

        public IReadOnlyList<string> ColumnNames => ColumnNamesForHeader;

        public long[] Read()
        {
            InspectionRuntimeDiagnostics diagnostics = _inspection.GetDiagnostics();
            long back = -1;
            long forward = -1;
            try
            {
                back = NavigationService.History.HistoryBack.LongCount();
                forward = NavigationService.History.HistoryForward.LongCount();
            }
            catch (InvalidOperationException)
            {
                // Unmounted cleanup samples truthfully mark history unavailable.
            }

            return new[]
            {
                (long)_state.AlivePageCount,
                _state.AttachedPageCount,
                _state.VisiblePageCount,
                _state.ActiveBackground,
                _platform.Controls.Metrics.ViewsLive,
                _platform.Controls.Metrics.ModalViewsLive,
                back,
                forward,
                diagnostics.RetainedCount,
                diagnostics.ProviderCount,
                diagnostics.ActionCount,
                _state.ApiRequests - _state.ApiTerminals,
                -1L,
                -1L,
                -1L,
                -1L,
                -1L
            };
        }
    }

    internal static class ScenarioFacts
    {
        public static string ScenarioVersion => RuntimeFacts.AssemblyVersion(typeof(ScenarioFacts));
        public static string NavigationVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Navigation", typeof(NavigationService));
        public static string InspectionVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Inspection", typeof(InspectionRuntime));
    }

    internal sealed class NavigationSummary : IScenarioSummary
    {
        private readonly IScenarioPlatform _platform;

        public NavigationSummary(IScenarioPlatform platform)
        {
            _platform = platform;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Platform", _platform.DisplayName),
            new KeyValuePair<string, string>("Navigation", ScenarioFacts.NavigationVersion),
            new KeyValuePair<string, string>("Inspection", ScenarioFacts.InspectionVersion)
        };

        public void WriteJson(JsonWriter json)
        {
            json.Prop("platform", _platform.PlatformId);
            json.Prop("adapter", RuntimeFacts.DescribeAssembly("adapter", _platform.AdapterMarkerType));
            json.Prop("navigation", ScenarioFacts.NavigationVersion);
            json.Prop("inspection", ScenarioFacts.InspectionVersion);
            json.Prop("nativeChildCountAtSummary", _platform.NativeChildCount);

            json.Array("claimBoundaries", () =>
            {
                foreach (string boundary in ClaimBoundaries) json.Item(boundary);
            });
        }

        private static readonly string[] ClaimBoundaries =
        {
            "Automated mode uses a real native host and message pump, but its assertions do not depend on pixels. " +
            "Visible adapter behavior remains interactive evidence through the documented WinForms and WPF smoke procedures.",

            "Every workload and failure control is owned by this scenario. No TestControl, Instrumentation, reflection hook, " +
            "or product fault-injection API is used.",

            "Navigation passive Inspection is enabled only through the public opt-in recorder surface. The scenario registers " +
            "and invokes no Inspection action.",

            "The public API exposes no navigation-gate depth. samples.csv records -1 for that column rather than inferring or " +
            "reflecting into the frozen runtime.",

            "Passive Inspection exposes cache, queue, active-attempt, background, overlay, and idle projections as object values. " +
            "The scenario asserts their provider presence but records -1 for opaque numeric fields rather than reflecting into them.",

            "Memory is sampled as a trend. The build-first delivery establishes no memory threshold; deterministic teardown, " +
            "page/surface ownership, handlers, timers, and native child counts are asserted directly."
        };
    }
}
