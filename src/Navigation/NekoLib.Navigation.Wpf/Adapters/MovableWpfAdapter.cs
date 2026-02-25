using PageNav.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PageNav.Wpf.Adapters
{
    public sealed class MovableWpfAdapter : IMovableAdapter
    {
        private class HandlerSet
        {
            public MouseButtonEventHandler Down;
            public MouseEventHandler Move;
            public MouseButtonEventHandler Up;
        }

        private readonly Dictionary<UIElement, HandlerSet> _map = new Dictionary<UIElement, HandlerSet>();

        public void MakeMovable(object view)
        {
            if(!(view is UIElement elem))
                throw new ArgumentException("WPF movable target must be UIElement.");

            if(_map.ContainsKey(elem))
                return;

            bool dragging = false;
            Point startMouse = new Point(0,0);
            Point startPos = new Point(0, 0);

            FrameworkElement fe = elem as FrameworkElement;
            DependencyObject parent = fe.Parent;


            bool isCanvas = parent is Canvas;

            TranslateTransform transform = null;
            if(!isCanvas)
            {
                transform = fe.RenderTransform as TranslateTransform;
                if(transform == null)
                {
                    transform = new TranslateTransform();
                    fe.RenderTransform = transform;
                }
            }

            var handlers = new HandlerSet();

            handlers.Down = (s, e) => {
                if(e.LeftButton != MouseButtonState.Pressed)
                    return;

                dragging = true;

                startMouse = e.GetPosition((IInputElement)parent ?? fe);

                if(isCanvas)
                {
                    startPos = new Point(
                        Canvas.GetLeft(fe),
                        Canvas.GetTop(fe)
                    );

                    if(double.IsNaN(startPos.X)) startPos.X = 0;
                    if(double.IsNaN(startPos.Y)) startPos.Y = 0;
                }

                fe.CaptureMouse();
            };

            handlers.Move = (s, e) => {
                if(!dragging) return;

                var currentMouse = e.GetPosition((IInputElement)parent ?? fe);
                var delta = currentMouse - startMouse;

                if(isCanvas)
                {
                    Canvas.SetLeft(fe, startPos.X + delta.X);
                    Canvas.SetTop(fe, startPos.Y + delta.Y);
                }
                else
                {
                    transform.X = delta.X;
                    transform.Y = delta.Y;
                }
            };

            handlers.Up = (s, e) => {
                dragging = false;
                fe.ReleaseMouseCapture();
            };

            elem.MouseDown += handlers.Down;
            elem.MouseMove += handlers.Move;
            elem.MouseUp += handlers.Up;

            _map[elem] = handlers;
        }

        public void RemoveMovable(object view)
        {
            if(!(view is UIElement elem)) return;

            if(!_map.TryGetValue(elem, out var h)) return;

            elem.MouseDown -= h.Down;
            elem.MouseMove -= h.Move;
            elem.MouseUp -= h.Up;

            _map.Remove(elem);
        }
    }
}
