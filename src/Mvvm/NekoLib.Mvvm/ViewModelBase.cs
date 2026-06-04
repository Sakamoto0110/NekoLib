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
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
        /// <see cref="PropertyChanged"/> when the value actually changed. Returns
        /// <c>true</c> if a change was applied.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
