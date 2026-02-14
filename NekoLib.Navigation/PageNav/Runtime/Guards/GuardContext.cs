using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {

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
 
