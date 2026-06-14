using Microsoft.Data.SqlClient;
using PasswordHashStorage.Desktop.Security;

namespace PasswordHashStorage.Desktop.Forms;

public sealed class ConnectionForm : Form
{
    private TextBox _serverBox = null!;
    private CheckBox _windowsAuthCheck = null!;
    private TextBox _userBox = null!;
    private TextBox _passBox = null!;
    private Button _connectBtn = null!;
    private CheckBox _rememberCheck = null!;
    private Label _statusLabel = null!;

    public string? ConnectionString { get; private set; }

    public ConnectionForm()
    {
        Text = "Connect to SQL Server";
        Size = new Size(420, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUI();
        LoadSavedSettings();
    }

    private void BuildUI()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 7,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Server:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        _serverBox = new TextBox { Text = "localhost", Dock = DockStyle.Fill };
        layout.Controls.Add(_serverBox, 1, 0);

        _windowsAuthCheck = new CheckBox { Text = "Windows Authentication", Checked = true, Dock = DockStyle.Fill };
        _windowsAuthCheck.CheckedChanged += (_, _) => UpdateAuthFields();
        layout.SetColumnSpan(_windowsAuthCheck, 2);
        layout.Controls.Add(_windowsAuthCheck, 0, 1);

        layout.Controls.Add(new Label { Text = "Username:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
        _userBox = new TextBox { Dock = DockStyle.Fill, Enabled = false };
        layout.Controls.Add(_userBox, 1, 2);

        layout.Controls.Add(new Label { Text = "Password:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
        _passBox = new TextBox { Dock = DockStyle.Fill, PasswordChar = '*', Enabled = false };
        layout.Controls.Add(_passBox, 1, 3);

        _rememberCheck = new CheckBox { Text = "Remember settings (encrypted)", Dock = DockStyle.Fill };
        layout.SetColumnSpan(_rememberCheck, 2);
        layout.Controls.Add(_rememberCheck, 0, 4);

        _statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.Red, AutoSize = false };
        layout.SetColumnSpan(_statusLabel, 2);
        layout.Controls.Add(_statusLabel, 0, 5);

        _connectBtn = new Button { Text = "Connect", Dock = DockStyle.Right, Width = 100 };
        _connectBtn.Click += ConnectBtn_Click;
        layout.SetColumnSpan(_connectBtn, 2);
        layout.Controls.Add(_connectBtn, 0, 6);

        Controls.Add(layout);
        AcceptButton = _connectBtn;
    }

    private void LoadSavedSettings()
    {
        var s = SettingsManager.Load();
        if (s is null) return;

        _serverBox.Text = s.Server;
        _windowsAuthCheck.Checked = s.WindowsAuth;
        _userBox.Text = s.Username ?? string.Empty;
        _passBox.Text = s.Password ?? string.Empty;
        _rememberCheck.Checked = true;
        UpdateAuthFields();
    }

    private void UpdateAuthFields()
    {
        bool sql = !_windowsAuthCheck.Checked;
        _userBox.Enabled = sql;
        _passBox.Enabled = sql;
    }

    private async void ConnectBtn_Click(object? sender, EventArgs e)
    {
        _connectBtn.Enabled = false;
        _statusLabel.ForeColor = Color.Black;
        _statusLabel.Text = "Connecting...";

        try
        {
            string cs = BuildConnectionString();
            await using SqlConnection conn = new(cs);
            await conn.OpenAsync();

            await Data.DatabaseSetup.InitializeAsync(cs);

            if (_rememberCheck.Checked)
            {
                SettingsManager.Save(new ConnectionSettings
                {
                    Server = _serverBox.Text.Trim(),
                    WindowsAuth = _windowsAuthCheck.Checked,
                    Username = _userBox.Text.Trim(),
                    Password = _passBox.Text
                });
            }
            else
            {
                SettingsManager.Delete();
            }

            ConnectionString = cs;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Color.Red;
            _statusLabel.Text = ex.Message;
            _connectBtn.Enabled = true;
        }
    }

    private string BuildConnectionString()
    {
        SqlConnectionStringBuilder b = new()
        {
            DataSource = _serverBox.Text.Trim(),
            InitialCatalog = "PHP",
            TrustServerCertificate = true
        };

        if (_windowsAuthCheck.Checked)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID = _userBox.Text.Trim();
            b.Password = _passBox.Text;
        }

        return b.ToString();
    }
}
