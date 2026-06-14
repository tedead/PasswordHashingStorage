using System.Security.Cryptography;
using System.Text;

namespace PasswordHashStorage.Desktop.Security;

public sealed record ComputedHash(string Algorithm, byte[] HashValue)
{
    public string Hex => Convert.ToHexString(HashValue).ToLowerInvariant();
}

public static class HashCalculator
{
    public static IReadOnlyList<string> SupportedAlgorithms { get; } =
        new[] { "MD5", "SHA1", "SHA256", "SHA384", "SHA512" };

    public static ComputedHash Compute(string plaintext, string algorithm)
    {
        byte[] input = Encoding.UTF8.GetBytes(plaintext);
        byte[] hash = algorithm.ToUpperInvariant() switch
        {
            "MD5"    => MD5.HashData(input),
            "SHA1"   => SHA1.HashData(input),
            "SHA256" => SHA256.HashData(input),
            "SHA384" => SHA384.HashData(input),
            "SHA512" => SHA512.HashData(input),
            _ => throw new NotSupportedException($"Algorithm '{algorithm}' is not supported.")
        };
        return new ComputedHash(algorithm, hash);
    }

    public static IEnumerable<ComputedHash> ComputeAll(string plaintext, IEnumerable<string> algorithms)
    {
        foreach (string alg in algorithms)
            yield return Compute(plaintext, alg);
    }

    public static IReadOnlyList<string> DetectAlgorithms(string hex)
    {
        string h = hex.Trim();
        if (!h.All(Uri.IsHexDigit))
            return Array.Empty<string>();

        return h.Length switch
        {
            32  => new[] { "MD5" },
            40  => new[] { "SHA1" },
            64  => new[] { "SHA256" },
            96  => new[] { "SHA384" },
            128 => new[] { "SHA512" },
            _   => Array.Empty<string>()
        };
    }

    public static byte[] ParseHex(string hex)
    {
        string h = hex.Trim();
        if (h.Length % 2 != 0)
            throw new FormatException("Invalid hexadecimal length.");
        return Convert.FromHexString(h);
    }
}
