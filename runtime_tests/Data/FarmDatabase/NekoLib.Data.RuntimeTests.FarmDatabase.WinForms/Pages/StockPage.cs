using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// Interactive stock control. Products move by a delta; animals leave the herd
    /// one at a time, and only with a reason the operator has typed.
    /// </summary>
    [PageMetadata(Name = "Controle de estoque", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class StockPage : FarmPageBase
    {
        private StockViewModel _vm;

        public StockPage()
        {
            InitializeComponent();
            ApplyTheme();

            if (IsInert) return;

            _vm = App.Stock;
            BuildProductColumns();
            BuildAnimalColumns();
            WireUp();
            Bind(_vm, _status, ApplyViewModel);
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleGrid(_productGrid);
            FarmTheme.StyleGrid(_animalGrid);

            _productBar.BackColor = FarmTheme.Surface;
            _animalBar.BackColor = FarmTheme.Surface;
            _deltaValue.BackColor = FarmTheme.SurfaceAlt;
            _deltaValue.ForeColor = FarmTheme.TextPrimary;
        }

        private void WireUp()
        {
            _productGrid.AutoGenerateColumns = false;
            _animalGrid.AutoGenerateColumns = false;
            _productGrid.DataSource = _vm.Products;
            _animalGrid.DataSource = _vm.Animals;

            _productGrid.SelectionChanged += (s, e) =>
                _vm.SelectedProduct = CurrentRow<Product>(_productGrid);

            _animalGrid.SelectionChanged += (s, e) =>
                _vm.SelectedAnimal = CurrentRow<Animal>(_animalGrid);

            _deltaValue.ValueChanged += (s, e) => _vm.Delta = (int)_deltaValue.Value;

            // The view-model asks for a reason; the view decides that "asking" means a
            // blocking prompt on the navigation host. Neither knows about the other's
            // machinery.
            _vm.RequestRemovalReason = animal =>
                NavigationService.ShowPromptAsync<ReasonPrompt, string>(animal);

            Bind(_refreshButton, _vm.RefreshCommand);
            Bind(_addButton, _vm.AddCommand);
            Bind(_removeButton, _vm.RemoveCommand);
            Bind(_removeAnimalButton, _vm.RemoveAnimalCommand);
        }

        private static T CurrentRow<T>(DataGridView grid) where T : class
        {
            if (grid.CurrentRow == null) return null;
            return grid.CurrentRow.DataBoundItem as T;
        }

        private void BuildProductColumns()
        {
            _productGrid.Columns.Clear();

            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Produto",
                DataPropertyName = nameof(Product.Name),
                FillWeight = 150
            });
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Categoria",
                DataPropertyName = nameof(Product.Category),
                FillWeight = 90
            });
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qtd",
                DataPropertyName = nameof(Product.Quantity),
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
                FillWeight = 55
            });
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Un",
                DataPropertyName = nameof(Product.Unit),
                FillWeight = 55
            });
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Preço",
                DataPropertyName = nameof(Product.UnitPrice),
                DefaultCellStyle =
                {
                    Format = "C2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                },
                FillWeight = 70
            });

            // Low stock is the one thing worth spotting at a glance.
            _productGrid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _vm.Products.Count) return;

                Product product = _vm.Products[e.RowIndex];
                e.CellStyle.ForeColor = product.Quantity <= 25
                    ? FarmTheme.Warn
                    : FarmTheme.TextPrimary;
            };
        }

        private void BuildAnimalColumns()
        {
            _animalGrid.Columns.Clear();

            _animalGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Brinco",
                DataPropertyName = nameof(Animal.Tag),
                FillWeight = 80
            });
            _animalGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Espécie",
                DataPropertyName = nameof(Animal.Species),
                FillWeight = 80
            });
            _animalGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Gênero",
                DataPropertyName = nameof(Animal.Gender),
                FillWeight = 75
            });
            _animalGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Idade",
                DataPropertyName = nameof(Animal.AgeYears),
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
                FillWeight = 55
            });
        }

        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            if (!IsInert && _vm.RefreshCommand.CanExecute(null))
                _vm.RefreshCommand.Execute(null);

            return Task.CompletedTask;
        }

        private void ApplyViewModel()
        {
            _productSummary.Text = _vm.ProductSummary;
            _animalSummary.Text = _vm.AnimalSummary;

            if ((int)_deltaValue.Value != _vm.Delta)
                _deltaValue.Value = Math.Min(_deltaValue.Maximum, Math.Max(_deltaValue.Minimum, _vm.Delta));
        }
    }
}
