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
using Demo.Pages;

namespace Demo {
    public partial class Form1 : Form
    {
        PanelPageHost _host;
        Diagnostics diagnostics;
        MemoryTelemetrySink memory= new MemoryTelemetrySink();
        public Form1()
        {
            InitializeComponent();

            diagnostics = new Diagnostics(new Logger(LogLevel.Debug,new DebugLogSink()), memory);
             

            _host = new PanelPageHost(panel1);



            var ctx = PageNavBootstrap.Use<WinFormsPlatformAdapter>(panel1)
               .RegisterPagesFromAssembly(typeof(Form1).Assembly)
                .UseDiagnostics(diagnostics)
               .ConfigurePages(cfg =>
               {
                   cfg.Page<Pages.PageA>().AsHome();
               }).Start();

            NavigationService.OnNoPageVisible += () =>
            {
                Debug.WriteLine("No page is visible!");
            };
            NavigationService.OnFirstPageAttached += (page) =>
            {
                Debug.WriteLine($"{page.Name} first attatched");
            };
            NavigationService.CurrentChanged += (page) =>
            {
                Debug.WriteLine("switched to " + page.Name);
            };

            NavigationService.UseContext(ctx);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            NavigationService.SwitchPage<HomePage>();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            NavigationService.SwitchPage<Pages.PageB>();

        }

        private void button3_Click(object sender, EventArgs e)
        {
            NavigationService.SwitchPage<Pages.PageA>();
            var result = memory.Snapshot();
            foreach (var page in result)
                Debug.WriteLine(page.Duration + " | " + "(" + string.Join(",", page.Data.Values));

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            await NavigationService.GoHomeAsync();
        }

        
    }
}

