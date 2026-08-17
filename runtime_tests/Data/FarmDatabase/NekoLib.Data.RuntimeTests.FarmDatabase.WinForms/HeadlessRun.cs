using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    /// <summary>
    /// Runs the simulation with no window, and prints a state digest at the end.
    /// <para/>
    /// This is not a debugging convenience. A run of thirty thousand ticks cannot be
    /// drawn frame by frame, and the comparison the scenario exists to make - the same
    /// seed against both engines, checked for divergence - is only possible without a
    /// UI in the way. The digest is what makes that comparison one value instead of a
    /// table-by-table diff.
    /// </summary>
    internal static class HeadlessRun
    {
        /// <summary>
        /// <c>--headless &lt;sqlite|access&gt; &lt;seed&gt; &lt;ticks&gt; [--checkpoint n] [--resume]</c>
        /// </summary>
        internal static int Execute(string[] args)
        {
            try
            {
                return RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FALHOU: " + ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine("  <- " + ex.InnerException.Message);
                return 2;
            }
        }

        private static async Task<int> RunAsync(string[] args)
        {
            string engine = Arg(args, 1, "sqlite").ToLowerInvariant();
            int seed = int.Parse(Arg(args, 2, "7"), CultureInfo.InvariantCulture);
            int ticks = int.Parse(Arg(args, 3, "1000"), CultureInfo.InvariantCulture);
            int checkpoint = IntOption(args, "--checkpoint", 0);
            bool resume = HasFlag(args, "--resume");

            using (var workspace = new FarmWorkspace())
            {
                FarmProvider provider = engine == "access"
                    ? FarmProvider.Access
                    : FarmProvider.Sqlite;

                // A resumed run must not recreate the file - that is the whole point of
                // the restart check.
                await workspace.ConnectAsync(provider, recreate: !resume).ConfigureAwait(false);
                FarmDb db = workspace.Require();

                // A builder probe needs the seeded schema and nothing else, so it runs
                // before any simulation state exists and returns straight away.
                if (HasFlag(args, "--cancel"))
                {
                    Console.WriteLine();
                    Console.WriteLine("Cancelamento em " + engine +
                                      "  (fallback sincrono: " + db.FallbackModeName + ")");
                    Console.WriteLine();

                    List<CancellationResult> probes =
                        await db.CancellationProbeAsync().ConfigureAwait(false);

                    int ignored = 0;
                    foreach (CancellationResult probe in probes)
                    {
                        if (!probe.Honoured) ignored++;

                        Console.WriteLine(
                            (probe.Honoured ? "  " : "! ") +
                            probe.Name.PadRight(20) + probe.Detail);
                    }

                    Console.WriteLine();
                    Console.WriteLine(ignored == 0
                        ? "OK   " + probes.Count + " chamada(s), todas recusaram o token cancelado"
                        : "IGNORADO  " + ignored + " de " + probes.Count +
                          " rodaram com o token ja cancelado");

                    return ignored == 0 ? 0 : 10;
                }

                if (HasFlag(args, "--reads"))
                {
                    Console.WriteLine();
                    Console.WriteLine("Formas de leitura em " + engine);
                    Console.WriteLine();

                    List<ReadShapeResult> shapes =
                        await db.ReadShapesAsync().ConfigureAwait(false);

                    // Every shape reads the same query. Agreement is the assertion; the
                    // expected count is whatever the majority of them found, so this
                    // does not depend on knowing the fixture.
                    int expected = Majority(shapes);
                    int disagreed = 0;

                    foreach (ReadShapeResult shape in shapes)
                    {
                        bool exempt = shape.Name.StartsWith("ContainsData", StringComparison.Ordinal);
                        bool ok = exempt || shape.Rows == expected;
                        if (!ok) disagreed++;

                        Console.WriteLine(
                            (ok ? "  " : "! ") + shape.Name.PadRight(22) +
                            shape.Rows.ToString(CultureInfo.InvariantCulture).PadLeft(4) +
                            (shape.Note.Length > 0 ? "   " + shape.Note : string.Empty));
                    }

                    Console.WriteLine();
                    Console.WriteLine(disagreed == 0
                        ? "OK   " + shapes.Count + " formas, todas em " + expected + " linha(s)"
                        : "DIVERGIU  " + disagreed + " forma(s) discordam de " + expected);

                    return disagreed == 0 ? 0 : 9;
                }

                if (HasFlag(args, "--builder"))
                {
                    Console.WriteLine();
                    Console.WriteLine("QueryBuilder em " + engine);
                    Console.WriteLine();
                    return await BuilderProbe.RunAsync(db, workspace).ConfigureAwait(false);
                }

                SimSnapshot snapshot;
                if (resume)
                {
                    SimSnapshot loaded = await db.LoadSimAsync().ConfigureAwait(false);
                    if (loaded == null)
                    {
                        Console.Error.WriteLine("Nenhum run salvo para retomar.");
                        return 3;
                    }

                    snapshot = loaded;
                    Console.WriteLine("retomado  tick=" + snapshot.State.Tick +
                                      "  seed=" + snapshot.State.Seed);
                }
                else
                {
                    snapshot = await db.StartRunAsync(seed).ConfigureAwait(false);
                    Console.WriteLine("iniciado  seed=" + seed + "  engine=" + engine);
                }

                DateTime started = DateTime.UtcNow;
                long statementsBefore = workspace.StatementCount;

                // The audit trail is verified by delta, not by absolute count, so a
                // resumed run compares against what it found rather than against zero.
                long auditBefore = await db.CountOperationLogAsync().ConfigureAwait(false);
                long expectedEvents = 0;
                bool skipChecks = HasFlag(args, "--no-check");

                int faultAt = IntOption(args, "--fault", 0);

                for (int i = 0; i < ticks; i++)
                {
                    // The fault test ends the run: after a rollback the snapshot in
                    // memory has advanced and the database has not, so continuing would
                    // compare two worlds that are legitimately different.
                    if (faultAt > 0 && snapshot.State.Tick + 1 == faultAt)
                        return await RunFaultAsync(db, snapshot).ConfigureAwait(false);

                    TickOutcome outcome = FarmSimulation.Advance(snapshot);
                    await db.SaveTickAsync(snapshot, outcome).ConfigureAwait(false);
                    expectedEvents += outcome.Events.Count;

                    if (!skipChecks)
                    {
                        IReadOnlyList<SimViolation> broken = SimInvariants.Check(snapshot);
                        if (broken.Count > 0)
                        {
                            Console.Error.WriteLine();
                            Console.Error.WriteLine(
                                "INVARIANTE QUEBRADA no tick " + snapshot.State.Tick);
                            foreach (SimViolation violation in broken)
                                Console.Error.WriteLine("  " + violation);

                            return 4;
                        }

                        if (SimInvariants.EconomyIsDead(snapshot))
                        {
                            Console.Error.WriteLine();
                            Console.Error.WriteLine(
                                "ECONOMIA MORTA no tick " + snapshot.State.Tick +
                                " (mes " + (snapshot.State.Month + 1) + ")");
                            Console.Error.WriteLine(
                                "  toda cultura no piso de preco; o resto do run nao mede nada");

                            return 5;
                        }
                    }

                    if (checkpoint > 0 && snapshot.State.Tick % checkpoint == 0)
                    {
                        Console.WriteLine(
                            "checkpoint  tick=" + snapshot.State.Tick.ToString("D6") +
                            "  " + Digest(snapshot));

                        if (!skipChecks)
                        {
                            int audit = await CheckAuditAsync(db, auditBefore, expectedEvents)
                                .ConfigureAwait(false);
                            if (audit != 0) return audit;
                        }
                    }
                }

                double seconds = (DateTime.UtcNow - started).TotalSeconds;

                // Read the world back from the database rather than reporting the copy
                // in memory. Anything that never reached disk has to show up here.
                SimSnapshot reloaded = await db.LoadSimAsync().ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("engine     " + engine);
                Console.WriteLine("seed       " + snapshot.State.Seed);
                long statements = workspace.StatementCount - statementsBefore;

                Console.WriteLine("ticks      " + ticks + " em " + seconds.ToString("F1", CultureInfo.InvariantCulture) + "s" +
                                  "  (" + (ticks / Math.Max(0.001, seconds)).ToString("F0", CultureInfo.InvariantCulture) + " tick/s)");

                // One transaction per tick here, so this is directly comparable with the
                // sql/tick the UI reports at real time.
                Console.WriteLine("sql        " + statements.ToString("N0", CultureInfo.InvariantCulture) +
                                  " statements  (" +
                                  ((double)statements / Math.Max(1, ticks)).ToString("F1", CultureInfo.InvariantCulture) +
                                  " por tick)");
                Console.WriteLine("memoria    " + Digest(snapshot));
                Console.WriteLine("banco      " + Digest(reloaded));
                Console.WriteLine();
                Console.WriteLine(Describe(reloaded));

                if (!skipChecks)
                {
                    int audit = await CheckAuditAsync(db, auditBefore, expectedEvents)
                        .ConfigureAwait(false);
                    if (audit != 0) return audit;

                    Console.WriteLine("auditoria  " + expectedEvents.ToString("N0", CultureInfo.InvariantCulture) +
                                      " evento(s), mesma quantidade de linhas no OperationLog");
                }

                if (HasFlag(args, "--stream"))
                {
                    int streamed = await CheckStreamAsync(db).ConfigureAwait(false);
                    if (streamed != 0) return streamed;
                }

                bool match = Digest(snapshot) == Digest(reloaded);
                Console.WriteLine(match
                    ? "OK   memoria e banco concordam" +
                      (skipChecks ? "  (invariantes desligadas)" : ", invariantes de pe")
                    : "DIVERGIU  memoria e banco discordam");

                return match ? 0 : 1;
            }
        }

        /// <summary>
        /// Reads the operation log by streaming and checks it agrees with the count.
        /// <para/>
        /// This is the only part of the module that differs by target framework, and
        /// the difference is deliberate: the streaming gateway exists only on net6 and
        /// later and is absent from the net481 assembly. So the two builds are
        /// expected to disagree here, and the net481 branch reporting its own absence
        /// is the correct result rather than a gap.
        /// </summary>
        private static async Task<int> CheckStreamAsync(FarmDb db)
        {
            long counted = await db.CountOperationLogAsync().ConfigureAwait(false);

#if NET6_0_OR_GREATER
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);

            int streamed = await db.StreamOperationLogAsync(entry =>
            {
                string kind = entry.EntityKind ?? "?";
                kinds.TryGetValue(kind, out int seen);
                kinds[kind] = seen + 1;
            }).ConfigureAwait(false);

            Console.WriteLine("streaming  " + streamed + " linha(s) lidas uma a uma, COUNT(*) diz " + counted);

            var breakdown = new List<string>();
            foreach (KeyValuePair<string, int> pair in kinds)
                breakdown.Add(pair.Key + "=" + pair.Value);

            breakdown.Sort(StringComparer.Ordinal);
            Console.WriteLine("           " + string.Join(" ", breakdown.ToArray()));

            if (streamed != counted)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("STREAMING NAO BATE  leu " + streamed + ", COUNT(*) diz " + counted);
                return 8;
            }

            return 0;
