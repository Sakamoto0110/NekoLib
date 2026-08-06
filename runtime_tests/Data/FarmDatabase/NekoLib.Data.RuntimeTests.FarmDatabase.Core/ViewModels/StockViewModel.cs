#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using NekoLib.Mvvm;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>
    /// Interactive stock control. Products move by a quantity delta; animals are
    /// removed one at a time and always with a recorded reason.
    /// </summary>
    public sealed class StockViewModel : FarmViewModelBase
    {
        private readonly BindingList<Product> _products = new BindingList<Product>();
        private readonly BindingList<Animal> _animals = new BindingList<Animal>();
        private Product? _selectedProduct;
        private Animal? _selectedAnimal;
        private int _delta = 10;

        public StockViewModel(FarmWorkspace workspace) : base(workspace)
        {
            RefreshCommand = new RelayCommand(
                () => Run(RefreshAsync),
                () => IsIdle && IsConnected);

            AddCommand = new RelayCommand(
                () => Run(() => MoveStockAsync(+Math.Abs(Delta))),
                () => IsIdle && IsConnected && SelectedProduct != null && Delta != 0);

            RemoveCommand = new RelayCommand(
                () => Run(() => MoveStockAsync(-Math.Abs(Delta))),
                () => IsIdle && IsConnected && SelectedProduct != null && Delta != 0);

            RemoveAnimalCommand = new RelayCommand(
                () => Run(RemoveAnimalAsync),
                () => IsIdle && IsConnected && SelectedAnimal != null);

            AddAnimalCommand = new RelayCommand(
                () => Run(AddAnimalAsync),
                () => IsIdle && IsConnected);
        }

        /// <summary>
        /// Asks the view to collect a new animal's details, returning <c>null</c> when
        /// the user backs out. The counterpart of
        /// <see cref="RequestRemovalReason"/>, and deliberately typed: it is the second
        /// prompt result type in the scenario, which is what makes the cost of a
        /// generic prompt base visible rather than theoretical.
        /// </summary>
        public Func<Task<NewAnimalRequest?>>? RequestNewAnimal { get; set; }

        /// <summary>
        /// Asks the view for a removal reason, returning <c>null</c> when the user
        /// backs out. This is the whole interaction contract between the view-model
        /// and the navigation module's prompt service - the view-model never learns
        /// what a prompt looks like, and the view never learns what a transaction is.
        /// </summary>
        public Func<Animal, Task<string?>>? RequestRemovalReason { get; set; }

        public BindingList<Product> Products => _products;
        public BindingList<Animal> Animals => _animals;

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    OnPropertyChanged(nameof(ProductSummary));
                    AddCommand.RaiseCanExecuteChanged();
                    RemoveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public Animal? SelectedAnimal
        {
            get => _selectedAnimal;
            set
            {
                if (SetProperty(ref _selectedAnimal, value))
                {
                    OnPropertyChanged(nameof(AnimalSummary));
                    RemoveAnimalCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Quantity applied by the add/remove buttons.</summary>
        public int Delta
        {
            get => _delta;
            set
            {
                if (SetProperty(ref _delta, value))
                {
                    AddCommand.RaiseCanExecuteChanged();
                    RemoveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand RemoveCommand { get; }
        public RelayCommand RemoveAnimalCommand { get; }
        public RelayCommand AddAnimalCommand { get; }

        public string ProductSummary => SelectedProduct == null
            ? "Nenhum produto selecionado"
            : SelectedProduct.Name + "  ·  " + SelectedProduct.Quantity + " " +
              SelectedProduct.Unit + "  ·  " + SelectedProduct.Category;

        public string AnimalSummary => SelectedAnimal == null
            ? "Nenhum animal selecionado"
            : SelectedAnimal.Tag + "  ·  " + SelectedAnimal.Species + "  ·  " +
              SelectedAnimal.Gender + "  ·  " + SelectedAnimal.AgeYears + " ano(s)";

        private async Task RefreshAsync()
        {
            FarmDb db = Workspace.Require();

            List<Product> products = await db.GetProductsAsync().ConfigureAwait(true);
            List<Animal> animals = await db.GetAnimalsAsync().ConfigureAwait(true);

            int? keepProductId = SelectedProduct?.Id;
            int? keepAnimalId = SelectedAnimal?.Id;

            Replace(_products, products);
            Replace(_animals, animals);

            SelectedProduct = keepProductId == null ? null : _products.FirstOrDefaultById(keepProductId.Value);
            SelectedAnimal = keepAnimalId == null ? null : _animals.FirstOrDefaultById(keepAnimalId.Value);

            StatusMessage = _products.Count + " produto(s), " + _animals.Count + " animal(is).";
        }

        private static void Replace<T>(BindingList<T> target, List<T> source)
        {
            // RaiseListChangedEvents is toggled so a bound grid repaints once instead
            // of once per row.
            target.RaiseListChangedEvents = false;
            target.Clear();
            foreach (T item in source)
                target.Add(item);
            target.RaiseListChangedEvents = true;
            target.ResetBindings();
        }

        private async Task MoveStockAsync(int delta)
        {
            Product? product = SelectedProduct;
            if (product == null) return;

            string movement = delta > 0 ? "entrada" : "saída";

            await Workspace.Require()
                .ChangeProductQuantityAsync(product, delta, "ajuste manual (" + movement + ")")
                .ConfigureAwait(true);

            _products.ResetBindings();
            OnPropertyChanged(nameof(ProductSummary));
            StatusMessage = movement + " de " + Math.Abs(delta) + " em " + product.Name +
                " - agora " + product.Quantity + " " + product.Unit + ".";
        }

        private async Task RemoveAnimalAsync()
        {
            Animal? animal = SelectedAnimal;
            if (animal == null) return;

            if (RequestRemovalReason == null)
            {
                throw new InvalidOperationException(
                    "Nenhum coletor de motivo foi ligado ao view-model, então a " +
                    "remoção seria registrada sem justificativa.");
            }

            string? reason = await RequestRemovalReason(animal).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(reason))
            {
                StatusMessage = "Remoção cancelada - nenhum motivo informado.";
                return;
            }

            await Workspace.Require()
                .RemoveAnimalAsync(animal, reason!.Trim())
                .ConfigureAwait(true);

            _animals.Remove(animal);
            SelectedAnimal = null;
            StatusMessage = animal.Tag + " removido. Motivo registrado no log.";
        }

        private async Task AddAnimalAsync()
        {
            if (RequestNewAnimal == null)
            {
                throw new InvalidOperationException(
                    "Nenhum coletor de cadastro foi ligado ao view-model.");
            }

            NewAnimalRequest? request = await RequestNewAnimal().ConfigureAwait(true);
            if (request == null)
            {
                StatusMessage = "Cadastro cancelado.";
                return;
            }

            Animal created = await Workspace.Require()
                .AddAnimalAsync(request)
                .ConfigureAwait(true);

            _animals.Add(created);
            _animals.ResetBindings();
            SelectedAnimal = created;

            StatusMessage = created.Tag + " registrado. A numeração não reaproveita " +
                "brincos de animais removidos.";
        }

        protected override void RaiseCommandStates()
        {
            RefreshCommand.RaiseCanExecuteChanged();
            AddCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
            RemoveAnimalCommand.RaiseCanExecuteChanged();
            AddAnimalCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();

            _products.Clear();
            _animals.Clear();
            SelectedProduct = null;
            SelectedAnimal = null;

            RaiseCommandStates();
        }
    }

    internal static class BindingListExtensions
    {
        public static Product? FirstOrDefaultById(this BindingList<Product> list, int id)
        {
            foreach (Product item in list)
                if (item.Id == id) return item;
            return null;
        }

        public static Animal? FirstOrDefaultById(this BindingList<Animal> list, int id)
        {
            foreach (Animal item in list)
                if (item.Id == id) return item;
            return null;
        }
    }
}
