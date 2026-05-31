using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageA
{
    public partial class PageA : PageView
    {
        private readonly PageAViewModel _viewModel;

        public PageA()
        {
            InitializeComponent();

            _viewModel = new PageAViewModel();

            BackColor = Color.LightBlue;

            var label = new Label
            {
                Text = "PAGE A  (hub)",
                Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(40, 40)
            };

            var btnBack = MakeNavButton("← Back", new Point(40, 100),
                () => _viewModel.GoBackCommand.Execute(null));
            var btnToB = MakeNavButton("→ PAGE B", new Point(40, 154),
                () => _viewModel.GoToBCommand.Execute(null));
            var btnToC = MakeNavButton("→ PAGE C", new Point(40, 198),
                () => _viewModel.GoToCCommand.Execute(null));
            var btnToD = MakeNavButton("→ PAGE D", new Point(40, 242),
                () => _viewModel.GoToDCommand.Execute(null));
            var btnToE = MakeNavButton("→ PAGE E", new Point(40, 286),
                () => _viewModel.GoToECommand.Execute(null));

            Controls.Add(label);
            Controls.Add(btnBack);
            Controls.Add(btnToB);
            Controls.Add(btnToC);
            Controls.Add(btnToD);
            Controls.Add(btnToE);
        }

        private static Button MakeNavButton(string text, Point location, System.Action onClick)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(160, 34),
                Location = location
            };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
