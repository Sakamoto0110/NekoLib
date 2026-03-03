using System;

namespace NekoLib.Navigation.Runtime
{
    /// <summary>
    /// Flags applied per navigation call.
    /// 
    /// These are NOT page metadata.
    /// They control runtime execution behavior.
    /// </summary>
    [Flags]
    public enum NavigationOptions
    {
        None = 0,

        /// <summary>
        /// Do not record this navigation in history.
        /// </summary>
        NoHistory = 1,

        /// <summary>
        /// Do not show interaction mask during navigation.
        /// </summary>
        NoMask = 2
    }
}