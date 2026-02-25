using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NekoLib.Watchdog
{
    internal static class CrashChecksums
    {
        public static string Sha256Hex(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
