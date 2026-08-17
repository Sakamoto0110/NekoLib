using System;
using System.IO;
using System.Reflection;
using System.Text;
using PublicApiGenerator;

namespace NekoLib.PublicApiTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: NekoLib.PublicApiTool <assembly-path> <output-path>");
                return 2;
            }

            try
            {
                var assemblyPath = Path.GetFullPath(args[0]);
                var outputPath = Path.GetFullPath(args[1]);

                if (!File.Exists(assemblyPath))
                {
                    Console.Error.WriteLine("Assembly not found: " + assemblyPath);
                    return 2;
                }

                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                var publicApi = assembly.GeneratePublicApi()
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");

                File.WriteAllText(outputPath, publicApi, new UTF8Encoding(false));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
