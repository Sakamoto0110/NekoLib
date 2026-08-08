#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers
{
    /// <summary>
    /// Access profile, driven through the ACE OLEDB provider.
    /// <para/>
    /// This is the interesting half of the scenario. Access disagrees with SQLite on
    /// row limiting (<c>TOP n</c> vs <c>LIMIT n</c>), on DDL vocabulary
    /// (<c>COUNTER</c>/<c>TEXT(n)</c>/<c>LONG</c>), and - most importantly for
    /// NekoLib - on parameter binding: OleDb binds positionally, which is what
    /// <c>PositionalDbParameterBinder</c> exists to handle.
    /// </summary>
    public sealed class AccessProfile : IFarmProviderProfile
    {
        // ACE 16.0 ships with current Office / the 2016+ redistributable; 12.0 is
        // the older but far more widely installed one. Either is fine.
        private static readonly string[] CandidateProgIds =
        {
            "Microsoft.ACE.OLEDB.12.0",
            "Microsoft.ACE.OLEDB.16.0"
        };

        private readonly string _progId;

        public AccessProfile(string databasePath)
        {
            DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _progId = ResolveInstalledProgId() ?? CandidateProgIds[0];
        }

        public FarmProvider Provider => FarmProvider.Access;

        public string DisplayName => "Access (ACE OLEDB)";

        public string DialectNotes =>
            "TOP n para limitar linhas - parâmetros posicionais (?) ligados por ordem - " +
            "catálogo só via schema rowset do OleDb - DDL usa COUNTER/TEXT(n)/LONG.";

        public string DatabasePath { get; }

        public string ConnectionString =>
            "Provider=" + _progId + ";Data Source=" + DatabasePath + ";";

        /// <summary>The ACE ProgID this profile resolved, shown in the UI.</summary>
        public string ProgId => _progId;

        // -----------------------------------------------------------------
        // Availability
        // -----------------------------------------------------------------

        /// <summary>
        /// Enumerates the OLEDB providers visible to *this* process. Bitness matters:
        /// an x64 ACE install is invisible to an x86 process and vice versa, which is
        /// why the app pins PlatformTarget rather than running AnyCPU.
        /// </summary>
        private static string? ResolveInstalledProgId()
        {
            try
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (OleDbDataReader reader = OleDbEnumerator.GetRootEnumerator())
                {
                    while (reader.Read())
                        installed.Add(reader.GetString(0));
                }

                foreach (string candidate in CandidateProgIds)
                    if (installed.Contains(candidate))
                        return candidate;

                return null;
            }
            catch
            {
                return null;
            }
        }

        public ProviderAvailability Probe()
        {
            string? resolved = ResolveInstalledProgId();
            if (resolved != null)
                return ProviderAvailability.Available();

            string bitness = IntPtr.Size == 8 ? "x64" : "x86";
            return ProviderAvailability.Unavailable(
                "Nenhum provider ACE OLEDB visível neste processo (" + bitness + ").",
                "Instale o Microsoft Access Database Engine 2016 Redistributable na " +
                "versão " + bitness + ". O driver é registrado por bitness: uma " +
                "instalação x64 é invisível para um processo x86 e vice-versa.");
        }

        // -----------------------------------------------------------------
        // File lifecycle
        // -----------------------------------------------------------------

        /// <summary>
        /// OleDb has no <c>CREATE DATABASE</c>, so an empty <c>.accdb</c> has to be
        /// produced by ADOX. It is bound late through its ProgID so the scenario needs
        /// no COM reference and no interop assembly - which also keeps it building
        /// identically on both target families.
        /// </summary>
        public void EnsureDatabaseFile()
        {
            string? dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(DatabasePath))
                return;

            Type? catalogType = Type.GetTypeFromProgID("ADOX.Catalog");
            if (catalogType == null)
            {
                throw new InvalidOperationException(
                    "ADOX.Catalog não está registrado nesta máquina, então um .accdb " +
                    "vazio não pode ser criado. O ADOX acompanha a instalação do ACE.");
            }

            object? catalog = Activator.CreateInstance(catalogType);
            if (catalog == null)
                throw new InvalidOperationException("Não foi possível instanciar ADOX.Catalog.");

            try
            {
                catalogType.InvokeMember(
                    "Create",
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: catalog,
                    args: new object[] { ConnectionString });
            }
            finally
            {
                ReleaseCom(catalog);
            }
        }

        private static void ReleaseCom(object comObject)
        {
            try
            {
                if (Marshal.IsComObject(comObject))
                    Marshal.ReleaseComObject(comObject);
            }
            catch
            {
                // Releasing is best effort: the GC finalizes the RCW either way, and
                // failing to release must never take the scenario down.
            }
        }

        public void DeleteDatabaseFile()
        {
            OleDbConnection.ReleaseObjectPool();
            if (File.Exists(DatabasePath))
                File.Delete(DatabasePath);

            // ACE leaves a lock file next to the database when a handle died badly.
            string lockFile = Path.ChangeExtension(DatabasePath, ".laccdb");
            if (File.Exists(lockFile))
            {
                try { File.Delete(lockFile); } catch { /* held by another process */ }
            }
        }

        // -----------------------------------------------------------------
        // NekoLib.Data wiring
        // -----------------------------------------------------------------

        public IDbConnectionFactory CreateConnectionFactory() =>
            new DbConnectionAbstractFactory<OleDbConnection>(ConnectionString);

        public IDbQueryTranslator CreateTranslator() => new AccessQueryTranslator();

        /// <summary>
        /// Bracket quoting. Access requires it for reserved words such as
        /// <c>Name</c>; SQLite accepts the same syntax for MS-compatibility, so both
        /// profiles can quote identically and the repositories stay dialect-free.
        /// </summary>
        public string Quote(string identifier) => "[" + identifier + "]";

        public IReadOnlyList<string> SchemaDdl() => new[]
        {
            @"CREATE TABLE Roles (
                [Id]          COUNTER      PRIMARY KEY,
                [Title]       TEXT(120)    NOT NULL,
                [BaseSalary]  DOUBLE       NOT NULL
              )",

            @"CREATE TABLE Employees (
                [Id]      COUNTER    PRIMARY KEY,
                [Name]    TEXT(120)  NOT NULL,
                [Age]     LONG       NOT NULL,
                [Cpf]     TEXT(20)   NOT NULL,
                [Phone]   TEXT(30)   NOT NULL,
                [RoleId]  LONG       NOT NULL
              )",

            @"CREATE TABLE Products (
                [Id]         COUNTER    PRIMARY KEY,
                [Name]       TEXT(120)  NOT NULL,
                [Category]   TEXT(40)   NOT NULL,
                [Unit]       TEXT(20)   NOT NULL,
                [Quantity]   LONG       NOT NULL,
                [UnitPrice]  DOUBLE     NOT NULL
              )",

            @"CREATE TABLE Animals (
                [Id]        COUNTER    PRIMARY KEY,
                [Species]   TEXT(40)   NOT NULL,
                [Tag]       TEXT(40)   NOT NULL,
                [AgeYears]  LONG       NOT NULL,
                [Gender]    TEXT(20)   NOT NULL,
                [Notes]     TEXT(255)
              )",

            // The herd never reuses a tag: removing BV-003 does not free that number
            // for the next arrival. A persisted counter is what makes that survive,
            // because a hard DELETE takes the row's evidence with it.
            @"CREATE TABLE TagSequence (
                [Prefix]      TEXT(10)  NOT NULL PRIMARY KEY,
                [LastNumber]  LONG      NOT NULL
              )",

            @"CREATE TABLE OperationLog (
                [Id]          COUNTER    PRIMARY KEY,
                [OccurredAt]  TEXT(30)   NOT NULL,
                [EntityKind]  TEXT(20)   NOT NULL,
                [EntityId]    LONG       NOT NULL,
                [EntityName]  TEXT(120)  NOT NULL,
                [Operation]   TEXT(20)   NOT NULL,
                [Quantity]    LONG       NOT NULL,
                [Reason]      TEXT(255)
              )",

            // --- simulation ------------------------------------------------
            // [Tick] is LONG, which in Access is a 32-bit integer. At one tick per
            // second that is comfortable for any run this scenario will do, but it is
            // the same 2,147,483,647 ceiling that gold would eventually hit - and
            // finding where the two engines stop agreeing is the point of running the
            // same seed on both.
            @"CREATE TABLE SimState (
                [Id]        LONG    NOT NULL PRIMARY KEY,
                [Tick]      LONG    NOT NULL,
                [Seed]      LONG    NOT NULL,
                [Gold]      DOUBLE  NOT NULL,
                [Terrains]  LONG    NOT NULL,
                [Slots]     LONG    NOT NULL,
                [Workers]   LONG    NOT NULL
              )",

            @"CREATE TABLE SimTiles (
                [Id]              COUNTER   PRIMARY KEY,
                [Terrain]         LONG      NOT NULL,
                [Slot]            LONG      NOT NULL,
                [Crop]            TEXT(40)  NOT NULL,
                [PlantedAtTick]   LONG      NOT NULL,
                [HasWorker]       LONG      NOT NULL,
                [NextActionTick]  LONG      NOT NULL
              )",

            @"CREATE TABLE SimMarket (
                [Crop]      TEXT(40)  NOT NULL PRIMARY KEY,
                [Quantity]  DOUBLE    NOT NULL
              )",

            @"CREATE TABLE SimInventory (
                [Crop]      TEXT(40)  NOT NULL PRIMARY KEY,
                [Quantity]  LONG      NOT NULL
              )"
        };

        /// <summary>
        /// Access has no queryable catalog the way SQLite does - <c>MSysObjects</c>
        /// is permission-gated - so the table list comes from the OleDb schema
        /// rowset. That needs the live connection, which is exactly what
        /// <see cref="DbSession.Connection"/> exposes.
        /// </summary>
        public Task<IReadOnlyList<string>> ListTablesAsync(
            IDatabaseGateway gateway,
            DbSession session,
            CancellationToken ct)
        {
            if (!(session.Connection is OleDbConnection oleDb))
            {
                throw new InvalidOperationException(
                    "A sessão do Access não carrega uma OleDbConnection.");
            }

            DataTable? schema = oleDb.GetOleDbSchemaTable(
                OleDbSchemaGuid.Tables,
                new object?[] { null, null, null, "TABLE" });

            var tables = new List<string>();
            if (schema != null)
            {
                foreach (DataRow row in schema.Rows)
                {
                    string? name = row["TABLE_NAME"] as string;
                    if (!string.IsNullOrEmpty(name))
                        tables.Add(name!);
                }
            }

            tables.Sort(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyList<string>>(tables);
        }
    }
}
