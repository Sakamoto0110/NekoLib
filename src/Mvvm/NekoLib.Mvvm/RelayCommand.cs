using System;
using System.Windows.Input;

namespace NekoLib.Mvvm
{
    /// <summary>
    /// Non-generic <see cref="ICommand"/> that dispatches to a delegate. The
    /// command parameter is passed through as <see cref="object"/> and may be
    /// <c>null</c>, because that is what a binding with no command parameter
    /// supplies; for typed parameters prefer <see cref="RelayCommand{T}"/>.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// Creates a command that passes the binding parameter to the supplied
        /// delegates unchanged.
        /// </summary>
        /// <param name="execute">Delegate invoked by <see cref="Execute(object)"/>.</param>
        /// <param name="canExecute">
        /// Optional predicate used by <see cref="CanExecute(object)"/>; when
        /// omitted, the command is always executable.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="execute"/> is <c>null</c>.</exception>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Creates a parameterless command. Any binding parameter supplied to
        /// <see cref="Execute(object)"/> or <see cref="CanExecute(object)"/> is ignored.
        /// </summary>
        /// <param name="execute">Parameterless delegate invoked by <see cref="Execute(object)"/>.</param>
        /// <param name="canExecute">
        /// Optional parameterless predicate; when omitted, the command is always executable.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="execute"/> is <c>null</c>.</exception>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = _ => execute();
            if (canExecute != null) _canExecute = _ => canExecute();
        }

        /// <summary>
        /// Evaluates the configured predicate for the supplied binding parameter.
        /// </summary>
        /// <param name="parameter">The binding-provided parameter, which may be <c>null</c>.</param>
        /// <returns><c>true</c> when no predicate was supplied or the predicate accepts the parameter.</returns>
        public bool CanExecute(object? parameter)
            => _canExecute == null || _canExecute(parameter);

        /// <summary>
        /// Invokes the delegate. It does not consult <see cref="CanExecute(object)"/>,
        /// matching <see cref="ICommand"/>: WPF gates the call through the bound
        /// control, and WinForms does not gate it at all.
        /// </summary>
        /// <param name="parameter">The binding-provided parameter, which may be <c>null</c>.</param>
        public void Execute(object? parameter)
            => _execute(parameter);

        /// <summary>
        /// Occurs when a consumer should reevaluate <see cref="CanExecute(object)"/>.
        /// The command raises the event only through <see cref="RaiseCanExecuteChanged"/>.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Notifies subscribers that <see cref="CanExecute(object)"/> may have
        /// changed. Call this from the view-model when a dependency of the
        /// predicate updates. It raises synchronously on the calling thread, and
        /// subscriber exceptions propagate to that caller.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Generic <see cref="ICommand"/> with a typed parameter. Lets the view-model
    /// declare <c>RelayCommand&lt;MyModel&gt;</c> instead of casting <c>object</c>
    /// at every call site. The parameter is coerced from the framework's
    /// <c>object</c> as follows:
    /// <list type="bullet">
    /// <item><description>If the supplied parameter is already a <typeparamref name="T"/>, it is used directly.</description></item>
    /// <item><description>If the parameter is <c>null</c> and <typeparamref name="T"/> is a reference type or nullable value type, <c>default(T)</c> is passed.</description></item>
    /// <item><description>Otherwise <see cref="CanExecute(object)"/> returns <c>false</c> and <see cref="Execute(object)"/> is a no-op, so a stale binding can never throw <see cref="InvalidCastException"/> through the command.</description></item>
    /// </list>
    /// Coercion requires an exact runtime type match: there is no numeric
    /// widening, no enum conversion from an integer, and no string parsing.
    /// </summary>
    /// <typeparam name="T">The exact command-parameter type accepted from the binding.</typeparam>
    public sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;

        /// <summary>
        /// Creates a typed command whose delegates receive only parameters that
        /// satisfy this type's exact coercion rules.
        /// </summary>
        /// <param name="execute">Delegate invoked for an accepted parameter.</param>
        /// <param name="canExecute">
        /// Optional predicate for accepted parameters; when omitted, every
        /// successfully coerced parameter is executable.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="execute"/> is <c>null</c>.</exception>
        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Coerces <paramref name="parameter"/> to <typeparamref name="T"/> and
        /// evaluates the configured predicate when coercion succeeds.
        /// </summary>
        /// <param name="parameter">The binding-provided parameter, which may be <c>null</c>.</param>
        /// <returns>
        /// <c>false</c> when coercion fails; otherwise the predicate result, or
        /// <c>true</c> when no predicate was supplied.
        /// </returns>
        public bool CanExecute(object? parameter)
        {
            if (!TryCoerce(parameter, out T typed))
                return false;
            return _canExecute == null || _canExecute(typed);
        }

        /// <summary>
        /// Coerces <paramref name="parameter"/> to <typeparamref name="T"/> and
        /// invokes the delegate. A parameter that cannot be coerced makes this a
        /// silent no-op rather than an <see cref="InvalidCastException"/>, so a
        /// stale binding does nothing instead of failing.
        /// <para/>
        /// Like <see cref="RelayCommand.Execute(object)"/>, this does not consult
        /// <see cref="CanExecute(object)"/>: WPF gates the call through the bound
        /// control, and WinForms does not gate it at all.
        /// </summary>
        /// <param name="parameter">The binding-provided parameter, which may be <c>null</c>.</param>
        public void Execute(object? parameter)
        {
            if (!TryCoerce(parameter, out T typed))
                return;
            _execute(typed);
        }

        /// <summary>
        /// Occurs when a consumer should reevaluate <see cref="CanExecute(object)"/>.
        /// The command raises the event only through <see cref="RaiseCanExecuteChanged"/>.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <inheritdoc cref="RelayCommand.RaiseCanExecuteChanged" />
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        // ---- parameter coercion ------------------------------------------

        private static bool TryCoerce(object? parameter, out T typed)
        {
            if (parameter is T direct)
            {
                typed = direct;
                return true;
            }

            if (parameter == null)
            {
                // null is valid for reference types and Nullable<>; otherwise reject.
                if (!typeof(T).IsValueType
                    || Nullable.GetUnderlyingType(typeof(T)) != null)
                {
                    typed = default!;
                    return true;
                }

                typed = default!;
                return false;
            }

            // Wrong runtime type — reject rather than throw InvalidCastException
            // through the binding pipeline.
            typed = default!;
            return false;
        }
    }
}
