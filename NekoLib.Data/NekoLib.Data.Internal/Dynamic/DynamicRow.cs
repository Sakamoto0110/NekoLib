#nullable enable
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
namespace NekoLib.Data.Internal.Gateway.Dynamic
{
    /// <summary>
    /// Linha dinâmica, acessível por:
    /// <list type="bullet">
    /// <item>indexador: row["Nome"]</item>
    /// <item>membro dinâmico: row.Nome</item>
    /// </list>
    /// Suporta dois backends:
    /// <list type="bullet">
    /// <item>Instância com propriedades (DTO/IL)</item>
    /// <item>Dicionário (Expando/Dictionary)</item>
    /// </list>
    /// </summary>
    public sealed class DynamicRow : DynamicObject
    {
        private readonly object? _instance;
        private readonly IDictionary<string, object?>? _dict;
        private readonly Dictionary<string, PropertyInfo>? _props;

        public DynamicRow(object instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));

            PropertyInfo[] properties = instance
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            _props = new Dictionary<string, PropertyInfo>(properties.Length, StringComparer.OrdinalIgnoreCase);
            for(int i = 0; i < properties.Length; i++)
            {
                PropertyInfo p = properties[i];
                _props[p.Name] = p;
            }
        }

        public DynamicRow(IDictionary<string, object?> dict)
        {
            _dict = dict ?? throw new ArgumentNullException(nameof(dict));
        }

        public object? this[string name]
        {
            get => Get(name);
            set => Set(name, value);
        }

        private object? Get(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;

            if(_dict != null)
            {
                _dict.TryGetValue(name, out var v);
                return v;
            }

            if(_props != null)
            {
                if(_props.TryGetValue(name, out var pi))
                    return pi.GetValue(_instance, null);
            }

            return null;
        }

        private void Set(string name, object? value)
        {
            if(string.IsNullOrEmpty(name)) return;

            if(_dict != null)
            {
                _dict[name] = value;
                return;
            }

            if(_props != null && _props.TryGetValue(name, out var pi) && pi.CanWrite)
            {
                pi.SetValue(_instance, value, null);
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = Get(binder.Name);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            Set(binder.Name, value);
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
        {
            if(indexes != null && indexes.Length == 1 && indexes[0] is string s)
            {
                result = Get(s);
                return true;
            }

            result = null;
            return false;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value)
        {
            if(indexes != null && indexes.Length == 1 && indexes[0] is string s)
            {
                Set(s, value);
                return true;
            }

            return false;
        }
    }
}
