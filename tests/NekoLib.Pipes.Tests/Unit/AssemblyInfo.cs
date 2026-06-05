using Xunit;

// These are in-process IPC tests: each spins up a real named-pipe server/client.
// On net481 every blocking pipe op (WaitForConnection / Connect) occupies a
// thread-pool thread, so running them in parallel starves the pool and makes
// connects time out. Run them serially — they are fast and deterministic that way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
