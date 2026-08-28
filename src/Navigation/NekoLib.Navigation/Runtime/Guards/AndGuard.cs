using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    /// <summary>
    /// Evaluates guards in order and returns the first denial or redirect; an
    /// empty collection allows navigation.
    /// </summary>
    public sealed class AndGuard : IGuard
    {
        private readonly IGuard[] _guards;

        /// <summary>Initializes the conjunction with a defensive copy of its guards.</summary>
        /// <param name="guards">Ordered guards; null elements are rejected.</param>
        public AndGuard(params IGuard[] guards)
        {
            if (guards == null)
                throw new ArgumentNullException(nameof(guards));

            _guards = (IGuard[])guards.Clone();
            if (Array.Exists(_guards, guard => guard == null))
                throw new ArgumentException("A guard collection cannot contain null.", nameof(guards));
        }

        /// <inheritdoc />
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
