#nullable enable
using System;
using System.Threading.Tasks;
using NekoLib.Mvvm;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>
    /// Shared plumbing for the scenario's view-models: busy state, a status line and
    /// an error line, plus the bridge that lets a synchronous
    /// <see cref="RelayCommand"/> drive asynchronous work.
    /// <para/>
    /// That bridge is the notable part. <c>NekoLib.Mvvm.RelayCommand</c> accepts
    /// <see cref="Action"/> and <see cref="Func{TResult}"/> of <see cref="bool"/>
    /// only - there is no <c>Func&lt;Task&gt;</c> overload and no async command type -
    /// so every database call in this app has to cross an <c>async void</c> boundary
    /// somewhere. Centralizing it here means exactly one place can swallow an
    /// exception, and it does not: failures land in <see cref="ErrorMessage"/>.
    /// </summary>
    public abstract class FarmViewModelBase : ViewModelBase
    {
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private string _errorMessage = string.Empty;

        protected FarmViewModelBase(FarmWorkspace workspace)
        {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public FarmWorkspace Workspace { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    RaiseCommandStates();
                }
            }
        }

        /// <summary>
        /// Re-evaluates every command this view-model owns. Must be overridden by
        /// each one, because <c>RelayCommand</c> has no automatic requery: unlike
        /// WPF's <c>CommandManager</c>, nothing polls <c>CanExecute</c>, so a state
        /// change that is not explicitly propagated leaves the bound control stuck at
        /// whatever it was told last.
        /// <para/>
        /// This is load-bearing for the busy flag in particular. The workspace raises
        /// <c>ConnectionChanged</c> from inside the awaited work, while
        /// <see cref="IsBusy"/> is still true, so commands evaluate to false there;
        /// without a second pass when the flag clears, every button stays disabled.
        /// </summary>
        protected virtual void RaiseCommandStates()
        {
        }

        /// <summary>Convenience inverse of <see cref="IsBusy"/> for enabling controls.</summary>
        public bool IsIdle => !_isBusy;

        public string StatusMessage
        {
            get => _statusMessage;
            protected set => SetProperty(ref _statusMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            protected set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        /// <summary>True when a database is open and this view-model can do work.</summary>
        public bool IsConnected => Workspace.IsConnected;

        /// <summary>Raised after any command finishes, so views can re-evaluate state.</summary>
        public event Action? CommandCompleted;

        /// <summary>
        /// Runs asynchronous work behind the busy flag, routing any failure into
        /// <see cref="ErrorMessage"/> instead of onto the UI thread's exception path.
        /// </summary>
        protected async void Run(Func<Task> work, string? successStatus = null)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await work().ConfigureAwait(true);
                if (successStatus != null)
                    StatusMessage = successStatus;
            }
            catch (Exception ex)
            {
                ErrorMessage = Describe(ex);
                StatusMessage = "Falhou.";
            }
            finally
            {
                IsBusy = false;
                CommandCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Flattens an exception chain. Provider errors frequently carry the useful
        /// text one or two levels down - ACE in particular reports syntax problems
        /// through an inner OleDbException.
        /// </summary>
        protected static string Describe(Exception ex)
        {
            string message = ex.Message;
            Exception? inner = ex.InnerException;

            int depth = 0;
            while (inner != null && depth < 3)
            {
                message += "  <- " + inner.Message;
                inner = inner.InnerException;
                depth++;
            }

            return message;
        }

        /// <summary>Re-reads connection-derived state. Called when the workspace changes.</summary>
        public virtual void OnConnectionChanged()
        {
            OnPropertyChanged(nameof(IsConnected));
        }
    }
}
