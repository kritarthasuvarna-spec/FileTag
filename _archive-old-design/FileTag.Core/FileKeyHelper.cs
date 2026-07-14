using System.Security.Cryptography;
using System.Text;

namespace FileTag.Core;

public static class FileKeyHelper
{
    public static string GetKey(string path)
    {
        var normalized = Path.GetFullPath(path).ToLowerInvariant().TrimEnd('\\');
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLower();
    }
}
