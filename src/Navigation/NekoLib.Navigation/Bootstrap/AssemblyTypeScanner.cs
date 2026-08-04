using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>
    /// The one tolerant assembly scan the bootstrap uses. <c>Assembly.GetTypes()</c>
    /// throws <see cref="ReflectionTypeLoadException"/> when any single type fails to
    /// load — a missing optional dependency is enough — which would abort the whole
    /// scan. Recover the types that DID load instead (NEW-5).
    /// </summary>
    /// <remarks>
    /// NAV-008(b): both the page scan and the custom-loading-mask probe go through
    /// here. The probe used to call <c>GetTypes()</c> directly, so a partially
    /// loadable assembly aborted <c>Start()</c> at the first line, before the
    /// tolerance the rest of bootstrap advertises could ever apply.
    /// </remarks>
    internal static class AssemblyTypeScanner
    {
        internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Navigation] Failed to load some types from '{assembly.FullName}': {ex.Message}");
                return ex.Types.OfType<Type>();
            }
        }
    }
}
