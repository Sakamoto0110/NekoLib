using System;

namespace NekoLib.Navigation.Contracts.Guards {

    public sealed class GuardContext
    {
        public Type TargetPage { get; }
         public IUserContext User { get; }

        public GuardContext(Type targetPage,
                             
                            IUserContext user)
        {
            TargetPage = targetPage;
             User = user;
        }
    }
}
 
