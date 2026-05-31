using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageC
{
    public class PageC : PageView
    {
        private readonly PageCViewModel _viewModel;

        public PageC()
        {
            _viewModel = new PageCViewModel();

            BackColor = Color.Khaki;
            Name = nameof(PageC);

            var label = new Label
            {
                Text = "PAGE C",
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
