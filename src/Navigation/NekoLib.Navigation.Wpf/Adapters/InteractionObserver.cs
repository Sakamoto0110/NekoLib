using NekoLib.Navigation.Contracts.Runtime;
using System;
using System.Windows;
using System.Windows.Input;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Raises <see cref="InteractionDetected"/> on any mouse, keyboard, or text input
    /// beneath the host. Uses WPF preview (tunneling) events on the root, so a single
    /// subscription covers the whole subtree regardless of which descendant is interacted
    /// with.
    /// </summary>
    public sealed class InteractionObserver : IInteractionObserverService, IDisposable
    {
        private readonly UIElement _root;

        public event Action InteractionDetected;

        public InteractionObserver(UIElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            _root.PreviewMouseDown += OnMouse;
            _root.PreviewKeyDown += OnKey;
            _root.PreviewTextInput += OnText;
        }

        private void OnMouse(object sender, MouseButtonEventArgs e) => InteractionDetected?.Invoke();
        private void OnKey(object sender, KeyEventArgs e) => InteractionDetected?.Invoke();
        private void OnText(object sender, TextCompositionEventArgs e) => InteractionDetected?.Invoke();

        public void Dispose()
        {
            _root.PreviewMouseDown -= OnMouse;
            _root.PreviewKeyDown -= OnKey;
            _root.PreviewTextInput -= OnText;
        }
    }
}
