using System;
using NekoLib.Mvvm;
using Xunit;

namespace NekoLib.Mvvm.Tests.Unit
{
    /// <summary>
    /// The generic <see cref="RelayCommand{T}"/>'s value comes from its parameter
    /// coercion: a stale binding must never throw <see cref="InvalidCastException"/>
    /// through the command. These tests pin the matrix of (param type × T's nature).
    /// </summary>
    public class RelayCommandOfTTests
    {
        // -------------------------------------------------------------
        // Reference-type parameter (string)
        // -------------------------------------------------------------

        [Fact]
        public void RefType_DirectMatch_Executes()
        {
            string captured = null;
            var cmd = new RelayCommand<string>(s => captured = s);

            cmd.Execute("hello");

            Assert.Equal("hello", captured);
        }

        [Fact]
        public void RefType_NullParameter_PassesNull()
        {
            string captured = "untouched";
            var cmd = new RelayCommand<string>(s => captured = s);

            cmd.Execute(null);

            Assert.Null(captured);
        }

        [Fact]
        public void RefType_WrongType_IsNoOp_NoThrow()
        {
            int calls = 0;
            var cmd = new RelayCommand<string>(_ => calls++);

            cmd.Execute(42);            // int, not string

            Assert.Equal(0, calls);
            Assert.False(cmd.CanExecute(42));
        }

        // -------------------------------------------------------------
        // Value-type parameter (int)
        // -------------------------------------------------------------

        [Fact]
        public void ValueType_DirectMatch_Executes()
        {
            int captured = -1;
            var cmd = new RelayCommand<int>(i => captured = i);

            cmd.Execute(7);

            Assert.Equal(7, captured);
        }

        [Fact]
        public void ValueType_NullParameter_IsNoOp_NoThrow()
        {
            int calls = 0;
            var cmd = new RelayCommand<int>(_ => calls++);

            cmd.Execute(null);          // int can't be null

            Assert.Equal(0, calls);
            Assert.False(cmd.CanExecute(null));
        }

        // -------------------------------------------------------------
        // Nullable value-type parameter (int?)
        // -------------------------------------------------------------

        [Fact]
        public void NullableValueType_NullParameter_PassesNull()
        {
            int? captured = -1;
            var cmd = new RelayCommand<int?>(i => captured = i);

            cmd.Execute(null);

            Assert.Null(captured);
        }

        [Fact]
        public void NullableValueType_DirectMatch_Executes()
        {
            int? captured = null;
            var cmd = new RelayCommand<int?>(i => captured = i);

            cmd.Execute(5);

            Assert.Equal(5, captured);
        }

        // -------------------------------------------------------------
        // CanExecute / predicate
        // -------------------------------------------------------------

        [Fact]
        public void CanExecute_TypedPredicate_RunsAgainstCoercedValue()
        {
            var cmd = new RelayCommand<string>(_ => { }, s => !string.IsNullOrEmpty(s));

            Assert.False(cmd.CanExecute(""));
            Assert.False(cmd.CanExecute(null));
            Assert.True(cmd.CanExecute("hi"));
            Assert.False(cmd.CanExecute(123));      // wrong type -> false
        }

        [Fact]
        public void CanExecute_NoPredicate_TrueForValidParam_FalseForCoercionFailure()
        {
            var cmd = new RelayCommand<string>(_ => { });

            Assert.True(cmd.CanExecute("ok"));
            Assert.True(cmd.CanExecute(null));      // null OK for ref type
            Assert.False(cmd.CanExecute(7));         // wrong type
        }

        // -------------------------------------------------------------
        // Misc
        // -------------------------------------------------------------

        [Fact]
        public void RaiseCanExecuteChanged_FiresEvent()
        {
            var cmd = new RelayCommand<string>(_ => { });
            int handled = 0;
            cmd.CanExecuteChanged += (s, e) => handled++;

            cmd.RaiseCanExecuteChanged();

            Assert.Equal(1, handled);
        }

        [Fact]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null));
        }
    }
}
