using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BundlerTool
{
    public class BundleEngine
    {
        // Arrays for filtering
        private static readonly string[] IgnoredDirs = { "bin", "obj", "build", "out", ".vs", ".git", "node_modules", "__pycache__", ".venv", "packages" };
        private static readonly string[] AllowedExtensions = { ".cs", ".c", ".cpp", ".h", ".hpp", ".js", ".ts", ".py", ".json", ".sql" };
        private static readonly string[] IgnoredFiles = { "AssemblyInfo.cs" }; 
        private static readonly string[] BoundaryFiles = { "*.sln", "*.csproj", "CMakeLists.txt", "Makefile", "package.json", "requirements.txt" };

        public static void ProcessDirectory(string targetDirectory, string outputDirectory, IProgress<string> progress)
        {
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Directory not found: {targetDirectory}");

            // 1. Identify the Solution/Root Name (e.g., "NekoLib")
            string rootFolderName = new DirectoryInfo(targetDirectory).Name;

            // 2. Create the Solution Wrapper inside the global bundles folder
            string solutionOutputDir = Path.Combine(outputDirectory, rootFolderName);
            Directory.CreateDirectory(solutionOutputDir);

            progress?.Report($"Starting scan in: {targetDirectory}");

            var projectGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Fill the groups (logic assumed to be in TraverseDirectory)
            TraverseDirectory(targetDirectory, targetDirectory, projectGroups, progress);

            int totalProcessed = 0;

            foreach (var kvp in projectGroups)
            {
                var projectName = kvp.Key;
                var files = kvp.Value;

                if (files.Count == 0) continue;

                progress?.Report($"Bundling project: {projectName} ({files.Count} files)");

                // 3. Create Project folder INSIDE the Solution folder
                string projectOutputDir = Path.Combine(solutionOutputDir, projectName);
                Directory.CreateDirectory(projectOutputDir);

                // 4. Move the compressed file INSIDE the project folder to reduce noise
                string compressedTxtPath = Path.Combine(projectOutputDir, $"{projectName}_full_source.txt");

                using (var writer = new StreamWriter(compressedTxtPath, false))
                {
                    int bundleIndex = 1;

                    // Using the .Chunk() extension we discussed
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

                            // Write to the internal summary file
                            string relativePath = file.Substring(targetDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            writer.WriteLine($"--- File: {relativePath} ---");

                            try
                            {
                                writer.WriteLine(File.ReadAllText(file));
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

            // 5. Create a Master Summary for the entire Solution
            string masterFilePath = Path.Combine(solutionOutputDir, $"_{rootFolderName}_MASTER_CONTEXT.txt");
            using (var masterWriter = new StreamWriter(masterFilePath, false))
            {
                masterWriter.WriteLine($"=============================================================");
                masterWriter.WriteLine($"MASTER CONTEXT FOR SOLUTION: {rootFolderName}");
                masterWriter.WriteLine($"Generated on: {DateTime.Now}");
                masterWriter.WriteLine($"=============================================================");
                masterWriter.WriteLine();

                // Find all the individual project summaries we just made
                var projectSummaries = Directory.GetFiles(solutionOutputDir, "*_full_source.txt", SearchOption.AllDirectories);
                foreach (var summaryFile in projectSummaries)
                {
                    string projectName = Path.GetFileNameWithoutExtension(summaryFile).Replace("_full_source", "");
                    masterWriter.WriteLine($">>> START PROJECT: {projectName} <<<");
                    masterWriter.WriteLine(File.ReadAllText(summaryFile));
                    masterWriter.WriteLine($">>> END PROJECT: {projectName} <<<");
                    masterWriter.WriteLine();
                }
            }

            progress?.Report($"Successfully bundled {totalProcessed} files into: bundles/{rootFolderName}");
        }
        private static void TraverseDirectory(string rootDirectory, string currentDirectory, Dictionary<string, List<string>> projectGroups, IProgress<string> progress)
        {
            // 1. Get all files in the current folder
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDirectory);
            }
            catch (Exception)
            {
                // Skip folders we can't access (like System Volume Information)
                return;
            }

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                string fileName = Path.GetFileName(file);

                // 2. Check if the file type is allowed and not on the ignore list
                if (AllowedExtensions.Contains(ext) && !IgnoredFiles.Contains(fileName))
                {
                    // 3. Find the "Owner" Project for this specific file
                    string projectName = FindNearestProjectBoundary(file, rootDirectory);

                    if (!projectGroups.ContainsKey(projectName))
                        projectGroups[projectName] = new List<string>();

                    projectGroups[projectName].Add(file);
                }
            }

            // 4. Recurse into subdirectories
            string[] subDirs = Directory.GetDirectories(currentDirectory);
            foreach (var subdir in subDirs)
            {
                string dirName = new DirectoryInfo(subdir).Name;

                // Skip folders like bin, obj, .git, .vs
                if (!IgnoredDirs.Contains(dirName.ToLower()))
                {
                    TraverseDirectory(rootDirectory, subdir, projectGroups, progress);
                }
            }
        }
        private static string FindNearestProjectBoundary(string path, string rootDirectory)
        {
            DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(path));

            // Climb up until we hit a project file or we reach the root we started from
            while (dir != null && dir.FullName.Length >= rootDirectory.Length)
            {
                var projectFile = dir.GetFiles("*.csproj").FirstOrDefault()
                               ?? dir.GetFiles("CMakeLists.txt").FirstOrDefault();

                if (projectFile != null)
                    return Path.GetFileNameWithoutExtension(projectFile.Name);

                dir = dir.Parent;
            }

            return "Miscellaneous";
        }
    }
}
