using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BundlerTool
{
    public static partial class CSharpContractIndexEngine
    {
        private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            DocumentationMode.Parse,
            SourceCodeKind.Regular);

        public static void ProcessDirectory(
            string targetDirectory,
            string outputDirectory,
            IProgress<string> progress,
            ContractVisibility visibility = ContractVisibility.PublicApi)
        {
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Directory not found: {targetDirectory}");

            string normalizedRoot = new DirectoryInfo(targetDirectory).FullName;
            string rootFolderName = new DirectoryInfo(normalizedRoot).Name;
            string indexOutputDirectory = Path.Combine(outputDirectory, rootFolderName);

            if (Directory.Exists(indexOutputDirectory))
            {
                progress?.Report($"Cleaning old C# contract index for {rootFolderName}...");
                Directory.Delete(indexOutputDirectory, true);
            }

            Directory.CreateDirectory(indexOutputDirectory);
            progress?.Report($"Discovering C# sources in: {normalizedRoot}");

            var projectGroups = BundleEngine.DiscoverProjectGroups(normalizedRoot, progress);
            var documents = new List<ContractIndexDocument>();
            var manifest = new ContractIndexManifest
            {
                SourceRoot = rootFolderName,
                Visibility = ContractAccessibilityRules.ToOptionText(visibility)
            };

            foreach (var projectGroup in projectGroups
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key, StringComparer.Ordinal))
            {
                var csharpFiles = projectGroup.Value
                    .Where(file => string.Equals(Path.GetExtension(file), ".cs", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => GetRelativePath(normalizedRoot, file), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(file => GetRelativePath(normalizedRoot, file), StringComparer.Ordinal)
                    .ToList();

                if (csharpFiles.Count == 0)
                    continue;

                progress?.Report($"Indexing C# contracts: {projectGroup.Key} ({csharpFiles.Count} files)");

                var collector = new CSharpContractCollector(normalizedRoot, projectGroup.Key);
                var manifestProject = new ContractManifestProject { Name = projectGroup.Key };

                foreach (var file in csharpFiles)
                {
                    collector.CollectFile(file);
                    manifestProject.Files.Add(new ContractManifestFile
                    {
                        Path = GetRelativePath(normalizedRoot, file),
                        Sha256 = ComputeSha256(file)
                    });
                }

                var document = collector.CreateDocument(visibility);
                documents.Add(document);
                manifest.Projects.Add(manifestProject);

                string safeProjectName = MakeSafeFileName(projectGroup.Key);
                string projectOutputDirectory = Path.Combine(indexOutputDirectory, safeProjectName);
                Directory.CreateDirectory(projectOutputDirectory);

                CSharpContractIndexWriter.WriteProject(
                    document,
                    Path.Combine(projectOutputDirectory, safeProjectName + "_API.md"),
                    Path.Combine(projectOutputDirectory, safeProjectName + "_API.json"));
            }

            CSharpContractIndexWriter.WriteMaster(
                rootFolderName,
                visibility,
                documents,
                Path.Combine(indexOutputDirectory, "_" + MakeSafeFileName(rootFolderName) + "_CSHARP_API_INDEX.md"));

            CSharpContractIndexWriter.WriteManifest(
                manifest,
                Path.Combine(indexOutputDirectory, "manifest.json"));

            int typeCount = documents.Sum(document => document.Types.Count);
            int memberCount = documents.Sum(document => document.Types.Sum(type => type.Members.Count));
            progress?.Report(
                $"C# contract index created: {documents.Count} projects, {typeCount} types, {memberCount} members.");
        }

        internal static string GetRelativePath(string rootDirectory, string path)
        {
            string normalizedRoot = new DirectoryInfo(rootDirectory).FullName.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(path);
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;

            if (normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(rootPrefix.Length)
                    .Replace(Path.DirectorySeparatorChar, '/');
            }

            return normalizedPath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "");
            }
        }

        private static string MakeSafeFileName(string value)
        {
            var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
            var builder = new StringBuilder(value.Length);

            foreach (char character in value)
            {
                builder.Append(invalidCharacters.Contains(character) ? '_' : character);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "UnnamedProject" : result;
        }
    }
}
