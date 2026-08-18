using System.Collections.Generic;
using System.Windows.Input;
using System;
using NekoLib.Mvvm;
using Xunit;

namespace NekoLib.Mvvm.Tests.Unit
{
    public class RelayCommandTests
    {
        [Fact]
        public void Execute_InvokesDelegate_WithParameter()
        {
            object captured = null;
            var cmd = new RelayCommand(p => captured = p);

            cmd.Execute("payload");

            Assert.Equal("payload", captured);
        }

        [Fact]
        public void Execute_NoParameterOverload_InvokesDelegate()
        {
            int calls = 0;
            var cmd = new RelayCommand(() => calls++);

            cmd.Execute(null);
            cmd.Execute("anything");

            Assert.Equal(2, calls);
        }

        [Fact]
        public void CanExecute_NoPredicate_ReturnsTrue()
        {
            var cmd = new RelayCommand(_ => { });

            Assert.True(cmd.CanExecute(null));
            Assert.True(cmd.CanExecute("x"));
        }

        [Fact]
        public void CanExecute_WithPredicate_PassesParameter()
        {
            var cmd = new RelayCommand(_ => { }, p => p is string s && s.Length > 0);

            Assert.False(cmd.CanExecute(null));
            Assert.False(cmd.CanExecute(""));
            Assert.True(cmd.CanExecute("hi"));
        }

        [Fact]
        public void RaiseCanExecuteChanged_FiresEvent()
        {
            var cmd = new RelayCommand(_ => { });
            int handled = 0;
            cmd.CanExecuteChanged += (s, e) => handled++;

            cmd.RaiseCanExecuteChanged();
            cmd.RaiseCanExecuteChanged();

            Assert.Equal(2, handled);
        }

        [Fact]
        public void RaiseCanExecuteChanged_ThrowingSubscriber_PropagatesAndSkipsLaterSubscribers()
        {
            var command = new RelayCommand(() => { });
            var reached = new List<string>();

            command.CanExecuteChanged += (s, e) =>
            {
                reached.Add("first");
                throw new InvalidOperationException("subscriber");
            };
            command.CanExecuteChanged += (s, e) => reached.Add("second");

            // Deliberately unlike Logging, Telemetry, Inspection and Diagnostics,
            // which isolate subscriber failures: a binding helper surfaces view
            // errors instead of swallowing them.
            Assert.Throws<InvalidOperationException>(() => command.RaiseCanExecuteChanged());
            Assert.Equal(new[] { "first" }, reached.ToArray());
        }

        [Fact]
        public void Execute_DelegateException_PropagatesToTheCaller()
        {
            var command = new RelayCommand(() => throw new NotSupportedException("boom"));

            Assert.Throws<NotSupportedException>(() => ((ICommand)command).Execute(null));
        }

        [Fact]
        public void RaiseCanExecuteChanged_IsReentrant()
        {
            RelayCommand command = null;
            var depth = 0;
            command = new RelayCommand(() => { });
            command.CanExecuteChanged += (s, e) =>
            {
                depth++;
                if (depth < 3) command.RaiseCanExecuteChanged();
            };

            command.RaiseCanExecuteChanged();

            // Cascading notifications are normal in view-models and are not guarded.
            Assert.Equal(3, depth);
        }

        [Fact]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action<object>)null));
            Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null));
        }
    }
}
