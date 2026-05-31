using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageE
{
    public class PageF : PageView
    {
        private readonly PageFViewModel _viewModel;

        public PageF()
        {
            _viewModel = new PageFViewModel();

            BackColor = Color.Plum;
            Name = nameof(PageF);

            var label = new Label
            {
                Text = "PAGE F",
                Font = new Font("Microsoft Sans Serif", 28F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(40, 40)
            };

            var btnBack = new Button
            {
                Text = "← Back",
                Size = new Size(140, 34),
                Location = new Point(40, 100)
            };
            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);

            Controls.Add(label);
            Controls.Add(btnBack);
        }
    }
}
