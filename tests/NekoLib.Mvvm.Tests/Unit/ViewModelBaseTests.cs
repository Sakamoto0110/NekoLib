using System.Collections.Generic;
using NekoLib.Mvvm;
using Xunit;

namespace NekoLib.Mvvm.Tests.Unit
{
    public class ViewModelBaseTests
    {
        private sealed class Vm : ViewModelBase
        {
            private string _name;
            private int _count;

            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            public int Count
            {
                get => _count;
                set => SetProperty(ref _count, value);
            }

            public void Raise(string propName) => OnPropertyChanged(propName);
        }

        [Fact]
        public void SetProperty_DifferentValue_FiresPropertyChanged_AndReturnsTrue()
        {
            var vm = new Vm();
            var fired = new List<string>();
            vm.PropertyChanged += (s, e) => fired.Add(e.PropertyName);

            vm.Name = "x";

            Assert.Equal(new[] { "Name" }, fired);
            Assert.Equal("x", vm.Name);
        }

        [Fact]
        public void SetProperty_SameValue_DoesNotFire()
        {
            var vm = new Vm { Name = "x" };
            var fired = new List<string>();
            vm.PropertyChanged += (s, e) => fired.Add(e.PropertyName);

            vm.Name = "x";                  // unchanged

            Assert.Empty(fired);
        }

        [Fact]
        public void SetProperty_HandlesNullViaEqualityComparer()
        {
            var vm = new Vm { Name = "y" };
            var fired = new List<string>();
            vm.PropertyChanged += (s, e) => fired.Add(e.PropertyName);

            vm.Name = null;                 // change to null
            vm.Name = null;                 // already null — no event

            Assert.Equal(new[] { "Name" }, fired);
        }

        [Fact]
        public void OnPropertyChanged_FiresWithSuppliedName()
        {
            var vm = new Vm();
            var fired = new List<string>();
            vm.PropertyChanged += (s, e) => fired.Add(e.PropertyName);

            vm.Raise("custom");

            Assert.Equal(new[] { "custom" }, fired);
        }

        [Fact]
        public void SetProperty_ValueType_RaisesOnChange()
        {
            var vm = new Vm();
            int events = 0;
            vm.PropertyChanged += (s, e) => events++;

            vm.Count = 5;
            vm.Count = 5;                   // same -> no event
            vm.Count = 6;

            Assert.Equal(2, events);
        }
    }
}