#else
            // Not a shortcoming: on this target the streaming members are not on
            // IDatabaseGateway at all, and referencing them would not compile.
            Console.WriteLine("streaming  indisponivel neste target (net481); COUNT(*) diz " + counted);
            return 0;
#endif
        }

        /// <summary>
        /// Forces one transaction to fail halfway and proves the database was left
        /// untouched.
        /// <para/>
        /// This is the scenario's central claim and the one path nothing had ever
        /// exercised: every write is wrapped in <c>catch { Rollback(); throw; }</c> and
        /// no run had ever made one throw.
        /// <para/>
        /// The failure is a real constraint violation rather than a fabricated
        /// exception — <c>OperationLog.EntityName</c> is <c>NOT NULL</c> in both
        /// dialects, so an event with no name is rejected by the engine itself. That
        /// matters because rollback is the provider's implementation, not the library's,
        /// and a synthetic throw would never reach it.
        /// <para/>
        /// The state row is written before the audit rows, so a rollback that does not
        /// work leaves a tick committed with no audit line beside it — precisely the
        /// half-landed transaction the design exists to rule out.
        /// </summary>
        private static async Task<int> RunFaultAsync(FarmDb db, SimSnapshot snapshot)
        {
            long tick = snapshot.State.Tick;

            SimSnapshot before = await db.LoadSimAsync().ConfigureAwait(false);
            if (before == null)
            {
                Console.Error.WriteLine("nada gravado para comparar.");
                return 7;
            }

            string expected = Digest(before);
            long auditExpected = await db.CountOperationLogAsync().ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("falha injetada no tick " + (tick + 1));
            Console.WriteLine("  antes     " + expected + "  auditoria=" + auditExpected);

            TickOutcome outcome = FarmSimulation.Advance(snapshot);

            // No name: the engine's own NOT NULL rejects this, mid-transaction, after
            // the state row has already been written.
            outcome.Events.Add(new SimEvent(SimEntityKinds.Market, null, 1, "falha injetada"));

            bool threw = false;
            try
            {
                await db.SaveTickAsync(snapshot, outcome).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                threw = true;
                Console.WriteLine("  recusado " + Flatten(ex.Message));
            }

            if (!threw)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("SEM FALHA  o banco aceitou uma linha de auditoria sem nome");
                return 7;
            }

            // Judge by the disk alone. Memory advanced and the database did not, so
            // they are supposed to disagree now - comparing them would be the wrong
            // question.
            SimSnapshot after = await db.LoadSimAsync().ConfigureAwait(false);
            string actual = Digest(after);
            long auditActual = await db.CountOperationLogAsync().ConfigureAwait(false);

            Console.WriteLine("  depois    " + actual + "  auditoria=" + auditActual);
            Console.WriteLine();

            if (actual != expected)
            {
                Console.Error.WriteLine("ROLLBACK FALHOU  o estado mudou apesar da transacao ter falhado");
                Console.Error.WriteLine("  esperado " + expected);
                Console.Error.WriteLine("  obtido   " + actual);
                return 7;
            }

            if (auditActual != auditExpected)
            {
                Console.Error.WriteLine("ROLLBACK FALHOU  a trilha de auditoria mudou");
                Console.Error.WriteLine("  esperado " + auditExpected + ", obtido " + auditActual);
                return 7;
            }

            Console.WriteLine("OK   transacao falhou inteira: nem o estado nem a auditoria mudaram");
            return 0;
        }

        /// <summary>
        /// The row count most shapes agreed on. Taking the majority rather than a
        /// hard-coded number keeps the check independent of the fixture — and means a
        /// single misbehaving shape stands out instead of moving the target.
        /// </summary>
        private static int Majority(List<ReadShapeResult> shapes)
        {
            var tally = new Dictionary<int, int>();

            foreach (ReadShapeResult shape in shapes)
            {
                if (shape.Name.StartsWith("ContainsData", StringComparison.Ordinal))
                    continue;

                tally.TryGetValue(shape.Rows, out int seen);
                tally[shape.Rows] = seen + 1;
            }

            int best = 0;
            int bestCount = -1;

            foreach (KeyValuePair<int, int> pair in tally)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    best = pair.Key;
                }
            }

            return best;
        }

        private static string Flatten(string text) =>
            text == null ? string.Empty : text.Replace("\r", " ").Replace("\n", " ").Trim();

        /// <summary>
        /// Verifies that the audit trail grew by exactly the number of events emitted.
        /// <para/>
        /// Fewer rows than events means a change committed without its audit row, which
        /// is the transaction landing by halves. More rows means something wrote to the
        /// log outside the simulation, which in a headless run means the accounting
        /// here is wrong.
        /// </summary>
        private static async Task<int> CheckAuditAsync(FarmDb db, long before, long expected)
        {
            long now = await db.CountOperationLogAsync().ConfigureAwait(false);
            long actual = now - before;

            if (actual == expected)
                return 0;

            Console.Error.WriteLine();
            Console.Error.WriteLine("TRILHA DE AUDITORIA NAO BATE");
            Console.Error.WriteLine("  eventos emitidos : " + expected);
            Console.Error.WriteLine("  linhas gravadas  : " + actual);
            Console.Error.WriteLine(actual < expected
                ? "  faltam linhas: uma mudanca comitou sem a sua auditoria"
                : "  sobram linhas: algo escreveu no log fora da simulacao");

            return 6;
        }

        /// <summary>
        /// A canonical fingerprint of the world. Two runs of the same seed on different
        /// engines must produce the same string; when they do not, this is the value
        /// that says so without a row-by-row comparison.
        /// </summary>
        private static string Digest(SimSnapshot snapshot)
        {
            if (snapshot == null) return "(vazio)";

            var text = new StringBuilder();
            text.Append(snapshot.State.Tick).Append('|')
                .Append(Math.Round(snapshot.State.Gold, 2).ToString("F2", CultureInfo.InvariantCulture)).Append('|')
                .Append(snapshot.State.Terrains).Append('|')
                .Append(snapshot.State.Slots).Append('|')
                .Append(snapshot.State.Workers).Append('|');

            foreach (SimMarketRow row in Sorted(snapshot.Market))
                text.Append(row.Crop).Append(':')
                    .Append(Math.Round(row.Quantity, 2).ToString("F2", CultureInfo.InvariantCulture)).Append(',');

            unchecked
            {
                int hash = 17;
                foreach (char c in text.ToString())
                    hash = (hash * 31) + c;

                return "h" + hash.ToString("X8") + " t" + snapshot.State.Tick +
                       " g" + Math.Round(snapshot.State.Gold).ToString(CultureInfo.InvariantCulture);
            }
        }

        private static List<SimMarketRow> Sorted(List<SimMarketRow> rows)
        {
            var copy = new List<SimMarketRow>(rows);
            copy.Sort((a, b) => string.CompareOrdinal(a.Crop, b.Crop));
            return copy;
        }

        private static string Describe(SimSnapshot snapshot)
        {
            if (snapshot == null) return "(sem estado)";

            var text = new StringBuilder();
            text.AppendLine("mes " + (snapshot.State.Month + 1) +
                            "  ciclo " + snapshot.State.Prime +
                            "  ouro " + Math.Round(snapshot.State.Gold).ToString(CultureInfo.InvariantCulture) +
                            "  slots " + snapshot.State.Slots +
                            "  workers " + snapshot.State.Workers +
                            "  terrenos " + snapshot.State.Terrains);

            text.AppendLine("plantando: " + string.Join(", ", new List<string>(snapshot.ChosenCrops()).ToArray()));

            foreach (Crop crop in SimRules.Crops)
            {
                SimMarketRow row = snapshot.MarketFor(crop.Name);
                text.AppendLine(
                    "  " + crop.Name.PadRight(9) +
                    " preco " + row.Price.ToString("F2", CultureInfo.InvariantCulture).PadLeft(7) +
                    "  (base " + crop.BasePrice.ToString("F0", CultureInfo.InvariantCulture).PadLeft(3) + ")" +
                    "  mercado " + Math.Round(row.Quantity).ToString(CultureInfo.InvariantCulture).PadLeft(6));
            }

            return text.ToString();
        }

        private static string Arg(string[] args, int index, string fallback) =>
            index < args.Length && !args[index].StartsWith("--") ? args[index] : fallback;

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private static int IntOption(string[] args, string name, int fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return int.Parse(args[i + 1], CultureInfo.InvariantCulture);

            return fallback;
        }
    }
}
