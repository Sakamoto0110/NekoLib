using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class AndGuard : IGuard
    {
        private readonly IGuard[] _guards;

        public AndGuard(params IGuard[] guards)
        {
            if (guards == null)
                throw new ArgumentNullException(nameof(guards));

            _guards = (IGuard[])guards.Clone();
            if (Array.Exists(_guards, guard => guard == null))
                throw new ArgumentException("A guard collection cannot contain null.", nameof(guards));
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
