using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Guards
{
    public interface IGuard
    {
        Task<GuardResult> EvaluateAsync(GuardContext context);
    }
}

