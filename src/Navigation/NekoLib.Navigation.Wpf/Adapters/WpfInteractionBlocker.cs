using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Blocks background interaction while a modal surface (dialog / prompt) is open.
    /// Disables the host Panel's CURRENT children — not the host itself — so the
    /// modal overlay, which the service adds to the same Panel right AFTER
    /// <see cref="Block"/>, stays interactive. Disabling the host root instead would
    /// propagate the disabled state to that later-added overlay too (WPF coerces every
    /// descendant's effective IsEnabled), leaving the dialog's own controls dead.
    /// Only children we actually disabled are restored on <see cref="Unblock"/>.
    /// <para>
    /// Every write goes through <see cref="DependencyObject.SetCurrentValue"/>. A
    /// plain <c>IsEnabled = false</c> assignment sets a local value, which
    /// permanently clears any <c>Binding</c> or style setter on the property — one
    /// dialog was enough to sever a page's <c>IsEnabled</c> binding for the rest of
    /// the process.
    /// </para>
    /// </summary>
    public sealed class WpfInteractionBlocker :
        IPageAwareInteractionBlocker
    {
        private readonly UIElement _root;
        private readonly Dictionary<UIElement, bool> _originalStates =
            new Dictionary<UIElement, bool>();

        // Only elements this blocker actually turned off. Restoring anything else
        // would pin a value onto an element that was merely inheriting its state.
        private readonly HashSet<UIElement> _disabled = new HashSet<UIElement>();
        private readonly List<UIElement> _modalStack = new List<UIElement>();
        private int _blockDepth;

        public WpfInteractionBlocker(UIElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block()
        {
            _blockDepth++;
            if (_blockDepth != 1) return;

            _originalStates.Clear();
            _disabled.Clear();
            _modalStack.Clear();

            // Snapshot + disable the children present now (the page and any earlier
            // overlay). WPF propagates IsEnabled down each child's subtree, so one
            // toggle per direct child is enough — no recursion needed.
            try
            {
                if (_root is Panel panel)
                {
                    foreach (UIElement child in panel.Children)
                    {
                        CaptureState(child);
                        Disable(child);
                    }
                }
                else
                {
                    // Non-Panel host (shouldn't happen via WpfPlatformAdapter): best effort.
                    CaptureState(_root);
                    Disable(_root);
                }
            }
            catch
            {
                RestoreDisabledElements();
                _blockDepth = 0;
                throw;
            }
        }

        public void Unblock()
        {
            if (_blockDepth == 0) return;

            _blockDepth--;
            if (_blockDepth != 0) return;

            RestoreDisabledElements();
        }

        public void OnViewAdded(object view, bool isModalSurface)
        {
            if (_blockDepth == 0 || !(view is UIElement element))
                return;

            CaptureState(element);

            if (!isModalSurface)
            {
                _modalStack.Remove(element);
                Disable(element);
                return;
            }

            if (_modalStack.Count > 0)
                Disable(_modalStack[_modalStack.Count - 1]);

            _modalStack.Remove(element);
            _modalStack.Add(element);
            Restore(element);
        }

        public void OnViewRemoved(object view)
        {
            if (!(view is UIElement element))
                return;

            var wasTop =
                _modalStack.Count > 0 &&
                ReferenceEquals(_modalStack[_modalStack.Count - 1], element);

            _modalStack.Remove(element);
            Restore(element);
            _disabled.Remove(element);
            _originalStates.Remove(element);

            if (_blockDepth > 0 && wasTop && _modalStack.Count > 0)
                Restore(_modalStack[_modalStack.Count - 1]);
        }

        private void RestoreDisabledElements()
        {
            foreach (var element in _disabled)
            {
                try { ApplyRestore(element); } catch { }
            }

            _disabled.Clear();
            _originalStates.Clear();
            _modalStack.Clear();
        }

        private void CaptureState(UIElement element)
        {
            if (!_originalStates.ContainsKey(element))
                _originalStates.Add(element, element.IsEnabled);
        }

        private void Disable(UIElement element)
        {
            if (!element.IsEnabled)
                return;

            element.SetCurrentValue(UIElement.IsEnabledProperty, false);
            _disabled.Add(element);
        }

        private void Restore(UIElement element)
        {
            if (_disabled.Remove(element))
                ApplyRestore(element);
        }

        private void ApplyRestore(UIElement element)
        {
            if (_originalStates.TryGetValue(element, out var enabled))
                element.SetCurrentValue(UIElement.IsEnabledProperty, enabled);

            // The source may have changed while the surface was blocked, so let a
            // binding re-assert itself instead of leaving the captured value pinned.
            BindingOperations
                .GetBindingExpression(element, UIElement.IsEnabledProperty)
                ?.UpdateTarget();
        }
    }
}
