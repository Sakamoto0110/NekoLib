using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
    /// </summary>
    public sealed class WpfInteractionBlocker :
        IPageAwareInteractionBlocker
    {
        private readonly UIElement _root;
        private readonly Dictionary<UIElement, bool> _originalStates =
            new Dictionary<UIElement, bool>();
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
                else if (_root.IsEnabled)
                {
                    // Non-Panel host (shouldn't happen via WpfPlatformAdapter): best effort.
                    CaptureState(_root);
                    _root.IsEnabled = false;
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
            _originalStates.Remove(element);

            if (_blockDepth > 0 && wasTop && _modalStack.Count > 0)
                Restore(_modalStack[_modalStack.Count - 1]);
        }

        private void RestoreDisabledElements()
        {
            foreach (var pair in _originalStates)
            {
                try { pair.Key.IsEnabled = pair.Value; } catch { }
            }

            _originalStates.Clear();
            _modalStack.Clear();
        }

        private void CaptureState(UIElement element)
        {
            if (!_originalStates.ContainsKey(element))
                _originalStates.Add(element, element.IsEnabled);
        }

        private static void Disable(UIElement element)
        {
            if (element.IsEnabled)
                element.IsEnabled = false;
        }

        private void Restore(UIElement element)
        {
            if (_originalStates.TryGetValue(element, out var enabled))
                element.IsEnabled = enabled;
        }
    }
}
