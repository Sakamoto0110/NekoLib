using System;

namespace NekoLib.Navigation.Contracts.Guards {

    /// <summary>
    /// Data passed to guards for a single navigation attempt.
    /// </summary>
    public sealed class GuardContext
    {
        /// <summary>Page type the runtime is attempting to navigate to.</summary>
        public Type TargetPage { get; }

        /// <summary>Current user/session state used by built-in and custom guards.</summary>
         public IUserContext User { get; }

        public GuardContext(Type targetPage,
                             
                            IUserContext user)
        {
            TargetPage = targetPage;
             User = user;
        }
    }
}
 
