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
 

namespace NavigationDemo 
{
    public partial class Form1 : Form
    {
        PanelPageHost _host;
        Diagnostics diagnostics;
        MemoryTelemetrySink memory= new MemoryTelemetrySink();
        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
            diagnostics = new Diagnostics(new Logger(LogLevel.Debug,new DebugLogSink()), memory);
             

             
             
            


            var ctx = PageNavBootstrap.Use<WinFormsPlatformAdapter>(panel1)
               .RegisterPagesFromAssembly(typeof(Form1).Assembly)
                .UseDiagnostics(diagnostics)
               .ConfigurePages(cfg => cfg.Page<HomePage>().AsHome())
               .Start();




            NavigationService.UseContext(ctx);

          

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            NavigationService.GoHomeAsync();
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
             

        }

        private void button3_Click(object sender, EventArgs e)
        {
             

        }

        private async void button4_Click(object sender, EventArgs e)
        {
             
        }

        
    }
}

