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
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action<object>)null));
            Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null));
        }
    }
}
