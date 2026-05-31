using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageD
{
    public class PageD : PageView
    {
        private readonly PageDViewModel _viewModel;

        public PageD()
        {
            _viewModel = new PageDViewModel();

            BackColor = Color.LightSalmon;
            Name = nameof(PageD);

            var label = new Label
            {
                Text = "PAGE D",
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

            var btnToE = new Button
            {
                Text = "Go to PAGE F →",
                Size = new Size(140, 34),
                Location = new Point(40, 144)
            };
            btnToE.Click += (s, e) => _viewModel.GoToFCommand.Execute(null);

            Controls.Add(label);
            Controls.Add(btnBack);
            Controls.Add(btnToE);
        }
    }
}
