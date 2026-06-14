using PasswordHashStorage.Desktop.Data;
using PasswordHashStorage.Desktop.Forms;

namespace PasswordHashStorage.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using ConnectionForm conn = new();
        if (conn.ShowDialog() != DialogResult.OK || conn.ConnectionString is null)
            return;

        var repo = new PasswordRepository(conn.ConnectionString);
        Application.Run(new MainForm(repo));
    }
}