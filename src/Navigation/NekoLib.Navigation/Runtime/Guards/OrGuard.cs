using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    /// <summary>
    /// Evaluates guards in order until one allows navigation; when all deny, the
    /// last denial is returned. An empty collection denies navigation.
    /// </summary>
    public sealed class OrGuard : IGuard
    {
        private readonly IGuard[] _guards;

        /// <summary>Initializes the disjunction with a defensive copy of its guards.</summary>
        /// <param name="guards">Ordered guards; null elements are rejected.</param>
        public OrGuard(params IGuard[] guards)
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
            GuardResult? lastFailure = null;

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
