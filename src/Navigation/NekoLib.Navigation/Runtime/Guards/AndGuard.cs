using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class AndGuard : IGuard
    {
        private readonly IGuard[] _guards;

        public AndGuard(params IGuard[] guards)
        {
            _guards = guards?
            .Where(g => g != null)
            .ToArray()
            ?? Array.Empty<IGuard>();
        }

        public async Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            foreach (var g in _guards)
            {
                var result = await g.EvaluateAsync(context).ConfigureAwait(false);

                if (!result.Allowed)
                    return result;
            }
            
            return GuardResult.Allow();
        }
    }


}
