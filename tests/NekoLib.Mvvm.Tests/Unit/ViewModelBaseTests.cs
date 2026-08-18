using System;
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
        public void SetProperty_NaNToNaN_IsSuppressedByDefaultEquality()
        {
            var vm = new Probe();
            var raised = 0;
            vm.PropertyChanged += (s, e) => raised++;

            vm.Rate = double.NaN;
            Assert.Equal(1, raised);

            // EqualityComparer<double>.Default follows Equals, not ==, so NaN
            // equals NaN and the second assignment raises nothing.
            vm.Rate = double.NaN;
            Assert.Equal(1, raised);
        }

        [Fact]
        public void SetProperty_SameReferenceAfterInPlaceMutation_DoesNotRaise()
        {
            var vm = new Probe();
            var raised = 0;
            vm.PropertyChanged += (s, e) => raised++;

            var items = new List<int>();
            vm.Items = items;
            Assert.Equal(1, raised);

            items.Add(1);
            vm.Items = items;

            // Reference equality: the classic "my grid did not refresh" case.
            Assert.Equal(1, raised);
        }

        [Fact]
        public void OnPropertyChanged_NullName_MeansEveryProperty()
        {
            var vm = new Probe();
            string captured = "unset";
            vm.PropertyChanged += (s, e) => captured = e.PropertyName;

            vm.Raise(null);

            Assert.Null(captured);
        }

        [Fact]
        public void OnPropertyChanged_ThrowingSubscriber_PropagatesOutOfTheSetter()
        {
            var vm = new Probe();
            vm.PropertyChanged += (s, e) => throw new InvalidOperationException("subscriber");

            Assert.Throws<InvalidOperationException>(() => vm.Rate = 1);
        }

        [Fact]
        public void OnPropertyChanged_IsVirtual_SoOneOverrideInterceptsEveryNotification()
        {
            var vm = new InterceptingProbe();
            var raised = 0;
            vm.PropertyChanged += (s, e) => raised++;

            vm.Rate = 1;
            vm.Raise("explicit");

            // SetProperty routes through OnPropertyChanged, so a single override
            // sees both the property setter and the direct call. This is the seam a
            // WinForms consumer uses to marshal notifications to the UI thread.
            Assert.Equal(new[] { "Rate", "explicit" }, vm.Intercepted.ToArray());
            Assert.Equal(2, raised);
        }

        private class Probe : ViewModelBase
        {
            private double _rate;
            private List<int> _items;

            public double Rate { get => _rate; set => SetProperty(ref _rate, value); }
            public List<int> Items { get => _items; set => SetProperty(ref _items, value); }

            public void Raise(string propertyName) => OnPropertyChanged(propertyName);
        }

        private sealed class InterceptingProbe : Probe
        {
            public List<string> Intercepted { get; } = new List<string>();

            protected override void OnPropertyChanged(string propertyName)
            {
                Intercepted.Add(propertyName);
                base.OnPropertyChanged(propertyName);
            }
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
