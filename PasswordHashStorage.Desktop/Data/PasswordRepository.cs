using Microsoft.Data.SqlClient;
using PasswordHashStorage.Desktop.Models;
using PasswordHashStorage.Desktop.Security;
using System.Data;

namespace PasswordHashStorage.Desktop.Data;

public sealed class PasswordRepository
{
    private readonly string _connectionString;

    public PasswordRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Dictionary<string, short>> GetAlgorithmMapAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT AlgorithmID, Name FROM HashAlgorithms;";
        var map = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            map[reader.GetString(1)] = reader.GetInt16(0);

        return map;
    }

    public async Task<long> InsertPasswordAsync(string plaintext, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Passwords(Plaintext)
            OUTPUT INSERTED.PasswordID
            VALUES(@Plaintext);
            """;

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Plaintext", plaintext);
        object? result = await cmd.ExecuteScalarAsync(ct);
        return (long)result!;
    }

    public async Task BulkInsertHashesAsync(
        IEnumerable<(long PasswordId, short AlgorithmId, byte[] HashValue)> rows,
        CancellationToken ct = default)
    {
        DataTable table = new();
        table.Columns.Add("PasswordID",  typeof(long));
        table.Columns.Add("AlgorithmID", typeof(short));
        table.Columns.Add("HashValue",   typeof(byte[]));

        foreach (var (pid, aid, hash) in rows)
            table.Rows.Add(pid, aid, hash);

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        using SqlBulkCopy bulk = new(conn, SqlBulkCopyOptions.CheckConstraints, null);
        bulk.DestinationTableName = "PasswordHashes";
        bulk.ColumnMappings.Add("PasswordID",  "PasswordID");
        bulk.ColumnMappings.Add("AlgorithmID", "AlgorithmID");
        bulk.ColumnMappings.Add("HashValue",   "HashValue");
        await bulk.WriteToServerAsync(table, ct);
    }

    public async Task<string?> LookupPlaintextAsync(string hexHash, string algorithm, CancellationToken ct = default)
    {
        byte[] hashBytes;
        try { hashBytes = HashCalculator.ParseHex(hexHash); }
        catch { return null; }

        const string sql = """
            SELECT TOP (1) p.Plaintext
            FROM PasswordHashes ph
            JOIN Passwords p ON p.PasswordID = ph.PasswordID
            JOIN HashAlgorithms ha ON ha.AlgorithmID = ph.AlgorithmID
            WHERE ha.Name = @Algorithm
              AND ph.HashValue = @HashValue;
            """;

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Algorithm", algorithm);
        cmd.Parameters.Add("@HashValue", SqlDbType.VarBinary, hashBytes.Length).Value = hashBytes;
        object? result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task<List<PasswordRecord>> GetRecentAsync(int top, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@Top)
                p.PasswordID,
                p.Plaintext,
                p.DateCreatedUtc,
                ha.Name        AS Algorithm,
                ph.HashValue
            FROM Passwords p
            JOIN PasswordHashes ph ON ph.PasswordID = p.PasswordID
            JOIN HashAlgorithms ha ON ha.AlgorithmID = ph.AlgorithmID
            ORDER BY p.PasswordID DESC;
            """;

        var records = new Dictionary<long, PasswordRecord>();

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Top", top);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            long pid = reader.GetInt64(0);
            if (!records.TryGetValue(pid, out PasswordRecord? rec))
            {
                rec = new PasswordRecord
                {
                    PasswordId = pid,
                    Plaintext = reader.GetString(1),
                    DateCreatedUtc = reader.GetDateTime(2)
                };
                records[pid] = rec;
            }
            rec.Hashes.Add(new HashRecord
            {
                AlgorithmName = reader.GetString(3),
                HashValue = (byte[])reader.GetValue(4)
            });
        }

        return records.Values.ToList();
    }

    public async Task<(long Passwords, long Hashes)> GetStatsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                (SELECT COUNT_BIG(*) FROM Passwords),
                (SELECT COUNT_BIG(*) FROM PasswordHashes);
            """;

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return (r.GetInt64(0), r.GetInt64(1));
    }
}
