using System.Security.Cryptography;

namespace RpaDevAssistant.Core.Fixes.Apply;

public static class UiPathFileHash
{
    public static string Sha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
