using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Windows.Forms;
using ConfirmDialogView = NavigationDemo.Pages.ConfirmDialog.ConfirmDialog;
using HomeView = NavigationDemo.Pages.Home.HomePage;

namespace NavigationDemo
{
    public partial class Form1 : Form
    {
        private readonly Form1ViewModel _viewModel;
        WinFormsLayeredPageHostBase _host;

        public Form1()
        {
            InitializeComponent();

            _viewModel = new Form1ViewModel();

            Load += Form1_Load;

            Logger logger = new Logger(LogLevel.Debug, new DebugLogSink());
            MemoryTelemetrySink memory = new MemoryTelemetrySink();
            Diagnostics diagnostics = new Diagnostics(logger, memory);

            var ctx = PageNavBootstrap.Use<WinFormsPlatformAdapter>(panel1)
              .RegisterPagesFromAssembly(typeof(Form1).Assembly)
              .ConfigurePages(cfg => cfg.Register<ConfirmDialogView>())
              .UseDiagnostics(diagnostics)
              .ConfigurePages(cfg => { cfg.Page<HomeView>().AsHome(); cfg.Page<ConfirmDialogView>().AsModal(); })
              .Start();

            foreach (var des in ctx.Registry.AllDescriptors())
            {
                Console.WriteLine($"Registered page: {des.PageType.FullName}");
            }
            NavigationService.UseContext(ctx);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _viewModel.GoHomeOnLoadCommand.Execute(null);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _viewModel.GoPageACommand.Execute(null);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _viewModel.GoPageBCommand.Execute(null);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _viewModel.GoHeavyPageCommand.Execute(null);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            _viewModel.GoHomeCommand.Execute(null);
        }
    }
}
