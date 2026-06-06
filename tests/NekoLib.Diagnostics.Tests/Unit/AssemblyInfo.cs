using Xunit;

// CrashHandler keeps process-global static state (InstalledHandlers,
// _globalHandlersInstalled, the AppDomain/TaskScheduler hooks). Run serially so
// tests that exercise Install()/global crash dispatch cannot interfere with each
// other on a shared AppDomain.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
