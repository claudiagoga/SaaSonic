using System.Security.Cryptography;
using System.Text;

namespace SaaSonic.Application.Tests.Common;

/// Replicates the same SHA-256 hash used internally by TokenHelper so that
/// tests can pre-compute token hashes without accessing the internal helper.
internal static class TestHashHelper
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
