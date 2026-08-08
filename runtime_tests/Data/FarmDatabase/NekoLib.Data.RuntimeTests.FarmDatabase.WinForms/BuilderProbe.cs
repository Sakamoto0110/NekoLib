using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    /// <summary>
    /// Runs every <see cref="QueryBuilder"/> clause the scenario never used, and prints
    /// what each one became on this engine.
    /// <para/>
    /// The scenario writes all its interesting SQL by hand, so the builder — which is
    /// the piece that actually translates per dialect — had no coverage. That gap
    /// matters most exactly where the two engines disagree: <c>LIMIT</c> against
    /// <c>TOP</c>, and where <c>DISTINCT</c> and the row cap sit relative to each
    /// other. The module's history records a fix for Access <c>DISTINCT TOP</c> output
    /// that had never been exercised here.
    /// <para/>
    /// Each case prints the dispatched statement and a digest of the rows. The
    /// statements are expected to differ between engines; <b>the digests are not</b>.
    /// That is the whole assertion: same builder, different SQL, same answer.
    /// </summary>
    internal static class BuilderProbe
    {
        private sealed class Case
        {
            public Case(string name, QueryBuilder builder)
            {
                Name = name;
                Builder = builder;
            }

            public string Name { get; }
            public QueryBuilder Builder { get; }
        }

        internal static async Task<int> RunAsync(FarmDb db, FarmWorkspace workspace)
        {
            foreach (Case probe in Build())
            {
                workspace.ClearTrace();

                string outcome;
                string digest;

                try
                {
                    List<Dictionary<string, RecordItem>> rows =
                        await db.QueryAsync(probe.Builder).ConfigureAwait(false);

                    outcome = rows.Count + " linha(s)";
                    digest = Digest(rows);

                    // A single scalar is printed outright. The digest folds in column
                    // names, and the two engines invent different ones for an unaliased
                    // aggregate — so for the count cases the digest cannot answer the
                    // only question that matters, which is whether the number agrees.
                    if (rows.Count == 1 && rows[0].Count == 1)
                    {
                        foreach (KeyValuePair<string, RecordItem> only in rows[0])
                            digest = "valor=" + only.Value.Value + "   " + digest;
                    }
                }
                catch (Exception ex)
                {
                    // A clause the engine refuses is a result, not a crash: it is the
                    // divergence the probe exists to surface.
                    outcome = "RECUSADO";
                    digest = Flatten(ex.Message);
                }

                Console.WriteLine(probe.Name.PadRight(16) + outcome.PadRight(14) + digest);

                string sql = LastDispatched(workspace);
                if (sql != null)
                    Console.WriteLine("                " + sql);

                Console.WriteLine();
            }

            return 0;
        }

        private static IEnumerable<Case> Build()
        {
            yield return new Case("top",
                new QueryBuilder().Select("*").From("[Products]").Top(3));

            yield return new Case("order+top",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .OrderBy("[Name]").Top(3));

            yield return new Case("distinct",
                new QueryBuilder().SelectDistinct("[Category]").From("[Products]"));

            // The case the module's history says was fixed for Access and that nothing
            // here had ever run.
            yield return new Case("distinct+top",
                new QueryBuilder().SelectDistinct("[Category]").From("[Products]").Top(2));

            yield return new Case("count",
                new QueryBuilder().Count().From("[Products]"));

            yield return new Case("count(col)",
                new QueryBuilder().Count("[Category]").From("[Products]"));

            yield return new Case("distinctcount",
                new QueryBuilder().DistinctCount("[Category]").From("[Products]"));

            yield return new Case("join",
                new QueryBuilder().Select("e.[Name]", "r.[Title]")
                    .From("[Employees] e")
                    .Join("[Roles] r", "e.[RoleId] = r.[Id]"));

            yield return new Case("groupby",
                new QueryBuilder().Select("[Category]").From("[Products]")
                    .GroupBy("[Category]"));

            yield return new Case("wherein",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereIn("[Category]", new object[] { "Fruta", "Legume" }));

            yield return new Case("wherenotin",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereNotIn("[Category]", new object[] { "Fruta", "Legume" }));

            yield return new Case("wherebetween",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereBetween("[Quantity]", 50, 100));

            yield return new Case("wherelike",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereLike("[Name]", "%an%"));

            // Subquery parameters start numbering at @p1 in both builders, so the
            // parent has to rename them on the way in. That renaming was read in the
            // source and never executed.
            yield return new Case("whereexists",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .Where("[Quantity] > @p1", 40)
                    .WhereExists(new QueryBuilder().Select("[Id]").From("[Animals]")
                        .Where("[Species] = @p1", "Vaca")));

            // Same query, clauses swapped, so the subquery's parameter comes first in
            // the text. If the engine binds by textual position this changes nothing;
            // if it evaluates the subquery first and consumes placeholders in that
            // order, only one of the two orderings can work.
            yield return new Case("exists-antes",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereExists(new QueryBuilder().Select("[Id]").From("[Animals]")
                        .Where("[Species] = @p1", "Vaca"))
                    .Where("[Quantity] > @p1", 40));

            // Two parameters outside the subquery instead of one. If the engine simply
            // takes the subquery's placeholder first and then the rest in text order,
            // the second form works and the first does not.
            yield return new Case("dois-fora",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .Where("[Quantity] > @p1", 40)
                    .Where("[Category] = @p1", "Legume")
                    .WhereExists(new QueryBuilder().Select("[Id]").From("[Animals]")
                        .Where("[Species] = @p1", "Vaca")));

            yield return new Case("dois-fora-rev",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereExists(new QueryBuilder().Select("[Id]").From("[Animals]")
                        .Where("[Species] = @p1", "Vaca"))
                    .Where("[Quantity] > @p1", 40)
                    .Where("[Category] = @p1", "Legume"));

            yield return new Case("wherenotexists",
                new QueryBuilder().Select("[Name]").From("[Products]")
                    .WhereNotExists(new QueryBuilder().Select("[Id]").From("[Animals]")
                        .Where("[Species] = @p1", "Dragao")));
        }

        /// <summary>
        /// Order-insensitive fingerprint of a result set. None of these queries carry a
        /// total ordering, so two engines are free to return the same rows in different
        /// order — sorting first is what keeps that from reading as divergence.
        /// </summary>
        private static string Digest(List<Dictionary<string, RecordItem>> rows)
        {
            var lines = new List<string>(rows.Count);

            foreach (Dictionary<string, RecordItem> row in rows)
            {
                var text = new StringBuilder();
                foreach (KeyValuePair<string, RecordItem> cell in row)
                    text.Append(cell.Key).Append('=').Append(cell.Value.Value).Append(';');

                lines.Add(text.ToString());
            }

            lines.Sort(StringComparer.Ordinal);

            unchecked
            {
                int hash = 17;
                foreach (string line in lines)
                    foreach (char c in line)
                        hash = (hash * 31) + c;

                return "h" + hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static string LastDispatched(FarmWorkspace workspace)
        {
            IReadOnlyList<string> trace = workspace.SqlTrace;

            for (int i = trace.Count - 1; i >= 0; i--)
                if (trace[i].IndexOf("despachado", StringComparison.Ordinal) >= 0)
                    return trace[i];

            return null;
        }

        private static string Flatten(string text) =>
            text == null ? string.Empty : text.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
