#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// The suite's resource pass conditions, asserted from the samples the run
    /// actually took.
    /// <para/>
    /// Threads and handles are asserted, because they are discrete resources
    /// with clear ownership: a run that ends holding more of them than it held
    /// after warm-up has leaked something identifiable. Memory is reported as a
    /// trend and not asserted against a number, because the suite says not to
    /// invent a universal memory threshold before measurements establish a
    /// baseline, and this is the run that establishes one.
    /// <para/>
    /// What is asserted about memory is the shape rather than the size: a
    /// managed heap that rose at every single periodic sample, never once
    /// falling, is the signature of an accumulating structure, and that is
    /// checkable without claiming to know how many megabytes are acceptable.
    /// </summary>
    internal static class ResourceMatrix
    {
        private const string Phase = "resources";

        /// <summary>
        /// Threads and handles a healthy run may still gain between warm-up and
        /// the end. The pool grows under load and does not shrink promptly, so
        /// zero would be a false failure; these bounds are wide enough to
        /// absorb that and far below what a per-cycle leak would produce.
        /// </summary>
        private const int ThreadGrowthAllowance = 32;
        private const int HandleGrowthAllowance = 256;

        public static Task RunAsync(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "no-unexplained-resource-growth",
                "threads and handles return to their warm-up level, and the managed heap is not monotonic",
                check =>
                {
                    IReadOnlyList<ResourceSample> taken = context.Sampler.Taken;

                    ResourceSample? warmUp = Find(taken, "post-warm-up");
                    ResourceSample final = taken[taken.Count - 1];

                    if (warmUp == null)
                    {
                        check.That(false, "no post-warm-up sample was taken, so drift cannot be judged");
                        return PhaseContext.CompletedTask;
                    }

                    int threadGrowth = final.ThreadCount - warmUp.ThreadCount;
                    int handleGrowth = final.HandleCount - warmUp.HandleCount;

                    check.Note("threads " + warmUp.ThreadCount + " -> " + final.ThreadCount +
                               " (" + Signed(threadGrowth) + ")");
                    check.Note("handles " + warmUp.HandleCount + " -> " + final.HandleCount +
                               " (" + Signed(handleGrowth) + ")");
                    check.Note("private bytes " + Megabytes(warmUp.PrivateBytes) + " -> " +
                               Megabytes(final.PrivateBytes));
                    check.Note("managed heap " + Megabytes(warmUp.ManagedHeapBytes) + " -> " +
                               Megabytes(final.ManagedHeapBytes));

                    check.That(threadGrowth <= ThreadGrowthAllowance,
                        "threads grew by " + threadGrowth + " between warm-up and the end, above the allowance of " +
                        ThreadGrowthAllowance);

                    check.That(handleGrowth <= HandleGrowthAllowance,
                        "handles grew by " + handleGrowth + " between warm-up and the end, above the allowance of " +
                        HandleGrowthAllowance);

                    // Monotonicity across the periodic samples. One sample that
                    // falls is enough to say the heap is being reclaimed; a run
                    // where every single one rose is the shape worth reporting.
                    List<ResourceSample> periodic = Collect(taken, "periodic");
                    if (periodic.Count >= 4)
                    {
                        int rises = 0;
                        for (int i = 1; i < periodic.Count; i++)
                            if (periodic[i].ManagedHeapBytes > periodic[i - 1].ManagedHeapBytes) rises++;

                        int comparisons = periodic.Count - 1;
                        check.Note(rises + " of " + comparisons + " periodic samples showed a higher managed heap " +
                                   "than the one before");

                        check.That(rises < comparisons,
                            "the managed heap rose at every one of the " + comparisons +
                            " periodic comparisons and never fell, which is the shape of an accumulating structure");
                    }
                    else if (periodic.Count == 0)
                    {
                        // The rehearsal takes no periodic samples at all: it does
                        // not run the sustained cycle loop, so its samples are the
                        // pre-fault and post-recovery pairs. Saying "the run was
                        // too short" here, as an earlier version did, was simply
                        // untrue of a 53-minute rehearsal.
                        check.Note("no periodic samples: this mode does not run the sustained cycle loop, so the " +
                                   "heap trend is judged by the smoke and the soak rather than here");
                    }
                    else
                    {
                        check.Note("only " + periodic.Count + " periodic sample(s), too few to judge a trend; " +
                                   "a sustained run produces one per cycle");
                    }

                    // The bounded structures have to be at or under their
                    // configured capacity at the end, whatever happened during.
                    check.That(context.Workspace.Logger.GetRecentEntries(int.MaxValue).Count
                               <= ObservabilityWorkspace.LogRecentCapacity,
                        "the log snapshot ended above its configured capacity");

                    check.That(context.Workspace.Telemetry.GetRecentOperations(int.MaxValue).Count
                               <= ObservabilityWorkspace.TelemetryCapacity,
                        "telemetry retention ended above its configured capacity");

                    check.That(context.Workspace.Inspection.GetDiagnostics().RetainedCount
                               <= ObservabilityWorkspace.InspectionCapacity,
                        "inspection retention ended above its configured capacity");

                    check.Note("bounded structures at the end: log snapshot " +
                               context.Workspace.Logger.GetRecentEntries(int.MaxValue).Count + "/" +
                               ObservabilityWorkspace.LogRecentCapacity + ", telemetry " +
                               context.Workspace.Telemetry.GetRecentOperations(int.MaxValue).Count + "/" +
                               ObservabilityWorkspace.TelemetryCapacity + ", inspection " +
                               context.Workspace.Inspection.GetDiagnostics().RetainedCount + "/" +
                               ObservabilityWorkspace.InspectionCapacity);

                    // Provider registrations must be back where they started.
                    check.Equal(0, context.Workspace.Inspection.GetDiagnostics().ProviderCount,
                        "state providers still registered on the shared runtime at the end");

                    return PhaseContext.CompletedTask;
                });
        }

        /// <summary>
        /// The sustained phase. Smoke and soak differ only in how long they run
        /// it for, so they share it: a smoke that just executed the matrices
        /// once would prove the assertions and nothing about behaviour over
        /// time, which is what the specified 15-to-30-minute window is for.
        /// </summary>
        public static async Task SustainAsync(
            PhaseContext context,
            DateTime deadline,
            Func<Task> cycle,
            string phaseName)
        {
            int cycles = 0;

            while (DateTime.UtcNow < deadline && !context.Ct.IsCancellationRequested)
            {
                cycles++;
                context.Artifacts.Out(phaseName + " cycle " + cycles.ToString(CultureInfo.InvariantCulture) +
                                      "  " + Remaining(deadline) + " remaining");

                // The sample is taken inside the gate, not after it. Reading the
                // shared logger's snapshot takes the same lock a flush holds, so
                // a sample racing the blocked-flush fault would stall for that
                // fault's budget and record a stalled process rather than a
                // working one.
                await context.ExclusiveAsync(async () =>
                {
                    await cycle().ConfigureAwait(false);
                    context.Sampler.Take(phaseName, "periodic");
                }).ConfigureAwait(false);
            }

            context.Artifacts.Out(phaseName + " completed " + cycles + " cycle(s)");
        }

        private static string Remaining(DateTime deadline)
        {
            TimeSpan left = deadline - DateTime.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;

            return left.TotalMinutes >= 1
                ? ((int)left.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m"
                : ((int)left.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
        }

        private static ResourceSample? Find(IReadOnlyList<ResourceSample> taken, string marker)
        {
            foreach (ResourceSample sample in taken)
                if (string.Equals(sample.Marker, marker, StringComparison.Ordinal)) return sample;

            return null;
        }

        private static List<ResourceSample> Collect(IReadOnlyList<ResourceSample> taken, string marker)
        {
            List<ResourceSample> found = new List<ResourceSample>();
            foreach (ResourceSample sample in taken)
                if (string.Equals(sample.Marker, marker, StringComparison.Ordinal)) found.Add(sample);

            return found;
        }

        private static string Signed(int value) =>
            (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);

        private static string Megabytes(long bytes) =>
            (bytes / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture) + " MiB";
    }
}
