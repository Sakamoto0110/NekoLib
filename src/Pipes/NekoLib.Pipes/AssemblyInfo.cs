using System.Runtime.CompilerServices;

// Grants the unit-test assembly access to internal framing helpers
// (PipeFraming) so idle-timeout tests can hold a raw connection open and
// frame messages directly. No runtime effect.
[assembly: InternalsVisibleTo("NekoLib.Pipes.Tests.Unit")]
