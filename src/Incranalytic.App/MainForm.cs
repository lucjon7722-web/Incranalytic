using System.Globalization;
using Incranalytic.Core;

namespace Incranalytic.App;

public sealed class MainForm : Form
{
    private ProjectInput? project;
    private AnalysisResult? analysis;
    private readonly TreeView tree = new() { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.None };
    private readonly DataGridView grid = Grid();
    private readonly RichTextBox source = TextArea();
    private readonly RichTextBox details = TextArea();
    private readonly RichTextBox overview = TextArea();
    private readonly TextBox search = new() { Width = 280, PlaceholderText = "Filter values, formulas, systems…" };
    private readonly ToolStripStatusLabel status = new("Ready — open a game package or load the demo.");
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox expression = new() { Dock = DockStyle.Top, Text = "baseCost * costGrowth ^ level" };
    private readonly TextBox variables = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = "baseCost=15\r\ncostGrowth=1.15\r\nlevel=10\r\nbaseProduction=1\r\nowned=10\r\nmultiplier=2\r\ntotalApples=1000000" };
    private readonly Label result = new() { AutoSize = true, Text = "Enter a formula and calculate.", Font = new Font("Segoe UI", 12, FontStyle.Bold), Padding = new Padding(0, 10, 0, 10) };
    private readonly DataGridView curve = Grid();
    private readonly Button calculate = new() { Text = "Calculate + level table", AutoSize = true };
    private readonly bool demo;

    public MainForm(bool demo)
    {
        this.demo = demo;
        Text = "Incranalytic 0.1 — Incremental Systems Workbench";
        Width = 1380; Height = 880; MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(226, 224, 210);
        var menu = new MenuStrip { BackColor = Color.FromArgb(231, 229, 215), RenderMode = ToolStripRenderMode.System };
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("Open package or data…", null, (_, _) => OpenFile());
        file.DropDownItems.Add("Open folder…", null, (_, _) => OpenFolder());
        file.DropDownItems.Add("Load demonstration", null, async (_, _) => await LoadDemo());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Export analysis report…", null, (_, _) => ExportReport());
        file.DropDownItems.Add("Export original file…", null, (_, _) => ExportOriginal());
        file.DropDownItems.Add("Extract files to new folder…", null, (_, _) => Extract());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("Terms, privacy and coverage", null, (_, _) => MessageBox.Show(this, Legal, "Incranalytic — terms and coverage", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add(file); menu.Items.Add(help);
        var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(239, 237, 225), RenderMode = ToolStripRenderMode.System, Padding = new Padding(8, 5, 8, 5) };
        bar.Items.Add("Open…", null, (_, _) => OpenFile());
        bar.Items.Add("Folder…", null, (_, _) => OpenFolder());
        bar.Items.Add("Demo", null, async (_, _) => await LoadDemo());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Export report…", null, (_, _) => ExportReport());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripControlHost(search) { AutoSize = false, Width = 300, Height = 28 });
        search.TextChanged += (_, _) => Filter();
        var footer = new StatusStrip(); footer.Items.Add(status);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Color.FromArgb(178, 178, 168), FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(tree);
        split.Panel1.Controls.Add(new Label { Text = "PROJECT FILES", Dock = DockStyle.Top, Height = 30, ForeColor = Color.White, BackColor = Color.FromArgb(50, 65, 79), Padding = new Padding(8) });
        split.Panel2.Controls.Add(tabs);
        var findings = new TabPage("Systems & values") { Padding = new Padding(6) };
        var vertical = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 440 };
        vertical.Panel1.Controls.Add(grid); vertical.Panel2.Controls.Add(details); findings.Controls.Add(vertical);
        foreach (var col in new[] { ("System", 125), ("Name", 340), ("Value", 240), ("Confidence", 160) }) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = col.Item1, HeaderText = col.Item1, Width = col.Item2 });
        grid.SelectionChanged += (_, _) => ShowFinding();
        var srcTab = new TabPage("Source evidence"); srcTab.Controls.Add(source);
        var lab = new TabPage("Formula Lab") { Padding = new Padding(16) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        var title = new Label { Text = "Explore a calculation", Font = new Font(Font, FontStyle.Bold), AutoSize = true };
        layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        layout.Controls.Add(expression, 0, 1); layout.SetColumnSpan(expression, 2);
        layout.Controls.Add(new Label { Text = "Variables — one name=value per line", AutoSize = true }, 0, 2);
        layout.Controls.Add(new Label { Text = "Result as level changes (0–20)", AutoSize = true }, 1, 2);
        layout.Controls.Add(variables, 0, 3); layout.Controls.Add(curve, 1, 3);
        layout.Controls.Add(calculate, 0, 4); layout.Controls.Add(result, 0, 5); layout.SetColumnSpan(result, 2);
        curve.Columns.Add("level", "Level"); curve.Columns.Add("value", "Calculated value"); curve.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        calculate.Click += (_, _) => Calculate(); lab.Controls.Add(layout);
        var infoTab = new TabPage("Overview & coverage"); infoTab.Controls.Add(overview);
        tabs.TabPages.AddRange([findings, srcTab, lab, infoTab]);
        tree.AfterSelect += (_, e) => { if (e.Node?.Tag is GameFile entry) ShowSource(entry); };
        Controls.Add(split); Controls.Add(bar); Controls.Add(menu); Controls.Add(footer); MainMenuStrip = menu;
        overview.Text = "INCRANALYTIC\n\nInspect the numbers behind progression.\n\nOpen an APK, ZIP, extracted folder, JSON file, or source file. Use Demo for a fictional game with known formulas.\n\nThis first build reads structured JSON and numeric assignments in text/source files. Binary engine data and compiled code require further decoding; coverage is reported explicitly.\n\nSelect a finding for an explanation and source evidence. Formula Lab evaluates explicit formulas using variables you supply. All processing is local.";
        tabs.SelectedTab = infoTab;
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is Finding f) Clipboard.SetText(f.Value); };
        Shown += async (_, _) => { split.SplitterDistance = 260; vertical.SplitterDistance = Math.Max(250, vertical.Height - 230); if (this.demo) await LoadDemo(); };
        FormClosed += (_, _) => project?.Dispose();
    }

    private static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.Fixed3D, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, RowTemplate = { Height = 27 }, ColumnHeadersHeight = 30 };
    private static RichTextBox TextArea() => new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Consolas", 10), WordWrap = true, DetectUrls = false, Margin = new Padding(12) };
    private async void OpenFile() { using var dialog = new OpenFileDialog { Filter = "Supported inputs|*.apk;*.zip;*.xapk;*.json;*.txt;*.csv;*.cs;*.gd;*.js;*.lua|All files|*.*" }; if (dialog.ShowDialog(this) == DialogResult.OK) await LoadInput(dialog.FileName); }
    private async void OpenFolder() { using var dialog = new FolderBrowserDialog { Description = "Choose an extracted game or data folder" }; if (dialog.ShowDialog(this) == DialogResult.OK) await LoadInput(dialog.SelectedPath); }
    private Task LoadDemo() => LoadInput(Path.Combine(AppContext.BaseDirectory, "samples", "incremental-demo.json"));
    private async Task LoadInput(string path)
    {
        Enabled = false; UseWaitCursor = true; status.Text = "Reading files and investigating values…";
        ProjectInput? next = null;
        try
        {
            var loaded = await Task.Run(() => { next = ProjectInput.Open(path); return Analyzer.Analyze(next); });
            project?.Dispose(); project = next; analysis = loaded;
            tree.BeginUpdate(); tree.Nodes.Clear();
            var root = tree.Nodes.Add(project!.Name);
            foreach (var entry in project.Files)
            {
                TreeNode current = root;
                var parts = entry.Path.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    var node = current.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == parts[i]) ?? current.Nodes.Add(parts[i]);
                    current = node;
                }
                current.Tag = entry;
            }
            root.Expand(); tree.EndUpdate(); search.Clear(); Filter(); tabs.SelectedIndex = 0;
            overview.Text = $"{project.Name}\n\nInput: {project.InputKind}\nDetected engine: {project.Engine}\nFiles: {project.Files.Count:N0}\nText files analyzed: {analysis.ReadFiles}\nFiles not analyzed: {analysis.SkippedFiles}\nFindings: {analysis.Findings.Count}\n\nSHA-256: {project.Sha256 ?? "Not applicable to folders"}\n\nCOVERAGE\nThis build reads JSON numbers and explicit formulas plus numeric assignments in source/text. It lists binary assets but does not yet decode DEX, IL2CPP, Unity assets, or Godot resource packs. Engine detection is based on file names.\n\nWARNINGS\n{string.Join("\n", analysis.Warnings)}";
            status.Text = $"{project.Name}  |  {project.Engine}  |  {analysis.Findings.Count} findings  |  {analysis.ReadFiles} files analyzed, {analysis.SkippedFiles} not analyzed";
        }
        catch (Exception ex) { next?.Dispose(); MessageBox.Show(this, ex.Message, "Could not open input", MessageBoxButtons.OK, MessageBoxIcon.Error); status.Text = "Import failed; your original input was not changed."; }
        finally { Enabled = true; UseWaitCursor = false; }
    }
    private void Filter()
    {
        if (analysis == null) return;
        string term = search.Text.Trim();
        grid.DataSource = analysis.Findings.Where(f => string.Join(" ", f.System, f.Name, f.Value, f.Source).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void ShowFinding()
    {
        if (grid.CurrentRow?.DataBoundItem is not Finding f) return;
        details.Text = $"{f.Name}\n\nValue / formula: {f.Value}\nSystem: {f.System}\nConfidence: {f.Confidence}\n\n{f.Explanation}\n\nEvidence: {f.Source}\nLocation: {f.Location}\n\nDouble-click the value to copy it, or select Source evidence to inspect its file.";
        var file = project?.Files.FirstOrDefault(e => e.Path == f.Source); if (file != null) ShowSource(file);
        if (f.Confidence == "Explicit formula") expression.Text = f.Value;
    }
    private void ShowSource(GameFile file)
    {
        try { source.Text = $"FILE: {file.Path} ({file.Size:N0} bytes)\n\n" + (project?.ReadText(file) ?? "Binary or large file. Text preview is unavailable. Export the files for engine-specific analysis."); }
        catch (Exception ex) { source.Text = ex.Message; }
    }
    private void Calculate()
    {
        try
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in variables.Lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var pair = line.Split('=', 2); if (pair.Length != 2 || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Use name=number, one per line.");
                values[pair[0].Trim()] = value;
            }
            result.ForeColor = Color.DarkGreen; result.Text = "Result: " + Formula.Evaluate(expression.Text, values).ToString("G12", CultureInfo.InvariantCulture);
            curve.Rows.Clear();
            for (int level = 0; level <= 20; level++) { values["level"] = level; curve.Rows.Add(level, Formula.Evaluate(expression.Text, values).ToString("G12", CultureInfo.InvariantCulture)); }
        }
        catch (Exception ex) { result.ForeColor = Color.DarkRed; result.Text = ex.Message; curve.Rows.Clear(); }
    }
    private void ExportReport()
    {
        if (project == null || analysis == null) return;
        using var dialog = new SaveFileDialog { Filter = "HTML report|*.html|JSON evidence|*.json|CSV table|*.csv", FileName = "Incranalytic-report" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Reports.Export(dialog.FileName, project, analysis); status.Text = "Report saved: " + dialog.FileName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); }
    }
    private void ExportOriginal()
    {
        if (project == null || project.IsDirectory) return;
        using var dialog = new SaveFileDialog { FileName = project.Name };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { project.ExportOriginal(dialog.FileName); status.Text = "Original copied without modification."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); }
    }
    private void Extract()
    {
        if (project == null) return;
        using var dialog = new FolderBrowserDialog { Description = "Choose a folder; existing files will never be overwritten" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { project.ExtractAll(Path.Combine(dialog.SelectedPath, "Incranalytic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"))); status.Text = "Files exported."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); }
    }
    public const string Legal = "INCRANALYTIC 0.1 — DEVELOPMENT PREVIEW\n\nEducational purpose: Incranalytic is intended for learning about and investigating game systems. You are responsible for the files you import, the rights or permissions required to inspect them, and all actions you take using this software or its exports.\n\nUse the software in accordance with applicable law and the rights of others. Do not distribute third-party code or assets without permission.\n\nThis preview is provided as-is, without a guarantee of correctness, completeness, fitness, or uninterrupted operation. Analysis may be incomplete or incorrect. Limitations apply only to the extent permitted by applicable law. These terms do not remove rights that cannot legally be waived.\n\nPrivacy: This build processes files locally. It has no telemetry, uploads, accounts, or online AI service. Imported software is not executed.\n\nCoverage: JSON literals, explicit formula strings, and numeric text assignments are supported. Compiled engine code, encrypted files, and binary assets are listed but are not automatically decompiled in this version.\n\nThese preview terms require legal review before a public commercial release.";
}
