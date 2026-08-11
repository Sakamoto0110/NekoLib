#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions("wpf");
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-NAV WPF: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText("wpf"));
                return ExitCodes.Usage;
            }

            if (options.PrintScheduleOnly) return NavigationScenarioRun.PrintSchedule(options);

            Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ScenarioWindow window = new ScenarioWindow(options);
            bool interrupted = false;
            Console.CancelKeyPress += (_, e) =>
            {
                if (interrupted) return;
                interrupted = true;
                e.Cancel = true;
                window.RequestCancellation();
            };
            app.Run(window);
            return window.ExitCode;
        }
    }

    internal sealed class ScenarioWindow : Window
    {
        private readonly ScenarioOptions _options;
        private readonly Grid _host = new Grid { ClipToBounds = true };
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private bool _complete;

        public ScenarioWindow(ScenarioOptions options)
        {
            _options = options;
            Title = "NekoLib E3-NAV WPF";
            Width = 960;
            Height = 640;
            Content = _host;
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        public int ExitCode { get; private set; } = ExitCodes.Unexpected;
        public void RequestCancellation() => _cancellation.Cancel();

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfScenarioPlatform platform = new WpfScenarioPlatform(_host);
                ExitCode = await new NavigationScenarioRun(
                    _options, platform, _cancellation.Token).ExecuteAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("E3-NAV WPF failed outside the scenario: " + ex);
                ExitCode = ExitCodes.Unexpected;
            }
            finally
            {
                _complete = true;
                _cancellation.Dispose();
                Application.Current.Shutdown(ExitCode);
            }
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_complete) return;
            e.Cancel = true;
            _cancellation.Cancel();
        }
    }
}
