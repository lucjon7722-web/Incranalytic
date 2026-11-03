using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Incranalytic.Core;

namespace Incranalytic.App;

public sealed class StudioForm : Form
{
    BindingList<StudioValue> values = new();
    readonly DataGridView editor = MakeGrid(false), history = MakeGrid(true);
    readonly NumericUpDown interval = new() { DecimalPlaces = 3, Minimum = .001m, Maximum = 3600, Value = 1, Width = 85 };
    readonly NumericUpDown precision = new() { Minimum = 1, Maximum = 1000, Value = 34, Width = 70 };
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205 };
    readonly ComboBox plotted = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly ToolStripStatusLabel status = new("New project — define your symbols in Values.");
    readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    readonly Stopwatch clock = new();
    readonly Panel graph = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly Panel diagram = new() { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
    readonly List<(string Time, Dictionary<string, ScientificNumber> State)> samples = [];
    StudioSession? session;
    bool dirty, loading;
    string? projectPath;
    double accumulator;

    public StudioForm()
    {
        Text = "Incranalytic Studio 0.2 — Untitled"; Width = 1350; Height = 880; MinimumSize = new(1000, 660);
        StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 9); BackColor = Color.FromArgb(230, 228, 214);
        var menu = new MenuStrip { BackColor = BackColor, RenderMode = ToolStripRenderMode.System };
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("New blank system", null, (_, _) => NewProject());
        file.DropDownItems.Add("Import project…", null, (_, _) => Open());
        file.DropDownItems.Add("Save project…", null, (_, _) => Save());
        file.DropDownItems.Add("Export C# for Unity…", null, (_, _) => ExportUnity());
        file.DropDownItems.Add("Export simulation CSV…", null, (_, _) => ExportCsv());
        file.DropDownItems.Add(new ToolStripSeparator()); file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("Terms and privacy", null, (_, _) => MessageBox.Show(this, Terms, "Incranalytic terms"));
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(this, "Incranalytic Studio 0.2\nA base studio for human-authored incremental systems.\nNo AI service or account is used by this software.\nDevelopment has used AI assistance.\n\nWindows x64 development preview; installer is unsigned.", "About Incranalytic"));
        menu.Items.AddRange([file, help]);
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(239, 237, 225), RenderMode = ToolStripRenderMode.System, Padding = new(5) };
        toolbar.Items.Add("New", null, (_, _) => NewProject()); toolbar.Items.Add("Import…", null, (_, _) => Open()); toolbar.Items.Add("Save…", null, (_, _) => Save());
        toolbar.Items.Add(new ToolStripSeparator()); toolbar.Items.Add("Run", null, (_, _) => Run()); toolbar.Items.Add("Pause", null, (_, _) => Pause()); toolbar.Items.Add("Step", null, (_, _) => Step()); toolbar.Items.Add("Reset", null, (_, _) => Reset());
        toolbar.Items.Add(new ToolStripSeparator()); toolbar.Items.Add("Unity C#…", null, (_, _) => ExportUnity());
        var config = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new(8), WrapContents = false, BackColor = BackColor };
        mode.Items.AddRange(["Per second: gain × interval", "Per tick: gain per interval"]); mode.SelectedIndex = 0;
        config.Controls.AddRange([new Label { Text = "Interval (seconds)", AutoSize = true, Padding = new(0, 5, 0, 0) }, interval, mode, new Label { Text = "Significant digits", AutoSize = true, Padding = new(10, 5, 0, 0) }, precision]);
        var valuesTab = new TabPage("Values");
        var intro = new Label { Dock = DockStyle.Top, Height = 65, Padding = new(12), Text = "Define the symbols in your formulas. Letters have no preset meaning. Names are labels; formulas use symbols.\nGain is added to the value each step (or multiplied by the interval in per-second mode). Use 0 for a constant." };
        var valueBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new(8) };
        var example = new Button { Text = "Load simple example", AutoSize = true }; example.Click += (_, _) => Example();
        var remove = new Button { Text = "Remove selected value", AutoSize = true }; remove.Click += (_, _) => { if (session == null && editor.CurrentRow?.DataBoundItem is StudioValue v) { values.Remove(v); MarkDirty(); } };
        var validate = new Button { Text = "Validate system", AutoSize = true }; validate.Click += (_, _) => Try(() => { Collect().Validate(); status.Text = "Valid: every formula resolves using the symbols in Values."; });
        valueBar.Controls.AddRange([example, remove, validate]);
        foreach (var c in new[] { ("Symbol", "Symbol", 140), ("Name", "Meaning / display name", 250), ("Initial", "Initial value", 210), ("Formula", "Gain formula", 450) })
            editor.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = c.Item1, HeaderText = c.Item2, Width = c.Item3, SortMode = DataGridViewColumnSortMode.NotSortable });
        editor.AllowUserToAddRows = true; editor.AllowUserToDeleteRows = true; editor.DataSource = values;
        editor.CellEndEdit += (_, _) => { MarkDirty(); diagram.Invalidate(); };
        editor.UserDeletedRow += (_, _) => MarkDirty();
        editor.DataError += (_, e) => { e.ThrowException = false; status.Text = "Could not apply that edit."; };
        valuesTab.Controls.Add(editor); valuesTab.Controls.Add(intro); valuesTab.Controls.Add(valueBar);
        var resultsTab = new TabPage("Simulation table"); resultsTab.Controls.Add(history);
        var graphTab = new TabPage("Graph"); var graphBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new(8) };
        graphBar.Controls.AddRange([new Label { Text = "Symbol", AutoSize = true, Padding = new(0, 5, 0, 0) }, plotted, new Label { Text = "Positive values only · Y = log10(value) · X = recorded sample", AutoSize = true, Padding = new(8, 5, 0, 0) }]);
        graphTab.Controls.Add(graph); graphTab.Controls.Add(graphBar); graph.Paint += PaintGraph; plotted.SelectedIndexChanged += (_, _) => graph.Invalidate();
        var nodeTab = new TabPage("Dependencies"); var connect = new Button { Text = "Connect symbols…", Dock = DockStyle.Top, Height = 36 }; connect.Click += (_, _) => Connect();
        nodeTab.Controls.Add(diagram); nodeTab.Controls.Add(connect); diagram.Paint += PaintDiagram;
        var referenceTab = new TabPage("Index & guide");
        var reference = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Font = new("Consolas", 11), Text = Guide };
        var find = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search the index…" }; find.TextChanged += (_, _) => { reference.Select(0, 0); if (find.Text.Length > 0) reference.Find(find.Text, RichTextBoxFinds.None); };
        referenceTab.Controls.Add(reference); referenceTab.Controls.Add(find);
        tabs.TabPages.AddRange([valuesTab, resultsTab, graphTab, nodeTab, referenceTab]); tabs.SelectedIndexChanged += (_, _) => diagram.Invalidate();
        var footer = new StatusStrip(); footer.Items.Add(status);
        Controls.Add(tabs); Controls.Add(config); Controls.Add(toolbar); Controls.Add(menu); Controls.Add(footer); MainMenuStrip = menu;
        interval.ValueChanged += (_, _) => MarkDirty(); precision.ValueChanged += (_, _) => MarkDirty(); mode.SelectedIndexChanged += (_, _) => MarkDirty();
        timer.Tick += (_, _) =>
        {
            accumulator += clock.Elapsed.TotalSeconds; clock.Restart(); int count = 0;
            while (accumulator >= (double)interval.Value && count++ < 200)
            { accumulator -= (double)interval.Value; if (!Advance(false)) break; }
            if (count > 0) RefreshResults();
        };
        FormClosing += (_, e) => { Pause(); if (!ConfirmDiscard()) e.Cancel = true; };
        FormClosed += (_, _) => timer.Dispose();
    }
    static DataGridView MakeGrid(bool readOnly) => new() { Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowTemplate = { Height = 29 }, ColumnHeadersHeight = 32, BorderStyle = BorderStyle.Fixed3D };
    void MarkDirty() { if (!loading) dirty = true; }
    bool ConfirmDiscard() => !dirty || MessageBox.Show(this, "Discard unsaved project changes?", "Unsaved project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    void Try(Action action) { try { action(); } catch (Exception ex) { Pause(); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Incranalytic", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    StudioProject Collect()
    {
        editor.EndEdit(); BindingContext?[values]?.EndCurrentEdit();
        return new StudioProject { Precision = (int)precision.Value, Interval = interval.Value, PerSecond = mode.SelectedIndex == 0,
            Values = values.Select(v => new StudioValue { Symbol = v.Symbol ?? "", Name = v.Name ?? "", Initial = v.Initial ?? "0", Formula = v.Formula ?? "0" }).ToList() };
    }
    void SetProject(StudioProject p, string? path)
    {
        Pause(); session = null; loading = true; values = new BindingList<StudioValue>(p.Values); editor.DataSource = values;
        precision.Value = p.Precision; interval.Value = p.Interval; mode.SelectedIndex = p.PerSecond ? 0 : 1; loading = false;
        Unlock(); samples.Clear(); history.Rows.Clear(); history.Columns.Clear(); plotted.Items.Clear(); projectPath = path; dirty = false;
        Text = "Incranalytic Studio 0.2 — " + (path == null ? "Untitled" : Path.GetFileName(path)); status.Text = "Ready — symbols belong to this project."; diagram.Invalidate(); graph.Invalidate();
    }
    void NewProject() { Pause(); if (ConfirmDiscard()) SetProject(new StudioProject(), null); }
    void Example()
    {
        Pause(); if (!ConfirmDiscard()) return;
        SetProject(new StudioProject { Values = [new() { Symbol = "A", Name = "Coins", Initial = "0", Formula = "B * C" }, new() { Symbol = "B", Name = "Base gain per second", Initial = "1", Formula = "0" }, new() { Symbol = "C", Name = "Multiplier", Initial = "1", Formula = "0" }] }, null); dirty = true;
    }
    void Open()
    {
        Pause(); if (!ConfirmDiscard()) return;
        using var d = new OpenFileDialog { Filter = "Incranalytic project|*.incr|JSON project|*.json" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() => SetProject(StudioProject.Load(d.FileName), d.FileName));
    }
    void Save()
    {
        Pause(); using var d = new SaveFileDialog { Filter = "Incranalytic project|*.incr", FileName = projectPath ?? "My system.incr" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() => { Collect().Save(d.FileName); projectPath = d.FileName; dirty = false; Text = "Incranalytic Studio 0.2 — " + Path.GetFileName(projectPath); status.Text = "Project saved."; });
    }
    void EnsureSession()
    {
        if (session != null) return;
        var p = Collect(); p.Validate(); if (p.Values.Count == 0) throw new InvalidOperationException("Add at least one symbol to Values first.");
        session = new StudioSession(p); samples.Clear(); history.Columns.Clear(); history.Rows.Clear(); plotted.Items.Clear();
        history.Columns.Add("time", "Elapsed seconds");
        foreach (var v in p.Values) { history.Columns.Add(v.Symbol, v.Symbol + (v.Name.Length > 0 ? " — " + v.Name : "")); plotted.Items.Add(v.Symbol); }
        plotted.SelectedIndex = 0; editor.ReadOnly = true; editor.AllowUserToAddRows = false; editor.AllowUserToDeleteRows = false; interval.Enabled = precision.Enabled = mode.Enabled = false;
        Record(); RefreshResults();
    }
    void Run() { Try(() => { EnsureSession(); accumulator = 0; clock.Restart(); timer.Start(); status.Text = "Running — Pause stops time; Reset unlocks editing."; }); }
    void Pause() { bool running = timer.Enabled; timer.Stop(); clock.Stop(); if (running && session != null) status.Text = $"Paused · {session.Ticks} ticks · elapsed {session.Elapsed} seconds."; }
    void Step() { Pause(); Advance(true); }
    bool Advance(bool refresh)
    {
        try { EnsureSession(); session!.Step(); Record(); if (refresh) RefreshResults(); return true; }
        catch (Exception ex) { Pause(); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Simulation paused"); return false; }
    }
    void Record()
    {
        samples.Add((session!.Elapsed.ToString(), new Dictionary<string, ScientificNumber>(session.State)));
        if (samples.Count > 2001) samples.RemoveAt(0);
    }
    void RefreshResults()
    {
        if (session == null) return;
        history.Rows.Clear(); foreach (var sample in samples) history.Rows.Add(new[] { sample.Time }.Concat(session.Project.Values.Select(v => sample.State[v.Symbol].ToString())).Cast<object>().ToArray());
        status.Text = $"{(timer.Enabled ? "Running" : "Paused")} · {session.Ticks} ticks · elapsed {session.Elapsed} seconds · {samples.Count} retained samples (maximum 2,001). Reset to edit.";
        graph.Invalidate();
    }
    void Unlock() { editor.ReadOnly = false; editor.AllowUserToAddRows = editor.AllowUserToDeleteRows = true; interval.Enabled = precision.Enabled = mode.Enabled = true; }
    void Reset() { Pause(); session = null; samples.Clear(); history.Rows.Clear(); Unlock(); graph.Invalidate(); status.Text = "Reset to initial conditions — editing unlocked."; }
    void PaintGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (samples.Count < 2 || plotted.SelectedItem is not string symbol) { g.DrawString("Run or Step a system to plot its values.", Font, Brushes.DimGray, 30, 30); return; }
        var points = samples.Select((s, i) => (Index: i, Value: s.State[symbol].PlotLog())).Where(p => double.IsFinite(p.Value)).ToArray();
        if (points.Length < 2) { g.DrawString("Need two positive samples within log10 plotting range ±1,000,000. All values remain in the table.", Font, Brushes.DimGray, 30, 30); return; }
        float w = Math.Max(1, graph.Width - 150), h = Math.Max(1, graph.Height - 110); double lo = points.Min(p => p.Value), hi = points.Max(p => p.Value); if (hi == lo) hi = lo + 1;
        g.DrawRectangle(Pens.Gray, 100, 35, w, h);
        for (int i = 0; i <= 5; i++) { float y = 35 + h * i / 5; g.DrawLine(Pens.Gainsboro, 100, y, 100 + w, y); g.DrawString((hi - (hi - lo) * i / 5).ToString("G6"), Font, Brushes.DimGray, 5, y); }
        using var pen = new Pen(Color.FromArgb(35, 90, 135), 2);
        PointF? previous = null; int prevIndex = -2;
        foreach (var p in points) { var point = new PointF(100 + w * p.Index / (samples.Count - 1), 35 + h * (float)((hi - p.Value) / (hi - lo))); if (previous.HasValue && p.Index == prevIndex + 1) g.DrawLine(pen, previous.Value, point); previous = point; prevIndex = p.Index; }
        g.DrawString($"{symbol} · log10(value) · zero, negative and out-of-range samples omitted", Font, Brushes.Black, 100, h + 50);
    }
    void PaintDiagram(object? sender, PaintEventArgs e)
    {
        var list = values.ToList(); var g = e.Graphics; g.TranslateTransform(diagram.AutoScrollPosition.X, diagram.AutoScrollPosition.Y);
        diagram.AutoScrollMinSize = new Size(900, ((list.Count + 2) / 3) * 130 + 80);
        if (list.Count == 0) { g.DrawString("Define symbols in Values, then connect them through gain formulas.", Font, Brushes.DimGray, 30, 30); return; }
        Rectangle Box(int i) => new(40 + i % 3 * 290, 40 + i / 3 * 130, 230, 75);
        using var arrow = new Pen(Color.SlateGray, 1.5f) { CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 5) };
        for (int i = 0; i < list.Count; i++) for (int j = 0; j < list.Count; j++) if (Regex.IsMatch(list[i].Formula ?? "", @"\b" + Regex.Escape(list[j].Symbol) + @"\b") && list[j].Symbol.Length > 0)
        { var from = Box(j); var to = Box(i); if (i != j) g.DrawLine(arrow, from.Right, from.Top + 37, to.Left, to.Top + 37); }
        for (int i = 0; i < list.Count; i++) { var b = Box(i); g.FillRectangle(Brushes.Beige, b); g.DrawRectangle(Pens.SlateGray, b); g.DrawString(list[i].Symbol + " — " + list[i].Name + "\nGain: " + list[i].Formula, Font, Brushes.Black, new RectangleF(b.X + 7, b.Y + 7, b.Width - 14, b.Height - 14)); }
    }
    void Connect()
    {
        if (session != null) { status.Text = "Reset the simulation before editing connections."; return; }
        editor.EndEdit(); if (values.Count < 2) { status.Text = "Define at least two symbols first."; return; }
        using var d = new Form { Text = "Connect symbols", Width = 450, Height = 240, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(15), FlowDirection = FlowDirection.TopDown };
        var from = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList }; var to = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in values) { from.Items.Add(v.Symbol); to.Items.Add(v.Symbol); } from.SelectedIndex = 0; to.SelectedIndex = 1;
        var operation = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList }; operation.Items.AddRange(["Add source to target's gain", "Multiply target's gain by source"]); operation.SelectedIndex = 0;
        var apply = new Button { Text = "Apply connection", AutoSize = true };
        apply.Click += (_, _) => { var v = values.First(v => v.Symbol == to.Text); v.Formula = "(" + v.Formula + ") " + (operation.SelectedIndex == 0 ? "+" : "*") + " " + from.Text; editor.Refresh(); MarkDirty(); diagram.Invalidate(); d.Close(); };
        flow.Controls.AddRange([new Label { Text = "Source → target (edits target gain formula)", AutoSize = true }, from, to, operation, apply]); d.Controls.Add(flow); d.ShowDialog(this);
    }
    void ExportCsv()
    {
        Pause(); if (session == null) { status.Text = "Run or Step before exporting simulation results."; return; }
        using var d = new SaveFileDialog { Filter = "CSV table|*.csv", FileName = "simulation.csv" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() =>
        {
            string Q(string s) => "\"" + (s.StartsWith('=') || s.StartsWith('+') || s.StartsWith('@') ? "'" : "") + s.Replace("\"", "\"\"") + "\"";
            File.WriteAllLines(d.FileName, new[] { "elapsed_seconds," + string.Join(",", session.Project.Values.Select(v => Q(v.Symbol))) }.Concat(samples.Select(s => Q(s.Time) + "," + string.Join(",", session.Project.Values.Select(v => Q(s.State[v.Symbol].ToString())))))); status.Text = "Simulation table exported.";
        });
    }
    void ExportUnity()
    {
        Pause(); using var d = new FolderBrowserDialog { Description = "Choose a parent folder for a new Unity export" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() => { var p = Collect(); p.Validate(); if (p.Values.Count == 0) throw new InvalidOperationException("Define some values first."); string folder = Path.Combine(d.SelectedPath, "Incranalytic-Unity-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")); UnityExport.Write(folder, p, ExportRuntime()); status.Text = "Unity scripts exported: " + folder; });
    }
    static string ExportRuntime()
    {
        var assembly = typeof(StudioForm).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("ScientificNumber.cs") || n.EndsWith("ExportRuntime.cs")))!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public void VerifyStudio(string reportPath)
    {
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add("PASS: " + message); }
        Check(values.Count == 0 && tabs.TabPages[0].Text == "Values", "Startup uses a blank Values sheet");
        SetProject(new StudioProject { Values = [new() { Symbol = "Q", Name = "Any chosen meaning", Initial = "0", Formula = "R" }, new() { Symbol = "R", Initial = "1e1000", Formula = "0" }] }, null);
        Check(Collect().Values[0].Name == "Any chosen meaning", "Values binding preserves arbitrary symbol meanings");
        EnsureSession(); Check(Advance(true), "Step succeeds through studio controls");
        Check(session!.State["Q"].ToString() == "1e1000" && history.Rows.Count == 2, "Simulation table displays huge scientific values");
        Run(); Check(timer.Enabled, "Run starts simulation timer"); Pause(); Check(!timer.Enabled, "Pause stops simulation timer");
        Advance(true); tabs.SelectedIndex = 2; graph.Refresh(); tabs.SelectedIndex = 3; diagram.Refresh();
        Check(samples.Count == 3 && plotted.Items.Count == 2, "Graph and dependency views render with simulation data");
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "ui-export-" + Guid.NewGuid().ToString("N"));
        UnityExport.Write(folder, Collect(), ExportRuntime()); Check(File.Exists(Path.Combine(folder, "ScientificNumber.cs")), "Installed app exports its embedded C# runtime");
        Reset(); Check(session == null && !editor.ReadOnly && samples.Count == 0, "Reset clears history and unlocks editing");
        dirty = false; File.WriteAllLines(reportPath, checks);
    }
    public const string Terms = "INCRANALYTIC STUDIO — DEVELOPMENT PREVIEW\n\nThis software supports education and human-authored system design. You are responsible for your use, your authored rules, and the rights needed for any imported material or distributed exports.\n\nProvided as-is, without guarantees of correctness, completeness, fitness, or uninterrupted operation, to the extent permitted by law. Mandatory legal rights remain unaffected. Verify exported code in your target engine. These preview terms need legal review before a public commercial release.\n\nPrivacy: projects and calculations stay local. This version has no telemetry, AI service, account, network requests, or automatic system generation. Development has used AI assistance.\n\nNumber behavior: finite significant-digit precision (1–1,000), truncation toward zero after each operation, arbitrary-size integer exponents subject to memory and input limits. Extremely small addends can be discarded at selected precision. No literal infinity is stored.\n\nSimulation: all gain formulas read the previous step's state; updates are simultaneous. Per-second mode uses fixed-step Euler updates (gain × interval); per-tick mode adds the gain once. Changing interval can change results.\n\nImport supports the Incranalytic project format, not arbitrary compiled scripts. Unity export uses the same arithmetic and update rules; engine integration must still be tested in Unity.";
    const string Guide = "INCRANALYTIC — SYSTEM INDEX\n\nVALUES\nSymbols are case-sensitive and user-defined. A can mean coins in one project and a multiplier in another. Use letters, digits and underscores; begin with a letter or underscore. Display names can contain spaces.\n\nINITIAL VALUE\nStarting number, e.g. 0, 1.25, 1e1000. All results display scientific notation.\n\nGAIN FORMULA\nAmount added to a symbol. Per-second mode multiplies it by the chosen interval; per-tick mode adds it once per tick. 0 keeps a value constant. Examples: B * C, A * 0.05, min(B, C).\n\nFUNCTIONS AND OPERATORS\n+ - * / ^ ( )\nabs(x), min(a,b), max(a,b)\nPowers require integer exponents from -100000 to 100000. Direct e notation supports much larger magnitudes. Fractional powers, comparisons and conditions are future capabilities.\n\nTIME\nChoose interval in seconds. Time has no automatic project symbol. To model elapsed time, define any symbol with initial 0 and gain 1 in per-second mode.\n\nSIMULATION TICK\nOne update. Every formula reads the previous state; all results apply together. Example: A starts at 0 and gains B; B starts at 1 and gains 1. After two one-second steps A=3, B=3.\n\nRUN / PAUSE / STEP / RESET\nRun advances with elapsed wall time. Pause stops advancing. Step applies one interval. Reset clears recorded results, restores initial conditions on the next run, and unlocks editing. Save retains the authored model, not a running simulation. Latest 2,001 samples are retained.\n\nPRECISION AND BIG NUMBERS\nChoose 1–1,000 significant digits. Results truncate toward zero after each operation. Numbers use integer coefficients and arbitrary-size integer exponents, limited by memory. This does not mean infinite storage or exact arithmetic at finite precision.\n\nGRAPHS\nGraph plots positive values on a log10 vertical axis. Zero/negative/out-of-range points are omitted; the table retains their values. Horizontal axis is sample index; elapsed time is in the table.\n\nDEPENDENCY GRAPH\nShows which symbols influence each gain formula. Connect symbols edits the target gain formula. Feedback and self-reference use previous-tick values, avoiding same-tick circular evaluation.\n\nGROWTH REGIME\nLinear: constant gain. Exponential: gain proportional to existing value.\n\nCOMPOUNDING\nGrowth that acts on previous growth. Interval and modifier order matter.\n\nMODIFIER PIPELINE\nOrder of bonuses; write explicit parentheses to express your chosen order.\n\nBOTTLENECK / THROUGHPUT\nA limiting input / quantity processed per time. min(A,B) can model a simple limiting rate.\n\nSOFT CAP\nA rule reducing growth beyond a threshold. The base studio does not yet implement conditional formulas or fractional powers.\n\nCOST ESCALATION\nExample: baseCost * growth ^ owned, with integer owned.\n\nAFFORDABILITY / PAYBACK\nCost divided by positive income or production increase. Variable income needs a time simulation.\n\nPRESTIGE / RESET LAYERS\nAuthored reset mechanics; specialized event and reset nodes are future work.\n\nOFFLINE SIMULATION\nA modeled time interval. A constant rate can be integrated as rate × duration; changing rates require explicit simulation.\n\nPROGRESSION ENVELOPE\nA creator's expected bounds or target outcomes. Incranalytic does not decide which progression is correct.\n\nC# / UNITY EXPORT\nExports a MonoBehaviour plus shared scientific arithmetic. Copy the two .cs files into a Unity project's Assets folder. Attach IncranalyticSystem to a GameObject. The export README explains integration, precision, and testing.\n\nPURPOSE\nYou create the rules. Incranalytic calculates, displays and tests them. It does not use AI to generate your system.";
}
