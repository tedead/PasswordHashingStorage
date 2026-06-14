using PasswordHashStorage.Desktop.Data;
using PasswordHashStorage.Desktop.Models;
using PasswordHashStorage.Desktop.Security;

namespace PasswordHashStorage.Desktop.Forms;

public sealed class MainForm : Form
{
    private readonly PasswordRepository _repo;
    private Dictionary<string, short> _algorithmMap = new();
    private CancellationTokenSource? _cts;

    // Controls
    private TabControl _tabs = null!;
    private DataGridView _recentGrid = null!;

    // Generator tab
    private RadioButton _randomRadio = null!, _mutationRadio = null!, _exhaustiveRadio = null!;
    private NumericUpDown _lengthMin = null!, _lengthMax = null!, _countLimit = null!;
    private CheckBox _chkLower = null!, _chkUpper = null!, _chkDigits = null!, _chkSymbols = null!;
    private TextBox _wordlistBox = null!;
    private Button _browseBtn = null!;
    private CheckedListBox _algList = null!;
    private Button _generateBtn = null!, _cancelBtn = null!;
    private ProgressBar _progressBar = null!;
    private Label _progressLabel = null!;

    // Lookup tab
    private TextBox _hashInputBox = null!;
    private ComboBox _algCombo = null!;
    private Button _autoDetectBtn = null!, _lookupBtn = null!;
    private Label _lookupResultLabel = null!;

