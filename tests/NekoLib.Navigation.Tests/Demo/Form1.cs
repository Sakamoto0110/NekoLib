using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using NavigationDemo.Pages;
using Demo.Pages;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.WinForms.Pages;


namespace NavigationDemo 
{
    public partial class Form1 : Form
    {
        WinFormsLayeredPageHostBase _host;

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;

            Logger logger = new Logger(LogLevel.Debug, new DebugLogSink());
            MemoryTelemetrySink memory = new MemoryTelemetrySink();
            Diagnostics diagnostics = new Diagnostics(logger, memory);

            var ctx = PageNavBootstrap.Use<WinFormsPlatformAdapter>(panel1)

              .RegisterPagesFromAssembly(typeof(Form1).Assembly)
              .ConfigurePages(cfg => cfg.Register<ConfirmDialog>())
              .UseDiagnostics(diagnostics)
              .ConfigurePages(cfg => { cfg.Page<HomePage>().AsHome(); cfg.Page<ConfirmDialog>().AsModal(); })
              .Start();

            foreach(var des in ctx.Registry.AllDescriptors())
            {
                Console.WriteLine($"Registered page: {des.PageType.FullName}");
            }
            NavigationService.UseContext(ctx);














        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await NavigationService.GoHomeAsync();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
           await NavigationService.SwitchPage<PageA>();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
           await NavigationService.SwitchPage<PageB>();


        }

        private async void button3_Click(object sender, EventArgs e)
        {

           await NavigationService.SwitchPage<HeavyPage>();

        }

        private async void button4_Click(object sender, EventArgs e)
        {
           await NavigationService.SwitchPage<HomePage>();

        }


    }
}

