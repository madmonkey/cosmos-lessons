using System;
using System.Reflection;
using Serilog;

namespace DCI.SystemEvents.Extensions
{
    static class TypeExtensions
    {
        public static bool HasProperty(this Type obj, string propertyName)
        {
            // ignore case and replace additional default binding parameters
            return obj.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) != null;
        }
        public static bool TrySetProperty<TValue>(this object obj, string propertyName, TValue value)
        {
            var property = obj.GetType().GetProperty(propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                var propertyType = property.PropertyType;
                try
                {
                    //use the converter to get the correct value
                    property.SetValue(obj, Convert.ChangeType(value, propertyType), null);
                    return true;
                }
                catch (Exception ex)
                {
                    //in case of incorrect entry (string to number, etc) - use default values and continue
                    Log.Warning($"Unable to assign {propertyName} = {value} : {ex.Message}");
                }
            }
            return false;
        }
    }
}