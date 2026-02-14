using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class OrGuard : IGuard
    {
        private readonly IGuard[] _guards;

        public OrGuard(params IGuard[] guards)
        {
            _guards = guards ?? Array.Empty<IGuard>();
        }

        public async Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            GuardResult lastFailure = null;

            foreach (var g in _guards)
            {
                var result = await g.EvaluateAsync(context);

                if (result.Allowed)
                    return result;

                lastFailure = result;
            }

            return lastFailure ?? GuardResult.Deny();
        }
    }



}
