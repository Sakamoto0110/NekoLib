using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NekoLib.Navigation.Contracts.Runtime;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// WPF's routed events bubble from leaf to root, so a single Preview subscription
    /// at the host catches every mouse / key / text interaction regardless of how
    /// deeply nested the source is — no per-control hooking required.
    /// </summary>
    public sealed class WpfInteractionObserver : IInteractionObserverService, IDisposable
    {
        private readonly UIElement _root;
        private readonly MouseButtonEventHandler _mouseDown;
        private readonly KeyEventHandler _keyDown;
        private readonly TextCompositionEventHandler _textInput;
        private readonly RoutedEventHandler _controlChanged;
        private readonly TextChangedEventHandler _textChanged;
        private readonly InteractionObservationMode _mode;

        /// <inheritdoc />
        public event Action? InteractionDetected;

        event Action? IInteractionObserverService.InteractionDetected
        {
            add { if (value != null) InteractionDetected += value; }
            remove { if (value != null) InteractionDetected -= value; }
        }

        /// <summary>Initializes input observation for one WPF root.</summary>
        /// <param name="root">Root element whose routed input events are observed.</param>
        public WpfInteractionObserver(UIElement root)
            : this(root, InteractionObservationMode.InputOnly)
        {
        }

        /// <summary>Observes routed events using an explicit, portable activity policy.</summary>
        /// <param name="root">Root whose routed events are observed.</param>
        /// <param name="mode">Input only, or input plus control changes. The original constructor retains its legacy policy.</param>
        public WpfInteractionObserver(UIElement root, InteractionObservationMode mode)
        {
            if (!Enum.IsDefined(typeof(InteractionObservationMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _mode = mode;

            _mouseDown = (_, __) => InteractionDetected?.Invoke();
            _keyDown = (_, __) => InteractionDetected?.Invoke();
            _textInput = (_, __) => InteractionDetected?.Invoke();
            _controlChanged = (_, __) => InteractionDetected?.Invoke();
            _textChanged = (_, __) => InteractionDetected?.Invoke();

            _root.PreviewMouseDown += _mouseDown;
            _root.PreviewKeyDown += _keyDown;
            _root.PreviewTextInput += _textInput;
            if (_mode == InteractionObservationMode.InputAndControlChanges)
            {
                _root.AddHandler(ButtonBase.ClickEvent, _controlChanged, true);
                _root.AddHandler(TextBoxBase.TextChangedEvent, _textChanged, true);
            }
        }

        /// <summary>Removes every routed input handler installed by this observer.</summary>
        public void Dispose()
        {
            try { _root.PreviewMouseDown -= _mouseDown; } catch { }
            try { _root.PreviewKeyDown -= _keyDown; } catch { }
            try { _root.PreviewTextInput -= _textInput; } catch { }
            if (_mode == InteractionObservationMode.InputAndControlChanges)
            {
                _root.RemoveHandler(ButtonBase.ClickEvent, _controlChanged);
                _root.RemoveHandler(TextBoxBase.TextChangedEvent, _textChanged);
            }
        }
    }
}
