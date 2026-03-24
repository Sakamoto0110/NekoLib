using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BundlerTool
{
    public class LanguageStats
    {
        public bool HasCSharp { get; set; }
        public bool HasCpp { get; set; }
        public bool HasPython { get; set; }
        public bool HasJava { get; set; }

        public bool HasHeuristicTargets => HasCpp || HasPython || HasJava;
    }

    public class SolutionScanResult
    {
        public int SolutionCount { get; set; }
        public int TotalProjects { get; set; }
        public int UnloadedOrMissingProjects { get; set; }
        public List<string> DirectoriesToIgnore { get; set; } = new List<string>();
    }

    public class BundleEngine
    {
        // Arrays for filtering
        private static readonly string[] IgnoredDirs = { "bin", "obj", "build", "out", ".vs", ".git", "node_modules", "__pycache__", ".venv", "packages" };
        private static readonly string[] AllowedExtensions = { ".cs", ".c", ".cpp", ".h", ".hpp", ".js", ".ts", ".py", ".json", ".sql", ".java" };
        private static readonly string[] IgnoredFiles = { "AssemblyInfo.cs" };

        public static LanguageStats AnalyzeTargetDirectory(string targetDirectory)
        {
            var stats = new LanguageStats();
            if (!Directory.Exists(targetDirectory)) return stats;

            try
            {
                var allFiles = Directory.EnumerateFiles(targetDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(f => !IgnoredDirs.Any(d => f.Contains($"{Path.DirectorySeparatorChar}{d}{Path.DirectorySeparatorChar}")));

                foreach (var file in allFiles)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".cs") stats.HasCSharp = true;
                    else if (ext == ".c" || ext == ".cpp" || ext == ".h" || ext == ".hpp") stats.HasCpp = true;
                    else if (ext == ".py") stats.HasPython = true;
                    else if (ext == ".java") stats.HasJava = true;

                    if (stats.HasCSharp && stats.HasCpp && stats.HasPython && stats.HasJava) break;
                }
            }
            catch { /* Ignore access exceptions during quick scan */ }

            return stats;
        }

        public static SolutionScanResult FastSolutionScan(string targetDirectory)
        {
            var result = new SolutionScanResult();
            if (!Directory.Exists(targetDirectory)) return result;

            // 1. First, find and parse any .slnf (Solution Filter) files
            var slnfFiles = Directory.GetFiles(targetDirectory, "*.slnf", SearchOption.AllDirectories)
                .Where(f => !IgnoredDirs.Any(d => f.Contains($"{Path.DirectorySeparatorChar}{d}{Path.DirectorySeparatorChar}")));

            var slnfLoadedProjects = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var slnf in slnfFiles)
            {
                var allowedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string targetSlnName = null;
                string slnfDir = Path.GetDirectoryName(slnf);

                foreach (var line in File.ReadLines(slnf))
                {
                    if (targetSlnName == null && line.Contains("\"path\"") && line.Contains(".sln"))
                    {
                        int start = line.IndexOf(':', line.IndexOf("\"path\"")) + 1;
                        int quote1 = line.IndexOf('"', start);
                        int quote2 = line.IndexOf('"', quote1 + 1);
                        if (quote1 > 0 && quote2 > quote1)
                        {
                            targetSlnName = line.Substring(quote1 + 1, quote2 - quote1 - 1).Replace("\\\\", "\\");
                        }
                    }
                    else if (line.Contains(".csproj" ) ||
                             line.Contains(".vcxproj") ||
                             line.Contains(".fsproj" ))
                    {
                        int start = line.IndexOf('"');
                        if (start >= 0)
                        {
                            int end = line.IndexOf('"', start + 1);
                            if (end > start)
                            {
                                string projPath = line.Substring(start + 1, end - start - 1).Replace("\\\\", "\\");
                                allowedProjects.Add(projPath);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(targetSlnName) && slnfDir != null)
                {
                    string fullSlnPath = Path.GetFullPath(Path.Combine(slnfDir, targetSlnName));
                    slnfLoadedProjects[fullSlnPath] = allowedProjects;
                }
            }

            // 2. Parse the actual .sln files
            var slnFiles = Directory.GetFiles(targetDirectory, "*.sln", SearchOption.AllDirectories)
                .Where(f => !IgnoredDirs.Any(d => f.Contains($"{Path.DirectorySeparatorChar}{d}{Path.DirectorySeparatorChar}")));

            foreach (var sln in slnFiles)
            {
                result.SolutionCount++;
                string slnDir = Path.GetDirectoryName(sln);

                bool hasFilter = slnfLoadedProjects.TryGetValue(sln, out var activeFilterList);

                foreach (var line in File.ReadLines(sln))
                {
                    if (line.StartsWith("Project("))
                    {
                        int firstComma = line.IndexOf(',');
                        if (firstComma > 0)
                        {
                            int secondComma = line.IndexOf(',', firstComma + 1);
                            if (secondComma > 0)
                            {
                                string relativePath = line.Substring(firstComma + 1, secondComma - firstComma - 1).Trim(' ', '"');

                                if (relativePath.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                                {
                                    result.TotalProjects++;
                                    string fullPath = Path.Combine(slnDir, relativePath);

                                    bool isMissingOnDisk = !File.Exists(fullPath);
                                    bool isFilteredOut = hasFilter && !activeFilterList.Contains(relativePath);

                                    if (isMissingOnDisk || isFilteredOut)
                                    {
                                        result.UnloadedOrMissingProjects++;
                                        string missingProjectDir = Path.GetDirectoryName(fullPath);
                                        if (!string.IsNullOrEmpty(missingProjectDir))
                                        {
                                            result.DirectoriesToIgnore.Add(missingProjectDir);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static void ProcessDirectory(string targetDirectory, string outputDirectory, IProgress<string> progress, bool astMode = false)
        {
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Directory not found: {targetDirectory}");

            string rootFolderName = new DirectoryInfo(targetDirectory).Name;
            string solutionOutputDir = Path.Combine(outputDirectory, rootFolderName);

            // Clean overwrite every time to prevent file duplication
            if (Directory.Exists(solutionOutputDir))
            {
                progress?.Report($"Cleaning old bundles for {rootFolderName}...");
                Directory.Delete(solutionOutputDir, true);
            }

            Directory.CreateDirectory(solutionOutputDir);
            progress?.Report($"Starting scan in: {targetDirectory}");

            // Parse the .bundleignore file
            var ignoredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string ignoreFilePath = Path.Combine(targetDirectory, ".bundleignore");
            if (File.Exists(ignoreFilePath))
            {
                foreach (var line in File.ReadLines(ignoreFilePath))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try { ignoredPaths.Add(Path.GetFullPath(line.Trim())); } catch { }
                    }
                }
            }

            var projectGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            TraverseDirectory(targetDirectory, targetDirectory, projectGroups, progress, ignoredPaths);

            int totalProcessed = 0;

            foreach (var kvp in projectGroups)
            {
                var projectName = kvp.Key;
                var files = kvp.Value;

                if (files.Count == 0) continue;

                progress?.Report($"Bundling project: {projectName} ({files.Count} files)");
                string projectOutputDir = Path.Combine(solutionOutputDir, projectName);
                Directory.CreateDirectory(projectOutputDir);

                string outputFileName = astMode ? $"{projectName}_ATS_source.txt" : $"{projectName}_full_source.txt";
                string compressedTxtPath = Path.Combine(projectOutputDir, outputFileName);

                using (var writer = new StreamWriter(compressedTxtPath, false))
                {
                    int bundleIndex = 1;
                    foreach (var fileChunk in files.Chunk(10))
                    {
                        string currentBundleDir = Path.Combine(projectOutputDir, $"bundle{bundleIndex++}");
                        Directory.CreateDirectory(currentBundleDir);

                        foreach (var file in fileChunk)
                        {
                            string fileName = Path.GetFileName(file);
                            progress?.Report($"Processing: {projectName} -> {fileName}");

                            string destFile = Path.Combine(currentBundleDir, fileName);
                            if (File.Exists(destFile))
                            {
                                string suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
                                destFile = Path.Combine(currentBundleDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{suffix}{Path.GetExtension(fileName)}");
                            }

                            File.Copy(file, destFile);
                            totalProcessed++;

                            string relativePath = file.Substring(targetDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            writer.WriteLine($"--- File: {relativePath} ---");

                            try
                            {
                                string fileContent = File.ReadAllText(file);

                                if (astMode)
                                {
                                    string ext = Path.GetExtension(file).ToLower();
                                    if (ext == ".cs") fileContent = StripCSharpDefinitions(fileContent);
                                    else if (ext == ".py") fileContent = StripPythonDefinitions(fileContent);
                                    else if (ext == ".java") fileContent = StripJavaDefinitions(fileContent);
                                    else if (ext == ".c" || ext == ".cpp" || ext == ".h" || ext == ".hpp") fileContent = StripCppDefinitions(fileContent);
                                }

                                writer.WriteLine(fileContent);
                                writer.WriteLine();
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"[Error reading file: {ex.Message}]");
                            }
                        }
                    }
                }
            }

            // Create Master Summary ONLY if a .sln file exists
            bool hasSlnFiles = Directory.EnumerateFiles(targetDirectory, "*.sln", SearchOption.AllDirectories).Any();

            if (hasSlnFiles)
            {
                string masterFileName = astMode
                    ? $"_{rootFolderName}_ATS_MASTER_CONTEXT.txt"
                    : $"_{rootFolderName}_MASTER_CONTEXT.txt";

                string masterFilePath = Path.Combine(solutionOutputDir, masterFileName);

                using (var masterWriter = new StreamWriter(masterFilePath, false))
                {
                    masterWriter.WriteLine($"=============================================================");

                    if (astMode) masterWriter.WriteLine($"ATS MASTER CONTEXT FOR SOLUTION: {rootFolderName}");
                    else masterWriter.WriteLine($"MASTER CONTEXT FOR SOLUTION: {rootFolderName}");

                    masterWriter.WriteLine($"Generated on: {DateTime.Now}");
                    masterWriter.WriteLine($"=============================================================");
                    masterWriter.WriteLine();

                    string searchPattern = astMode ? "*_ATS_source.txt" : "*_full_source.txt";
                    var projectSummaries = Directory.GetFiles(solutionOutputDir, searchPattern, SearchOption.AllDirectories);

                    foreach (var summaryFile in projectSummaries)
                    {
                        string cleanSuffix = astMode ? "_ATS_source" : "_full_source";
                        string projectName = Path.GetFileNameWithoutExtension(summaryFile).Replace(cleanSuffix, "");
                        masterWriter.WriteLine($">>> START PROJECT: {projectName} <<<");
                        masterWriter.WriteLine(File.ReadAllText(summaryFile));
                        masterWriter.WriteLine($">>> END PROJECT: {projectName} <<<");
                        masterWriter.WriteLine();
                    }
                }
            }

            string modeDirName = astMode ? "ATS_Bundles" : "bundles";
            progress?.Report($"Successfully bundled {totalProcessed} files into: {modeDirName}/{rootFolderName}");
        }

        private static void TraverseDirectory(string rootDirectory, string currentDirectory, Dictionary<string, List<string>> projectGroups, IProgress<string> progress, HashSet<string> ignoredPaths)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDirectory);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var file in files)
            {
                // Respect the .bundleignore list
                if (ignoredPaths.Contains(Path.GetFullPath(file))) continue;

                string ext = Path.GetExtension(file).ToLower();
                string fileName = Path.GetFileName(file);

                if (AllowedExtensions.Contains(ext) && !IgnoredFiles.Contains(fileName))
                {
                    string projectName = FindNearestProjectBoundary(file, rootDirectory);

                    if (!projectGroups.ContainsKey(projectName))
                        projectGroups[projectName] = new List<string>();

                    projectGroups[projectName].Add(file);
                }
            }

            string[] subDirs = Directory.GetDirectories(currentDirectory);
            foreach (var subdir in subDirs)
            {
                // Respect the .bundleignore list for entire folders
                if (ignoredPaths.Contains(Path.GetFullPath(subdir))) continue;

                string dirName = new DirectoryInfo(subdir).Name;
                if (!IgnoredDirs.Contains(dirName.ToLower()))
                {
                    TraverseDirectory(rootDirectory, subdir, projectGroups, progress, ignoredPaths);
                }
            }
        }

        private static string FindNearestProjectBoundary(string path, string rootDirectory)
        {
            DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(path));
            string normalizedRoot = new DirectoryInfo(rootDirectory).FullName;

            while (dir != null)
            {
                var projectFile = dir.GetFiles("*.csproj").FirstOrDefault()
                               ?? dir.GetFiles("CMakeLists.txt").FirstOrDefault()
                               ?? dir.GetFiles("pom.xml").FirstOrDefault()
                               ?? dir.GetFiles("package.json").FirstOrDefault()
                               ?? dir.GetFiles("*.sln").FirstOrDefault(); // Grabs root-level files

                if (projectFile != null)
                    return Path.GetFileNameWithoutExtension(projectFile.Name);

                // Hard boundary: Stop climbing the exact moment we hit the target root folder.
                if (dir.FullName.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    break;

                dir = dir.Parent;
            }

            return "Miscellaneous";
        }

        // --- PARSERS & STRIPPERS ---

        private static string StripCSharpDefinitions(string sourceCode)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();
            var rewriter = new SkeletonRewriter();
            var newRoot = rewriter.Visit(root);
            return newRoot.ToFullString();
        }

        private static string StripPythonDefinitions(string sourceCode)
        {
            var lines = sourceCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new StringBuilder();

            bool stripping = false;
            int stripIndentLevel = -1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!stripping) result.AppendLine(line);
                    continue;
                }

                int currentIndent = line.Length - line.TrimStart().Length;

                if (stripping)
                {
                    if (currentIndent > stripIndentLevel) continue;
                    else stripping = false;
                }

                if (!stripping)
                {
                    result.AppendLine(line);
                    string trimmed = line.TrimStart();

                    if (trimmed.StartsWith("def ") || trimmed.StartsWith("class ") || trimmed.StartsWith("async def "))
                    {
                        stripping = true;
                        stripIndentLevel = currentIndent;
                        result.AppendLine(new string(' ', currentIndent + 4) + "pass # [AST stripped]");
                    }
                }
            }
            return result.ToString();
        }

        private static string StripCppDefinitions(string sourceCode)
        {
            var result = new StringBuilder();
            result.AppendLine("/* [AST WARNING]: Heuristic extraction. Macros (#define) are UNRELIABLE and not unrolled. */\n");
            return ProcessBraceDepthHeuristic(sourceCode, result, maxAllowedDepth: 1);
        }

        private static string StripJavaDefinitions(string sourceCode)
        {
            var result = new StringBuilder();
            result.AppendLine("/* [AST WARNING]: Heuristic extraction. Interface/Class bodies retained, method bodies stripped. */\n");
            return ProcessBraceDepthHeuristic(sourceCode, result, maxAllowedDepth: 1);
        }

        private static string ProcessBraceDepthHeuristic(string sourceCode, StringBuilder result, int maxAllowedDepth)
        {
            int braceDepth = 0;
            bool inString = false, inChar = false, inSingleLineComment = false, inMultiLineComment = false;

            for (int i = 0; i < sourceCode.Length; i++)
            {
                char c = sourceCode[i];
                char nextC = i + 1 < sourceCode.Length ? sourceCode[i + 1] : '\0';
                char prevC = i > 0 ? sourceCode[i - 1] : '\0';

                if (inSingleLineComment) { if (c == '\n') inSingleLineComment = false; }
                else if (inMultiLineComment) { if (c == '*' && nextC == '/') { inMultiLineComment = false; i++; result.Append("*/"); continue; } }
                else if (inString) { if (c == '"' && prevC != '\\') inString = false; }
                else if (inChar) { if (c == '\'' && prevC != '\\') inChar = false; }
                else
                {
                    if (c == '/' && nextC == '/') { inSingleLineComment = true; i++; result.Append("//"); continue; }
                    if (c == '/' && nextC == '*') { inMultiLineComment = true; i++; result.Append("/*"); continue; }
                    if (c == '"') inString = true;
                    if (c == '\'') inChar = true;

                    if (c == '{')
                    {
                        braceDepth++;
                        if (braceDepth > maxAllowedDepth)
                        {
                            if (braceDepth == maxAllowedDepth + 1)
                            {
                                // Erase trailing whitespace and newlines to snug the semicolon
                                while (result.Length > 0 && char.IsWhiteSpace(result[result.Length - 1]))
                                {
                                    result.Length--;
                                }
                                result.Append(";");
                            }
                            continue;
                        }
                    }
                    else if (c == '}')
                    {
                        if (braceDepth > maxAllowedDepth)
                        {
                            braceDepth--;
                            continue; // Skip appending the closing brace entirely
                        }
                        braceDepth--;
                    }
                }

                if (braceDepth <= maxAllowedDepth) result.Append(c);
            }
            return result.ToString();
        }

        // --- Roslyn Syntax Rewriter for C# ---
        private class SkeletonRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var newNode = node.WithBody(null)
                                  .WithExpressionBody(null)
                                  .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                if (newNode.ParameterList != null)
                    newNode = newNode.WithParameterList(newNode.ParameterList.WithoutTrailingTrivia());

                return base.VisitMethodDeclaration(newNode);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                var newNode = node.WithBody(null)
                                  .WithExpressionBody(null)
                                  .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                if (newNode.ParameterList != null)
                    newNode = newNode.WithParameterList(newNode.ParameterList.WithoutTrailingTrivia());

                return base.VisitConstructorDeclaration(newNode);
            }

            public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
            {
                var newNode = node.WithBody(null)
                                  .WithExpressionBody(null)
                                  .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                return base.VisitAccessorDeclaration(newNode);
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.ExpressionBody != null)
                {
                    var emptyGet = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                    return base.VisitPropertyDeclaration(
                        node.WithExpressionBody(null)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(emptyGet))));
                }
                return base.VisitPropertyDeclaration(node);
            }
        }
    }
}