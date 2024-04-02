using billpg.HashBackCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCore_Tests
{
    internal static class TestCommon
    {
        /// <summary>
        /// Create a function that returns a mock "now", starting at five billion and
        /// going up by a kilosecond each subsequent call.
        /// </summary>
        /// <returns>Callable "now" function.</returns>
        internal static InternalTools.OnNowFn StartClock()
        {
            /* Initial value for clock. Use first round value after 32 bit limit. */
            long clock = 5L * 1000 * 1000 * 1000;

            /* Return a function that reads the clock and updates the value. */
            return () => clock += 1000;
        }

        internal static TypeInfo GetTypeInfo(this Type ty)   
            => ty.Assembly.DefinedTypes.Single(tyInDll => tyInDll.FullName == ty.FullName);

        internal static MethodInfo GetFunctionByName(this TypeInfo tyi, string functionName)
            => tyi.DeclaredMethods.Single(fn => fn.Name == functionName);

        internal static object? Invoke(object obj, string functionName, params object?[]? invokePars)
            => obj.GetType().GetTypeInfo().GetFunctionByName(functionName).Invoke(obj, invokePars);

        internal static object? GetPublicPropertyValue(object obj, string propertyName)
            => obj.GetType().GetProperties().Single(prop => prop.Name == propertyName).GetValue(obj);        
    }
}