    public MainForm(PasswordRepository repo)
    {
        _repo = repo;
        Text = "Password Hash Storage";
        Size = new Size(900, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BuildUI();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _algorithmMap = await _repo.GetAlgorithmMapAsync();
        foreach (string alg in HashCalculator.SupportedAlgorithms)
            _algList.Items.Add(alg, true);
        foreach (string alg in HashCalculator.SupportedAlgorithms)
            _algCombo.Items.Add(alg);
        _algCombo.SelectedIndex = 2; // SHA256 default
        await RefreshRecentAsync();
    }

    private void BuildUI()
    {
        _tabs = new TabControl { Dock = DockStyle.Fill };

        _tabs.TabPages.Add(BuildGeneratorTab());
        _tabs.TabPages.Add(BuildLookupTab());
        _tabs.TabPages.Add(BuildBrowseTab());

        var statusStrip = new StatusStrip();
        var tssl = new ToolStripStatusLabel("Ready");
        statusStrip.Items.Add(tssl);

        Controls.Add(_tabs);
        Controls.Add(statusStrip);

        Tag = tssl;

        Load += async (_, _) => await UpdateStatsLabelAsync((ToolStripStatusLabel)Tag!);
    }

    private async Task UpdateStatsLabelAsync(ToolStripStatusLabel label)
    {
        try
        {
            var (pw, hsh) = await _repo.GetStatsAsync();
            label.Text = $"Passwords: {pw:N0}   Hashes: {hsh:N0}";
        }
        catch { }
    }

    // ── Generator Tab ──────────────────────────────────────────────────────────

    private TabPage BuildGeneratorTab()
    {
        var page = new TabPage("Generate & Store");
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 3, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        outer.Controls.Add(BuildModeGroup(), 0, 0);
        outer.Controls.Add(BuildAlgorithmGroup(), 0, 1);
        outer.Controls.Add(BuildGeneratorActions(), 0, 2);

        page.Controls.Add(outer);
        return page;
    }

    private GroupBox BuildModeGroup()
    {
        var g = new GroupBox { Text = "Generation Mode", Dock = DockStyle.Fill, Height = 200 };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _randomRadio = new RadioButton { Text = "Random passwords", Checked = true, AutoSize = true };
        _mutationRadio = new RadioButton { Text = "Dictionary mutations", AutoSize = true };
        _exhaustiveRadio = new RadioButton { Text = "Exhaustive (short)", AutoSize = true };

        _randomRadio.CheckedChanged += ModeChanged;
        _mutationRadio.CheckedChanged += ModeChanged;

        layout.Controls.Add(_randomRadio, 0, 0);
        layout.Controls.Add(_mutationRadio, 0, 1);
        layout.Controls.Add(_exhaustiveRadio, 0, 2);

        layout.Controls.Add(new Label { Text = "Min length:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
        var row = new FlowLayoutPanel { AutoSize = true };
        _lengthMin = new NumericUpDown { Minimum = 1, Maximum = 64, Value = 8, Width = 60 };
        _lengthMax = new NumericUpDown { Minimum = 1, Maximum = 64, Value = 16, Width = 60 };
        row.Controls.AddRange(new Control[] { _lengthMin, new Label { Text = "–", Width = 12, TextAlign = ContentAlignment.MiddleCenter }, _lengthMax });
        layout.Controls.Add(row, 1, 3);

        layout.Controls.Add(new Label { Text = "Max count:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
        _countLimit = new NumericUpDown { Minimum = 1, Maximum = 10_000_000, Value = 1000, Width = 100, ThousandsSeparator = true };
        layout.Controls.Add(_countLimit, 1, 4);

        var charsets = new FlowLayoutPanel { AutoSize = true };
        _chkLower   = new CheckBox { Text = "a-z", Checked = true, AutoSize = true };
        _chkUpper   = new CheckBox { Text = "A-Z", Checked = true, AutoSize = true };
        _chkDigits  = new CheckBox { Text = "0-9", Checked = true, AutoSize = true };
        _chkSymbols = new CheckBox { Text = "Symbols", AutoSize = true };
        charsets.Controls.AddRange(new Control[] { _chkLower, _chkUpper, _chkDigits, _chkSymbols });
        layout.Controls.Add(new Label { Text = "Charsets:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 5);
        layout.Controls.Add(charsets, 1, 5);

        var dictRow = new FlowLayoutPanel { AutoSize = true };
        _wordlistBox = new TextBox { Width = 260, Enabled = false };
        _browseBtn   = new Button { Text = "Browse…", Width = 80, Enabled = false };
        _browseBtn.Click += BrowseWordlist;
        dictRow.Controls.AddRange(new Control[] { new Label { Text = "Wordlist:", AutoSize = true }, _wordlistBox, _browseBtn });
        layout.SetColumnSpan(dictRow, 2);
        // Add wordlist row inside char group area — skip for now, added below
        g.Controls.Add(layout);
        return g;
    }

    private GroupBox BuildAlgorithmGroup()
    {
        var g = new GroupBox { Text = "Hash Algorithms", Dock = DockStyle.Fill };
        _algList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        g.Controls.Add(_algList);
        return g;
    }

    private Panel BuildGeneratorActions()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 70 };

        _progressBar = new ProgressBar { Left = 8, Top = 8, Width = 500, Height = 22, Style = ProgressBarStyle.Continuous };
        _progressLabel = new Label { Left = 8, Top = 34, Width = 500, Text = "" };
        _generateBtn = new Button { Text = "Generate & Store", Left = 520, Top = 8, Width = 150, Height = 28 };
        _cancelBtn   = new Button { Text = "Cancel", Left = 680, Top = 8, Width = 80, Height = 28, Enabled = false };

        _generateBtn.Click += GenerateBtn_Click;
        _cancelBtn.Click   += (_, _) => _cts?.Cancel();

        // Wordlist row
        var wlLabel = new Label { Text = "Wordlist:", Left = 520, Top = 40, Width = 60 };
        _wordlistBox = new TextBox { Left = 584, Top = 38, Width = 160 };
        _browseBtn   = new Button { Text = "…", Left = 748, Top = 37, Width = 30 };
        _browseBtn.Click += BrowseWordlist;

        panel.Controls.AddRange(new Control[] { _progressBar, _progressLabel, _generateBtn, _cancelBtn, wlLabel, _wordlistBox, _browseBtn });
        return panel;
    }

    private void ModeChanged(object? sender, EventArgs e)
    {
        bool dict = _mutationRadio.Checked;
        _wordlistBox.Enabled = dict;
        _browseBtn.Enabled   = dict;
        bool rnd = _randomRadio.Checked;
        _chkLower.Enabled   = rnd || _exhaustiveRadio.Checked;
        _chkUpper.Enabled   = rnd || _exhaustiveRadio.Checked;
        _chkDigits.Enabled  = rnd || _exhaustiveRadio.Checked;
        _chkSymbols.Enabled = rnd;
    }

    private void BrowseWordlist(object? sender, EventArgs e)
    {
        using OpenFileDialog dlg = new() { Filter = "Text files|*.txt|All files|*.*" };
        if (dlg.ShowDialog() == DialogResult.OK)
            _wordlistBox.Text = dlg.FileName;
    }

    private async void GenerateBtn_Click(object? sender, EventArgs e)
    {
        var selectedAlgs = _algList.CheckedItems.Cast<string>().ToList();
        if (selectedAlgs.Count == 0)
        {
            MessageBox.Show("Select at least one hash algorithm.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _generateBtn.Enabled = false;
        _cancelBtn.Enabled   = true;
        _progressBar.Value   = 0;
        _progressLabel.Text  = "Starting…";

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            int maxCount = (int)_countLimit.Value;
            IEnumerable<string> passwords = BuildPasswordSource(maxCount);

            var algMap = _algorithmMap;
            long inserted = 0;
            var batch = new List<(long, short, byte[])>(500);

            var progress = new Progress<string>(msg => _progressLabel.Text = msg);

            await Task.Run(async () =>
            {
                int count = 0;
                foreach (string pw in passwords)
                {
                    ct.ThrowIfCancellationRequested();

                    long pid = await _repo.InsertPasswordAsync(pw, ct);

                    foreach (string alg in selectedAlgs)
                    {
                        var computed = HashCalculator.Compute(pw, alg);
                        if (algMap.TryGetValue(alg, out short aid))
                            batch.Add((pid, aid, computed.HashValue));
                    }

                    if (batch.Count >= 500)
                    {
                        await _repo.BulkInsertHashesAsync(batch, ct);
                        inserted += batch.Count;
                        batch.Clear();
                    }

                    count++;
                    ((IProgress<string>)progress).Report($"Generated {count:N0} passwords, {inserted:N0} hashes stored…");
                }

                if (batch.Count > 0)
                {
                    await _repo.BulkInsertHashesAsync(batch, ct);
                    inserted += batch.Count;
                }

                ((IProgress<string>)progress).Report($"Done — {count:N0} passwords, {inserted:N0} hashes.");
            }, ct);

            await RefreshRecentAsync();
            if (Tag is ToolStripStatusLabel tssl)
                await UpdateStatsLabelAsync(tssl);
        }
        catch (OperationCanceledException)
        {
            _progressLabel.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _progressLabel.Text = "Error occurred.";
        }
        finally
        {
            _generateBtn.Enabled = true;
            _cancelBtn.Enabled   = false;
        }
    }

    private IEnumerable<string> BuildPasswordSource(int maxCount)
    {
        if (_randomRadio.Checked)
        {
            int minLen = (int)_lengthMin.Value;
            int maxLen = (int)_lengthMax.Value;
            for (int i = 0; i < maxCount; i++)
            {
                int len = minLen == maxLen ? minLen : minLen + (i % (maxLen - minLen + 1));
                yield return PasswordGenerator.GenerateRandom(len, _chkLower.Checked, _chkUpper.Checked, _chkDigits.Checked, _chkSymbols.Checked);
            }
        }
        else if (_mutationRadio.Checked)
        {
            string path = _wordlistBox.Text.Trim();
            if (!File.Exists(path))
                throw new FileNotFoundException($"Wordlist not found: {path}");

            int count = 0;
            foreach (string word in File.ReadLines(path))
            {
                foreach (string mutation in PasswordGenerator.GenerateMutations(word))
                {
                    if (count >= maxCount) yield break;
                    yield return mutation;
                    count++;
                }
            }
        }
        else // exhaustive
        {
            string charset = "";
            if (_chkLower.Checked)  charset += "abcdefghijklmnopqrstuvwxyz";
            if (_chkUpper.Checked)  charset += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (_chkDigits.Checked) charset += "0123456789";
            if (charset.Length == 0) charset = "abcdefghijklmnopqrstuvwxyz";

            int length = (int)_lengthMin.Value;
            int count = 0;
            foreach (string pw in PasswordGenerator.GenerateExhaustive(charset, length))
            {
                if (count >= maxCount) yield break;
                yield return pw;
                count++;
            }
        }
    }

    // ── Lookup Tab ─────────────────────────────────────────────────────────────

    private TabPage BuildLookupTab()
    {
        var page = new TabPage("Hash Lookup");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 5, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Hash (hex):", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        _hashInputBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
        layout.Controls.Add(_hashInputBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Algorithm:", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
        var row = new FlowLayoutPanel { AutoSize = true };
        _algCombo = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        _autoDetectBtn = new Button { Text = "Auto-detect", AutoSize = true };
        _autoDetectBtn.Click += AutoDetect_Click;
        row.Controls.AddRange(new Control[] { _algCombo, _autoDetectBtn });
        layout.Controls.Add(row, 1, 1);

        _lookupBtn = new Button { Text = "Look Up", AutoSize = true };
        _lookupBtn.Click += LookupBtn_Click;
        layout.Controls.Add(_lookupBtn, 1, 2);

        _lookupResultLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            ForeColor = Color.DarkGreen
        };
        layout.SetColumnSpan(_lookupResultLabel, 2);
        layout.Controls.Add(_lookupResultLabel, 0, 3);

        page.Controls.Add(layout);
        return page;
    }

    private void AutoDetect_Click(object? sender, EventArgs e)
    {
        string hex = _hashInputBox.Text.Trim();
        var detected = HashCalculator.DetectAlgorithms(hex);
        if (detected.Count == 0)
        {
            _lookupResultLabel.ForeColor = Color.Red;
            _lookupResultLabel.Text = "Cannot detect algorithm — check the hash value.";
            return;
        }
        int idx = _algCombo.Items.IndexOf(detected[0]);
        if (idx >= 0) _algCombo.SelectedIndex = idx;
        _lookupResultLabel.ForeColor = Color.DarkBlue;
        _lookupResultLabel.Text = $"Detected: {string.Join(", ", detected)}";
    }

    private async void LookupBtn_Click(object? sender, EventArgs e)
    {
        if (_algCombo.SelectedItem is not string alg) return;
        string hex = _hashInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(hex)) return;

        _lookupBtn.Enabled = false;
        _lookupResultLabel.Text = "Searching…";

        try
        {
            string? plaintext = await _repo.LookupPlaintextAsync(hex, alg);
            if (plaintext is null)
            {
                _lookupResultLabel.ForeColor = Color.Red;
                _lookupResultLabel.Text = "Not found in database.";
            }
            else
            {
                _lookupResultLabel.ForeColor = Color.DarkGreen;
                _lookupResultLabel.Text = $"Found: {plaintext}";
            }
        }
        catch (Exception ex)
        {
            _lookupResultLabel.ForeColor = Color.Red;
            _lookupResultLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _lookupBtn.Enabled = true;
        }
    }

    // ── Browse Tab ─────────────────────────────────────────────────────────────

    private TabPage BuildBrowseTab()
    {
        var page = new TabPage("Browse Records");

        _recentGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false
        };
        _recentGrid.Columns.Add("Plaintext", "Password");
        _recentGrid.Columns.Add("Algorithm", "Algorithm");
        _recentGrid.Columns.Add("HashHex",   "Hash (hex)");
        _recentGrid.Columns.Add("Created",   "Created (UTC)");

        var refreshBtn = new Button { Text = "Refresh", Dock = DockStyle.Bottom, Height = 30 };
        refreshBtn.Click += async (_, _) => await RefreshRecentAsync();

        page.Controls.Add(_recentGrid);
        page.Controls.Add(refreshBtn);
        return page;
    }

    private async Task RefreshRecentAsync()
    {
        try
        {
            var records = await _repo.GetRecentAsync(500);
            _recentGrid.Rows.Clear();
            foreach (var rec in records)
            {
                foreach (var hash in rec.Hashes)
                {
                    _recentGrid.Rows.Add(rec.Plaintext, hash.AlgorithmName, hash.HashHex, rec.DateCreatedUtc.ToString("u"));
                }
            }
        }
        catch { }
    }
}
