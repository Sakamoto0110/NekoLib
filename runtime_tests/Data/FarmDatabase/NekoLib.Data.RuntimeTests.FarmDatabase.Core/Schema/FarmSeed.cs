#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Schema
{
    /// <summary>
    /// Fixed seed data for the farm. Deterministic on purpose: two runs against two
    /// different engines must produce comparable content, otherwise a difference in
    /// the UI cannot be attributed to the provider.
    /// <para/>
    /// The CPFs are fabricated and are not valid check-digit sequences.
    /// </summary>
    public static class FarmSeed
    {
        public static IReadOnlyList<Role> Roles => new[]
        {
            new Role { Title = "Capataz",      BaseSalary = 4200.00 },
            new Role { Title = "Tratador",     BaseSalary = 2600.00 },
            new Role { Title = "Ordenhador",   BaseSalary = 2750.00 },
            new Role { Title = "Horticultor",  BaseSalary = 2900.00 },
            new Role { Title = "Veterinário",  BaseSalary = 7300.00 }
        };

        /// <summary>RoleId values are 1-based and line up with <see cref="Roles"/>.</summary>
        public static IReadOnlyList<Employee> Employees => new[]
        {
            new Employee { Name = "Aparecida Nogueira", Age = 47, Cpf = "312.884.190-33", Phone = "(16) 99721-4408", RoleId = 1 },
            new Employee { Name = "Benedito Ramalho",   Age = 58, Cpf = "204.617.885-12", Phone = "(16) 99184-2231", RoleId = 2 },
            new Employee { Name = "Cleuza Antunes",     Age = 34, Cpf = "581.229.043-77", Phone = "(16) 98862-1190", RoleId = 3 },
            new Employee { Name = "Divino Prates",      Age = 29, Cpf = "447.930.612-05", Phone = "(16) 99503-7764", RoleId = 2 },
            new Employee { Name = "Eunice Bittencourt",  Age = 41, Cpf = "690.155.827-48", Phone = "(16) 99347-8802", RoleId = 4 },
            new Employee { Name = "Firmino Salgado",    Age = 52, Cpf = "158.402.736-91", Phone = "(16) 98219-6653", RoleId = 4 },
            new Employee { Name = "Gilmara Peçanha",    Age = 38, Cpf = "873.061.294-26", Phone = "(16) 99630-1178", RoleId = 5 },
            new Employee { Name = "Hamilton Vasques",   Age = 63, Cpf = "025.748.361-59", Phone = "(16) 99012-4437", RoleId = 1 }
        };

        public static IReadOnlyList<Product> Products => new[]
        {
            // Frutas
            new Product { Name = "Banana Prata",     Category = Categories.Fruit,     Unit = "cacho", Quantity = 64,  UnitPrice =  8.50 },
            new Product { Name = "Laranja Pera",     Category = Categories.Fruit,     Unit = "caixa", Quantity = 37,  UnitPrice = 42.00 },
            new Product { Name = "Manga Tommy",      Category = Categories.Fruit,     Unit = "caixa", Quantity = 22,  UnitPrice = 55.75 },
            new Product { Name = "Mamão Formosa",    Category = Categories.Fruit,     Unit = "unid",  Quantity = 118, UnitPrice =  6.20 },
            new Product { Name = "Abacaxi Pérola",   Category = Categories.Fruit,     Unit = "unid",  Quantity = 73,  UnitPrice =  9.90 },
            new Product { Name = "Maracujá Azedo",   Category = Categories.Fruit,     Unit = "kg",    Quantity = 46,  UnitPrice = 12.40 },

            // Verduras
            new Product { Name = "Alface Crespa",    Category = Categories.Vegetable, Unit = "maço",  Quantity = 152, UnitPrice =  3.10 },
            new Product { Name = "Couve Manteiga",   Category = Categories.Vegetable, Unit = "maço",  Quantity = 97,  UnitPrice =  3.80 },
            new Product { Name = "Rúcula",           Category = Categories.Vegetable, Unit = "maço",  Quantity = 61,  UnitPrice =  4.25 },
            new Product { Name = "Espinafre",        Category = Categories.Vegetable, Unit = "maço",  Quantity = 44,  UnitPrice =  4.60 },
            new Product { Name = "Agrião",           Category = Categories.Vegetable, Unit = "maço",  Quantity = 38,  UnitPrice =  4.15 },

            // Legumes
            new Product { Name = "Cenoura",          Category = Categories.Legume,    Unit = "kg",    Quantity = 240, UnitPrice =  5.30 },
            new Product { Name = "Batata Inglesa",   Category = Categories.Legume,    Unit = "kg",    Quantity = 415, UnitPrice =  4.70 },
            new Product { Name = "Abobrinha",        Category = Categories.Legume,    Unit = "kg",    Quantity = 88,  UnitPrice =  6.05 },
            new Product { Name = "Tomate Italiano",  Category = Categories.Legume,    Unit = "kg",    Quantity = 176, UnitPrice =  7.85 },
            new Product { Name = "Beterraba",        Category = Categories.Legume,    Unit = "kg",    Quantity = 92,  UnitPrice =  5.95 },
            new Product { Name = "Mandioquinha",     Category = Categories.Legume,    Unit = "kg",    Quantity = 57,  UnitPrice = 11.20 }
        };

        public static IReadOnlyList<Animal> Animals => new[]
        {
            new Animal { Species = "Vaca",    Tag = "BV-001", AgeYears = 6, Gender = "Fêmea", Notes = "Holandesa, alta produção"  },
            new Animal { Species = "Vaca",    Tag = "BV-002", AgeYears = 4, Gender = "Fêmea", Notes = "Girolando"                 },
            new Animal { Species = "Vaca",    Tag = "BV-003", AgeYears = 9, Gender = "Fêmea", Notes = "Produção em queda"         },
            new Animal { Species = "Vaca",    Tag = "BV-004", AgeYears = 2, Gender = "Macho", Notes = "Reprodutor jovem"          },
            new Animal { Species = "Vaca",    Tag = "BV-005", AgeYears = 7, Gender = "Fêmea", Notes = null                        },
            new Animal { Species = "Porco",   Tag = "SU-101", AgeYears = 2, Gender = "Fêmea", Notes = "Matriz"                    },
            new Animal { Species = "Porco",   Tag = "SU-102", AgeYears = 1, Gender = "Macho", Notes = null                        },
            new Animal { Species = "Porco",   Tag = "SU-103", AgeYears = 3, Gender = "Fêmea", Notes = "Matriz, terceira leitegada" },
            new Animal { Species = "Porco",   Tag = "SU-104", AgeYears = 1, Gender = "Macho", Notes = null                        },
            new Animal { Species = "Galinha", Tag = "GA-201", AgeYears = 2, Gender = "Fêmea", Notes = "Poedeira"                  },
            new Animal { Species = "Galinha", Tag = "GA-202", AgeYears = 1, Gender = "Fêmea", Notes = "Poedeira"                  },
            new Animal { Species = "Galinha", Tag = "GA-203", AgeYears = 3, Gender = "Fêmea", Notes = "Postura irregular"         },
            new Animal { Species = "Galinha", Tag = "GA-204", AgeYears = 1, Gender = "Macho", Notes = "Galo do lote"              },
            new Animal { Species = "Galinha", Tag = "GA-205", AgeYears = 2, Gender = "Fêmea", Notes = null                        }
        };

        /// <summary>The species a new animal can be registered as, in seed order.</summary>
        public static IReadOnlyList<string> Species
        {
            get
            {
                var species = new List<string>();
                foreach (Animal animal in Animals)
                    if (!species.Contains(animal.Species))
                        species.Add(animal.Species);
                return species;
            }
        }

        /// <summary>The genders a new animal can be registered as.</summary>
        public static IReadOnlyList<string> Genders => new[] { "Fêmea", "Macho" };

        /// <summary>
        /// The tag prefix a species uses, read out of the seed rather than declared
        /// twice: `Vaca` yields `BV` because the seeded cows are tagged `BV-00n`.
        /// </summary>
        public static string PrefixFor(string species)
        {
            foreach (Animal animal in Animals)
                if (animal.Species == species)
                    return SplitTag(animal.Tag).Prefix;

            throw new ArgumentOutOfRangeException(
                nameof(species), species, "No seeded animal uses that species.");
        }

        /// <summary>
        /// Starting counter per prefix: the highest number the seed itself used. New
        /// arrivals continue from here, so the first cow registered after seeding is
        /// `BV-006` regardless of how many have been removed.
        /// </summary>
        public static IReadOnlyDictionary<string, int> InitialTagNumbers()
        {
            var highest = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Animal animal in Animals)
            {
                (string prefix, int number) = SplitTag(animal.Tag);
                if (!highest.TryGetValue(prefix, out int current) || number > current)
                    highest[prefix] = number;
            }

            return highest;
        }

        /// <summary>Splits <c>"BV-003"</c> into <c>("BV", 3)</c>.</summary>
        public static (string Prefix, int Number) SplitTag(string tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            int dash = tag.IndexOf('-');
            if (dash <= 0 || dash == tag.Length - 1)
                throw new FormatException("Tag '" + tag + "' is not in PREFIX-NUMBER form.");

            return (tag.Substring(0, dash), int.Parse(
                tag.Substring(dash + 1), CultureInfo.InvariantCulture));
        }

        /// <summary>Formats <c>("BV", 6)</c> as <c>"BV-006"</c>.</summary>
        public static string FormatTag(string prefix, int number) =>
            prefix + "-" + number.ToString("000", CultureInfo.InvariantCulture);
    }
}
