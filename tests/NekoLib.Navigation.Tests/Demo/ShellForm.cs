using System.Drawing;
using System.Windows.Forms;

namespace NavigationDemo
{
    public class ShellForm : Form
    {
        public Panel HostPanel { get; }

        public ShellForm()
        {
            this.Text = "Shell";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            HostPanel = new Panel
            {
                Name = "HostPanel",
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            this.Controls.Add(HostPanel);
        }
    }
}
