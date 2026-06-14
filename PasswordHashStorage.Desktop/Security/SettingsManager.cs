using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordHashStorage.Desktop.Security;

public sealed class ConnectionSettings
{
    public string Server { get; set; } = "localhost";
    public bool WindowsAuth { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PasswordHashStorage",
        "connection.dat");

    public static ConnectionSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            byte[] encrypted = File.ReadAllBytes(SettingsPath);
            byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<ConnectionSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(ConnectionSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

        string dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(SettingsPath, encrypted);
    }

    public static void Delete()
    {
        if (File.Exists(SettingsPath))
            File.Delete(SettingsPath);
    }
}
