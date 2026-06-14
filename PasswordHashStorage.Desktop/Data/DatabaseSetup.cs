using Microsoft.Data.SqlClient;

namespace PasswordHashStorage.Desktop.Data;

public static class DatabaseSetup
{
    public static async Task InitializeAsync(string connectionString)
    {
        await using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        await ExecuteAsync(conn, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HashAlgorithms')
            CREATE TABLE HashAlgorithms
            (
                AlgorithmID      SMALLINT IDENTITY(1,1) NOT NULL,
                Name             VARCHAR(50)            NOT NULL,
                HashLengthBytes  SMALLINT               NOT NULL,
                CONSTRAINT PK_HashAlgorithms PRIMARY KEY (AlgorithmID),
                CONSTRAINT UQ_HashAlgorithms_Name UNIQUE (Name)
            );
            """);

        await ExecuteAsync(conn, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Passwords')
            CREATE TABLE Passwords
            (
                PasswordID      BIGINT IDENTITY(1,1) NOT NULL,
                Plaintext       NVARCHAR(512)        NOT NULL,
                DateCreatedUtc  DATETIME2            NOT NULL
                    CONSTRAINT DF_Passwords_DateCreatedUtc DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Passwords PRIMARY KEY (PasswordID)
            );
            """);

        await ExecuteAsync(conn, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PasswordHashes')
            CREATE TABLE PasswordHashes
            (
                PasswordHashID  BIGINT   IDENTITY(1,1) NOT NULL,
                PasswordID      BIGINT                  NOT NULL,
                AlgorithmID     SMALLINT                NOT NULL,
                HashValue       VARBINARY(64)           NOT NULL,
                CONSTRAINT PK_PasswordHashes PRIMARY KEY (PasswordHashID),
                CONSTRAINT FK_PasswordHashes_Password
                    FOREIGN KEY (PasswordID) REFERENCES Passwords(PasswordID)
            );
            """);

        await ExecuteAsync(conn, """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'UX_PasswordHashes_Algorithm_Hash'
            )
            CREATE UNIQUE INDEX UX_PasswordHashes_Algorithm_Hash
                ON PasswordHashes(AlgorithmID, HashValue);
            """);

        await ExecuteAsync(conn, """
            IF NOT EXISTS (SELECT 1 FROM HashAlgorithms WHERE Name = 'MD5')
            INSERT INTO HashAlgorithms(Name, HashLengthBytes) VALUES
                ('MD5',    16),
                ('SHA1',   20),
                ('SHA256', 32),
                ('SHA384', 48),
                ('SHA512', 64);
            """);
    }

    private static async Task ExecuteAsync(SqlConnection conn, string sql)
    {
        await using SqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
