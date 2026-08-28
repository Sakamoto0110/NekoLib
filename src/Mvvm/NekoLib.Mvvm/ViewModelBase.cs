using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NekoLib.Mvvm
{
    /// <summary>
    /// Minimal MVVM <see cref="INotifyPropertyChanged"/> base. Optional helper —
    /// provided so consumers don't reinvent the same handful of lines per app.
    /// Use with WinForms, WPF, MAUI, or any UI that observes
    /// <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs synchronously on the notifying thread when a property changes.
        /// A <c>null</c> or empty property name denotes that all properties changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> on the calling thread. A
        /// <c>null</c> or empty name means "every property changed" to both
        /// WinForms and WPF binding.
        ///
        /// Override this to intercept every notification in one place — it is the
        /// single funnel <see cref="SetProperty{T}"/> routes through, so a
        /// view-model updated from a background thread can marshal here instead of
        /// at each subscriber.
        /// </summary>
        /// <param name="propertyName">
        /// Changed property name, or <c>null</c> or empty to notify all properties.
        /// </param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
        /// <see cref="PropertyChanged"/> when the value actually changed. Returns
        /// <c>true</c> if a change was applied.
        ///
        /// Equality uses <see cref="EqualityComparer{T}.Default"/>, so a reference
        /// type without a value equality implementation compares by reference: an
        /// object mutated in place and reassigned raises nothing.
        /// </summary>
        /// <typeparam name="T">The property's value type.</typeparam>
        /// <param name="field">Backing field updated when the value changes.</param>
        /// <param name="value">Candidate value.</param>
        /// <param name="propertyName">Property name reported to subscribers.</param>
        /// <returns><c>true</c> when the field changed and a notification was raised; otherwise <c>false</c>.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
