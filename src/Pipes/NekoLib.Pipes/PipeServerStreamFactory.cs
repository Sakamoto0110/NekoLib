using System;
using System.IO.Pipes;

#if NET481
using System.Security.AccessControl;
using System.Security.Principal;
#endif

namespace NekoLib.Pipes
{
    internal static class PipeServerStreamFactory
    {
        public static NamedPipeServerStream Create(
            string pipeName,
            PipeDirection direction,
            PipeAccessPolicy accessPolicy)
        {
            Validate(accessPolicy);

#if NET9
            var options = PipeOptions.Asynchronous;
            if (accessPolicy == PipeAccessPolicy.CurrentUserOnly)
                options |= PipeOptions.CurrentUserOnly;

            return new NamedPipeServerStream(
                pipeName,
                direction,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                options);
#else
            if (accessPolicy == PipeAccessPolicy.CurrentUserOnly)
            {
                return new NamedPipeServerStream(
                    pipeName,
                    direction,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    CreateCurrentUserSecurity());
            }

            return new NamedPipeServerStream(
                pipeName,
                direction,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
#endif
        }

        internal static PipeOptions ResolveOptions(PipeAccessPolicy accessPolicy)
        {
            Validate(accessPolicy);

            var options = PipeOptions.Asynchronous;
#if NET9
            if (accessPolicy == PipeAccessPolicy.CurrentUserOnly)
                options |= PipeOptions.CurrentUserOnly;
#endif
            return options;
        }

        internal static void Validate(PipeAccessPolicy accessPolicy)
        {
            if (accessPolicy != PipeAccessPolicy.PlatformDefault &&
                accessPolicy != PipeAccessPolicy.CurrentUserOnly)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(accessPolicy),
                    accessPolicy,
                    "Unsupported pipe access policy.");
            }
        }

#if NET481
        private static PipeSecurity CreateCurrentUserSecurity()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var user = identity.User;
                if (user == null)
                    throw new InvalidOperationException("The current Windows user SID is unavailable.");

                var security = new PipeSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.SetOwner(user);
                security.AddAccessRule(new PipeAccessRule(
                    user,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                return security;
            }
        }
#endif
    }
}
