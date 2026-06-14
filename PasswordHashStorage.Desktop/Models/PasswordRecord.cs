namespace PasswordHashStorage.Desktop.Models;

public sealed class PasswordRecord
{
    public long PasswordId { get; set; }
    public string Plaintext { get; set; } = string.Empty;
    public DateTime DateCreatedUtc { get; set; }
    public List<HashRecord> Hashes { get; set; } = new();
}

public sealed class HashRecord
{
    public long HashId { get; set; }
    public long PasswordId { get; set; }
    public short AlgorithmId { get; set; }
    public string AlgorithmName { get; set; } = string.Empty;
    public byte[] HashValue { get; set; } = Array.Empty<byte>();
    public string HashHex => Convert.ToHexString(HashValue).ToLowerInvariant();
}

public sealed class AlgorithmRecord
{
    public short AlgorithmId { get; set; }
    public string Name { get; set; } = string.Empty;
    public short HashLengthBytes { get; set; }
}
