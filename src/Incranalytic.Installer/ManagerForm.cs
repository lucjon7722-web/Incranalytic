using System.Diagnostics;
using Incranalytic.Distribution;
using Microsoft.Win32;

namespace Incranalytic.Installer;

public sealed class ManagerForm : Form
{
    readonly ComboBox versions = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox language = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox token = new() { Width = 400, UseSystemPasswordChar = true, PlaceholderText = "GitHub token for private repository access" };
    readonly CheckBox remember = new() { AutoSize = true, Text = "Remember in Windows Credential Manager" };
    readonly CheckBox previews = new() { AutoSize = true, Text = "Include preview releases" };
    readonly Button refresh = new() { Text = "Refresh versions", AutoSize = true };
    readonly Button install = new() { Text = "Install selected version…", AutoSize = true, Enabled = false };
    readonly Button cancel = new() { Text = "Cancel download", AutoSize = true, Enabled = false };
    readonly Button forget = new() { Text = "Forget saved token", AutoSize = true };
    readonly RichTextBox notes = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false, BackColor = Color.White, Font = new("Consolas", 10) };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 55, Padding = new(10), Text = "Refresh to choose a published version." };
    readonly Label installed = new() { AutoSize = true };
    readonly ProgressBar progress = new() { Dock = DockStyle.Bottom, Height = 20 };
    readonly ReleaseClient client = new();
    CancellationTokenSource? operation;
    bool busy, launching;
    const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{82D861BC-A02B-4C41-B9EA-2878D66918B3}_is1";
    static (Version? Version, string Directory) Installed()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        Version.TryParse(key?.GetValue("DisplayVersion") as string, out var version);
        return (version, key?.GetValue("InstallLocation") as string ?? "");
    }
    public ManagerForm(bool verify = false)
    {
        Text = "Incranalytic — Install and Manage Versions"; Width = 1000; Height = 800; MinimumSize = new(900, 650); StartPosition = FormStartPosition.CenterScreen;
        Font = new("Segoe UI", 9); BackColor = Color.FromArgb(232, 230, 216);
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 255, Padding = new(12), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        top.Controls.Add(new Label { Text = "INCRANALYTIC VERSION MANAGER", Font = new(Font, FontStyle.Bold), AutoSize = true });
        installed.Text = "Installed studio: " + (Installed().Version?.ToString() ?? "not registered for this Windows account"); top.Controls.Add(installed);
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        language.Items.Add("English"); language.SelectedIndex = 0;
        row.Controls.AddRange([new Label { Text = "Version", AutoSize = true, Padding = new(0, 5, 0, 0) }, versions, refresh, language, previews]); top.Controls.Add(row);
        top.Controls.Add(new Label { Text = "Private repository: enter a repository-scoped GitHub token with Contents: Read access.", AutoSize = true });
        var auth = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; auth.Controls.AddRange([token, forget]); top.Controls.Add(auth); top.Controls.Add(remember);
        var help = new Button { Text = "Private-access help", AutoSize = true };
        var releases = new Button { Text = "Open GitHub releases", AutoSize = true };
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; actions.Controls.AddRange([install, cancel, releases, help]); top.Controls.Add(actions);
        top.Controls.Add(new Label { Text = "Keep this manager. It discovers future releases; each installation still uses the normal setup wizard.", AutoSize = true });
        Controls.Add(notes); Controls.Add(top); Controls.Add(progress); Controls.Add(status);
        versions.SelectedIndexChanged += (_, _) => SelectVersion();
        refresh.Click += async (_, _) => await RefreshAsync();
        install.Click += async (_, _) => await InstallAsync();
        cancel.Click += (_, _) => operation?.Cancel();
        releases.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/" + ReleaseClient.Repository + "/releases") { UseShellExecute = true });
        help.Click += (_, _) => MessageBox.Show(this,
            "This repository is private. Your browser's GitHub sign-in cannot be reused by this app.\n\nCreate a fine-grained personal access token in GitHub Settings → Developer settings → Personal access tokens. Select only the Incranalytic repository and Contents: Read permission. Paste it here, then Refresh.\n\nRemember is optional and initially off. It saves the token in Windows Credential Manager for this Windows account. Forget removes that saved token. Tokens are sent only to api.github.com and are never included in installers or project files.\n\nStudio projects remain local. Only this optional manager contacts GitHub for releases and downloads.", "Private repository access");
        forget.Click += (_, _) => { try { TokenStore.Forget(); token.Clear(); remember.Checked = false; status.Text = "Saved token removed. Public releases can still be requested without a token."; } catch (Exception ex) { status.Text = ex.Message; } };
        if (!verify)
        {
            try { var saved = TokenStore.Load(); if (!string.IsNullOrEmpty(saved)) { token.Text = saved; remember.Checked = true; } } catch { status.Text = "Could not read saved credentials. You can enter a token for this session."; }
            Shown += async (_, _) => await RefreshAsync();
        }
        FormClosing += (_, e) => { if (busy && !launching) { operation?.Cancel(); e.Cancel = true; status.Text = "Cancelling… close the manager once the operation has stopped."; } };
        FormClosed += (_, _) => { operation?.Dispose(); client.Dispose(); };
    }
    void SetBusy(bool value)
    {
        busy = value; refresh.Enabled = versions.Enabled = token.Enabled = remember.Enabled = forget.Enabled = previews.Enabled = !value;
        install.Enabled = !value && versions.SelectedItem is InstallableRelease; cancel.Enabled = value;
    }
    void ShowReleases(List<InstallableRelease> releases)
    {
        versions.Items.Clear(); foreach (var release in releases) versions.Items.Add(release);
        if (versions.Items.Count > 0) versions.SelectedIndex = 0;
        else { notes.Text = "No installable releases were found. A release needs a matching versioned setup EXE and SHA-256 file."; install.Enabled = false; }
    }
    void SelectVersion()
    {
        if (versions.SelectedItem is not InstallableRelease r) return;
        notes.Text = r.Name + "\n\n" + r.Tag + " · installer " + (r.Installer.Size / 1048576d).ToString("F1") + " MB\n\n" + r.Notes;
        install.Enabled = !busy;
    }
    async Task RefreshAsync()
    {
        if (busy) return; operation?.Dispose(); operation = new CancellationTokenSource(); SetBusy(true); status.Text = "Requesting published versions from GitHub…"; progress.Value = 0;
        try
        {
            var releases = await client.ListAsync(token.Text, previews.Checked, operation.Token); operation.Token.ThrowIfCancellationRequested(); ShowReleases(releases);
            if (remember.Checked && token.Text.Length > 0) TokenStore.Save(token.Text.Trim()); else TokenStore.Forget();
            status.Text = $"{releases.Count} installable versions available. Choose a version, then Install.";
        }
        catch (OperationCanceledException) { status.Text = "Refresh cancelled."; }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { SetBusy(false); }
    }
    static bool StudioRunning() { var processes = Process.GetProcessesByName("Incranalytic"); try { return processes.Length > 0; } finally { foreach (var p in processes) p.Dispose(); } }
    async Task InstallAsync()
    {
        if (busy || versions.SelectedItem is not InstallableRelease release) return;
        var current = Installed();
        if (current.Version != null && release.Version < current.Version && MessageBox.Show(this,
            $"Replace studio {current.Version} with {release.Tag}?\n\nAn older studio may not open newer project formats. Keep your project backups. The version manager remains available so you can switch again.", "Install older version", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        operation?.Dispose(); operation = new CancellationTokenSource(); SetBusy(true); progress.Value = 0;
        status.Text = "Downloading and verifying " + release.Tag + "…";
        try
        {
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Incranalytic", "cache", "versions");
            string file = await client.DownloadVerifiedAsync(release, token.Text, cache, new Progress<int>(p => { if (!IsDisposed) progress.Value = p; }), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            status.Text = "Installer SHA-256 verified.";
            if (StudioRunning()) { MessageBox.Show(this, "The installer is ready. Save your work and close Incranalytic Studio, then click Install again. The verified download is cached.", "Close studio before installation"); return; }
            var start = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file)! };
            start.ArgumentList.Add("/LANG=english");
            if (current.Directory.Length > 0 && Path.IsPathFullyQualified(current.Directory)) start.ArgumentList.Add("/DIR=" + current.Directory);
            operation.Token.ThrowIfCancellationRequested();
            _ = Process.Start(start) ?? throw new InvalidOperationException("Windows could not start the verified installer.");
            launching = true; Close();
        }
        catch (OperationCanceledException) { status.Text = "Download cancelled; no installer was launched."; }
        catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Installation did not start", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!IsDisposed) SetBusy(false); }
    }
    public void Verify(string reportPath)
    {
        var checks = new List<string>(); void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add("PASS: " + message); }
        Check(language.Items.Count == 1 && language.Text == "English", "Manager offers English language");
        var asset = new ReleaseAsset(1, "Incranalytic-0.3.0-Setup.exe", 100, ""); var checksum = new ReleaseAsset(2, "Incranalytic-0.3.0-Setup.sha256", 100, "");
        ShowReleases([new("v0.3.0", new Version(0, 3, 0), "New release", "New notes", false, asset, checksum), new("v0.2.0", new Version(0, 2, 0), "Old release", "Old notes", false, asset, checksum)]);
        Check(versions.Items.Count == 2 && install.Enabled, "Version picker supports multiple releases");
        versions.SelectedIndex = 1; Check(notes.Text.Contains("Old notes"), "Selecting a version changes the release notes");
        SetBusy(true); Check(!install.Enabled && cancel.Enabled, "Downloads disable install and enable cancellation"); SetBusy(false);
        Check(token.UseSystemPasswordChar && !remember.Checked, "Private access is masked and remembering is opt-in");
        string target = TokenStore.Target + "/test-" + Guid.NewGuid().ToString("N");
        try { TokenStore.Save("dummy-test-token", target); Check(TokenStore.Load(target) == "dummy-test-token", "Windows credential round trip"); }
        finally { TokenStore.Forget(target); }
        Check(TokenStore.Load(target) == null, "Forget removes only the manager test credential");
        File.WriteAllLines(reportPath, checks);
    }
}
