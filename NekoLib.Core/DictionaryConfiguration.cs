using System;
using System.Collections.Generic;

namespace NekoLib.Core
{
    public sealed class DictionaryConfiguration : INekoConfiguration
    {
        private readonly IDictionary<string, object> _values;

        public DictionaryConfiguration(IDictionary<string, object> values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public T Get<T>(string key, T defaultValue = default(T))
        {
            if(key == null) return defaultValue;
            object v;
            if(!_values.TryGetValue(key, out v) || v == null)
                return defaultValue;

            if(v is T) return (T)v;

            try
            {
                return (T)Convert.ChangeType(v, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
