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
    readonly StudioViewHost tabs = new() { Dock = DockStyle.Fill };
    readonly WorkspacePane workspace = new();
    readonly StudioProfile profile;
    RecoveryStore? recovery;
    readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 30000 };
    readonly ToolTip hints = new() { AutoPopDelay = 15000, InitialDelay = 500 };
    readonly Label viewHeading = new() { Dock = DockStyle.Top, Height = 52, Padding = new(16, 12, 12, 6), Font = new("Segoe UI", 13, FontStyle.Bold), BackColor = Color.FromArgb(244, 243, 235) };
    readonly Dictionary<StudioPage, Button> navigation = [];
    ManualForm? systemManual, ifcloudManual;
    WelcomeForm? welcome;
    FlowLayoutPanel? simulationSettings;
    ToolStripButton? settingsToggle;
    ToolStripMenuItem? settingsMenu;
    ToolStrip? mainToolbar;
    bool applyingPreferences;
    readonly ToolStripStatusLabel recoveryStatus = new("Recovery on");
    long revision, recoveredRevision = -1;
    sealed record Channel(string Key, string Label) { public override string ToString() => Label; }
    readonly List<Channel> channels = [];
    readonly ToolStripStatusLabel status = new("New project — add blocks in Workspace or define symbols in Values.");
    readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    readonly Stopwatch clock = new();
    readonly Panel graph = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly Panel diagram = new() { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
    readonly List<(string Time, Dictionary<string, ScientificNumber> State)> samples = [];
    StudioSession? session;
    bool dirty, loading;
    string? projectPath;
    double accumulator;

    public StudioForm(StudioProfile? suppliedProfile = null, bool interactiveStartup = true)
    {
        profile = suppliedProfile ?? new StudioProfile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Incranalytic"));
        Text = "Incranalytic Studio 0.5 — Untitled"; Width = 1450; Height = 900; MinimumSize = new(1100, 700);
        StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 9); BackColor = Color.FromArgb(230, 228, 214);
        var menu = new MenuStrip { BackColor = BackColor, RenderMode = ToolStripRenderMode.System };
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("New blank system", null, (_, _) => NewProject());
        file.DropDownItems.Add("Import project…", null, (_, _) => Open());
        file.DropDownItems.Add("Save project…", null, (_, _) => Save());
        file.DropDownItems.Add("Save project as…", null, (_, _) => SaveProject(true));
        file.DropDownItems.Add("Recover unsaved work…", null, (_, _) => OfferRecovery());
        file.DropDownItems.Add("Export C# for Unity…", null, (_, _) => ExportUnity());
        file.DropDownItems.Add("Export simulation CSV…", null, (_, _) => ExportCsv());
        file.DropDownItems.Add(new ToolStripSeparator()); file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("Getting started…", null, (_, _) => ShowWelcome());
        help.DropDownItems.Add("System Manual…", null, (_, _) => ShowManual(false));
        help.DropDownItems.Add("IFCLOUD Manual…", null, (_, _) => ShowManual(true));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add("Terms and privacy", null, (_, _) => MessageBox.Show(this, Terms, "Incranalytic terms"));
        help.DropDownItems.Add(DistributionChannel.UpdateMenuLabel, null, (_, _) => Try(() =>
        {
            if (DistributionChannel.IsStore)
            {
                MessageBox.Show(this, "Microsoft Store manages updates for this edition. Open Microsoft Store → Library to check for updates.\n\nProject files remain editable in compatible Incranalytic versions.", "Microsoft Store updates");
                return;
            }
            string manager = Path.Combine(AppContext.BaseDirectory, "manager", "Incranalytic-Installer.exe");
            if (!File.Exists(manager)) throw new FileNotFoundException("The reusable Version Manager is included with the published setup. Install version 0.4 or download Incranalytic-Installer.exe from GitHub.");
            Process.Start(new ProcessStartInfo(manager) { UseShellExecute = true });
        }));
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(this, "Incranalytic Studio 0.5.0\nA studio for human-authored incremental systems.\n\nDeveloped with substantial assistance from GPT-6.1SOL. This is a solo learning project with no funding or development team; AI assistance makes building and testing it feasible. Human direction defines the product.\n\nThe studio does not use AI services to generate user systems.\n\n" + DistributionChannel.AboutDistribution + "\nActual Unity integration and clean-machine testing remain limited. See the repository's AI development disclosure.", "About Incranalytic"));
        var viewMenu = new ToolStripMenuItem("&View");
        ToolStripMenuItem Toggle(string label, bool initial, Action<bool> set)
        {
            var item = new ToolStripMenuItem(label) { CheckOnClick = true, Checked = initial };
            item.CheckedChanged += (_, _) => { if (applyingPreferences) return; set(item.Checked); ApplyPreferences(); SavePreferences(); }; viewMenu.DropDownItems.Add(item); return item;
        }
        Toggle("Show workspace grid", profile.Preferences.ShowGrid, b => profile.Preferences.ShowGrid = b);
        Toggle("Snap blocks to grid", profile.Preferences.SnapToGrid, b => profile.Preferences.SnapToGrid = b);
        Toggle("Show live values on blocks", profile.Preferences.ShowBlockValues, b => profile.Preferences.ShowBlockValues = b);
        Toggle("Show block inspector", profile.Preferences.ShowInspector, b => profile.Preferences.ShowInspector = b);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        settingsMenu = Toggle("Show simulation settings", profile.Preferences.ShowSimulationSettings, b => profile.Preferences.ShowSimulationSettings = b);
        Toggle("Show tooltips", profile.Preferences.ShowTooltips, b => profile.Preferences.ShowTooltips = b);
        Toggle("Automatic local recovery", profile.Preferences.EnableRecovery, b => profile.Preferences.EnableRecovery = b);
        menu.Items.AddRange([file, viewMenu, help]);
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(239, 237, 225), RenderMode = ToolStripRenderMode.System, Padding = new(5) };
        mainToolbar = toolbar;
        toolbar.Items.Add("New", null, (_, _) => NewProject()); toolbar.Items.Add("Import…", null, (_, _) => Open()); toolbar.Items.Add("Save…", null, (_, _) => Save());
        toolbar.Items.Add(new ToolStripSeparator()); toolbar.Items.Add("Run", null, (_, _) => Run()); toolbar.Items.Add("Pause", null, (_, _) => Pause()); toolbar.Items.Add("Step", null, (_, _) => Step()); toolbar.Items.Add("Reset", null, (_, _) => Reset());
        toolbar.Items.Add(new ToolStripSeparator()); toolbar.Items.Add("Unity C#…", null, (_, _) => ExportUnity());
        toolbar.Items.Add("Validate", null, (_, _) => Try(() => { Collect().Validate(); status.Text = "Valid: code, ports, connections and first-tick calculations checked."; }));
        settingsToggle = new ToolStripButton("Simulation settings") { CheckOnClick = true, ToolTipText = "Show or hide interval, rate mode and numerical precision." };
        settingsToggle.CheckedChanged += (_, _) => { if (applyingPreferences) return; profile.Preferences.ShowSimulationSettings = settingsToggle.Checked; ApplyPreferences(); SavePreferences(); };
        toolbar.Items.Add(new ToolStripSeparator()); toolbar.Items.Add(settingsToggle);
        var config = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new(8), WrapContents = false, BackColor = BackColor };
        simulationSettings = config;
        mode.Items.AddRange(["Per second: gain × interval", "Per tick: gain per interval"]); mode.SelectedIndex = 0;
        config.Controls.AddRange([new Label { Text = "Interval (seconds)", AutoSize = true, Padding = new(0, 5, 0, 0) }, interval, mode, new Label { Text = "Significant digits", AutoSize = true, Padding = new(10, 5, 0, 0) }, precision]);
        var valuesTab = new StudioPage("Values");
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
        var resultsTab = new StudioPage("Simulation Table"); resultsTab.Controls.Add(history);
        history.VirtualMode = true;
        history.CellValueNeeded += (_, e) => { if (e.RowIndex < samples.Count) e.Value = e.ColumnIndex == 0 ? samples[e.RowIndex].Time : samples[e.RowIndex].State[channels[e.ColumnIndex - 1].Key].ToString(); };
        var graphTab = new StudioPage("Graph"); var graphBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new(8) };
        graphBar.Controls.AddRange([new Label { Text = "Symbol", AutoSize = true, Padding = new(0, 5, 0, 0) }, plotted, new Label { Text = "Positive values only · Y = log10(value) · X = recorded sample", AutoSize = true, Padding = new(8, 5, 0, 0) }]);
        graphTab.Controls.Add(graph); graphTab.Controls.Add(graphBar); graph.Paint += PaintGraph; plotted.SelectedIndexChanged += (_, _) => graph.Invalidate();
        var nodeTab = new StudioPage("Dependencies"); var connect = new Button { Text = "Connect symbols…", Dock = DockStyle.Top, Height = 36 }; connect.Click += (_, _) => Connect();
        nodeTab.Controls.Add(diagram); nodeTab.Controls.Add(connect); diagram.Paint += PaintDiagram;
        var workspaceTab = new StudioPage("Workspace"); workspaceTab.Controls.Add(workspace); workspace.Changed += MarkDirty; workspace.Message += s => status.Text = s;
        workspace.LanguageHelpRequested += owner => ShowManual(true, owner);
        workspace.RecoveryChanged += () => { revision++; if (!workspace.HasCodeDraft) { if (!dirty) ClearRecovery(); else CaptureRecovery(); } };
        tabs.TabPages.AddRange([workspaceTab, valuesTab, resultsTab, graphTab, nodeTab]); tabs.InitializeViews();
        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 185, Padding = new(10, 18, 10, 10), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(224, 223, 209) };
        sidebar.Controls.Add(new Label { Text = "WORK VIEWS", Height = 35, Width = 160, Font = new(Font, FontStyle.Bold) });
        foreach (var page in tabs.TabPages)
        {
            var button = new Button { Text = page.Text, Width = 160, Height = 44, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, Margin = new(0, 0, 0, 7), Padding = new(9, 0, 0, 0) };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => tabs.SelectedTab = page; navigation.Add(page, button); sidebar.Controls.Add(button);
            hints.SetToolTip(button, page.Text == "Workspace" ? "Place, wire and code blocks." : page.Text == "Values" ? "Define the symbols and numbers in your system." : page.Text == "Simulation Table" ? "Inspect recorded values and elapsed time." : page.Text == "Graph" ? "Plot a chosen simulation signal." : "Inspect references between Values formulas.");
        }
        tabs.SelectedIndexChanged += (_, _) => { UpdateNavigation(); diagram.Invalidate(); }; UpdateNavigation();
        var content = new Panel { Dock = DockStyle.Fill }; content.Controls.Add(tabs); content.Controls.Add(viewHeading);
        var root = new Panel { Dock = DockStyle.Fill }; root.Controls.Add(content); root.Controls.Add(sidebar);
        var footer = new StatusStrip(); status.Spring = true; status.TextAlign = ContentAlignment.MiddleLeft; footer.Items.Add(status); footer.Items.Add(recoveryStatus);
        Controls.Add(root); Controls.Add(config); Controls.Add(toolbar); Controls.Add(menu); Controls.Add(footer); MainMenuStrip = menu;
        hints.SetToolTip(interval, "Seconds represented by one simulation step."); hints.SetToolTip(mode, "Applies to Values gain formulas. Workspace modules execute once per step."); hints.SetToolTip(precision, "Number of significant digits retained by scientific arithmetic.");
        foreach (ToolStripItem item in toolbar.Items) if (string.IsNullOrEmpty(item.ToolTipText)) item.ToolTipText = item.Text switch { "Run" => "Advance simulation using elapsed real time.", "Pause" => "Stop advancing while keeping the current state.", "Step" => "Complete one simulation interval.", "Reset" => "Clear simulation results and unlock editing.", "Save…" => "Save the authored system, including workspace and code drafts.", "Validate" => "Check code, wiring and initial calculations.", _ => item.Text };
        ApplyPreferences();
        interval.ValueChanged += (_, _) => MarkDirty(); precision.ValueChanged += (_, _) => MarkDirty(); mode.SelectedIndexChanged += (_, _) => MarkDirty();
        timer.Tick += (_, _) =>
        {
            accumulator += clock.Elapsed.TotalSeconds; clock.Restart(); int count = 0; bool failed = false;
            while (accumulator >= (double)interval.Value && count++ < 200)
            { accumulator -= (double)interval.Value; if (!Advance(false)) { failed = true; break; } }
            if (count > 0 && !failed) RefreshResults();
        };
        recoveryTimer.Tick += (_, _) => CaptureRecovery();
        FormClosing += (_, e) => { Pause(); if (interactiveStartup && !ConfirmDiscard()) e.Cancel = true; else { recoveryTimer.Stop(); ClearRecovery(); } };
        FormClosed += (_, _) => { timer.Dispose(); recoveryTimer.Dispose(); hints.Dispose(); recovery?.Dispose(); };
        if (interactiveStartup) Shown += (_, _) => BeginInvoke(new Action(() => { EnsureRecovery(); OfferRecovery(); if (!profile.Preferences.HasSeenWelcome || profile.Preferences.ShowWelcomeAtStartup) ShowWelcome(); if (profile.Warning != null) status.Text = profile.Warning; }));
    }
    static DataGridView MakeGrid(bool readOnly) => new() { Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowTemplate = { Height = 29 }, ColumnHeadersHeight = 32, BorderStyle = BorderStyle.Fixed3D };
    void MarkDirty() { if (!loading) { dirty = true; revision++; UpdateTitle(); } }
    void UpdateTitle() => Text = "Incranalytic Studio 0.5 — " + (projectPath == null ? "Untitled" : Path.GetFileName(projectPath)) + (dirty ? " *" : "");
    void UpdateNavigation()
    {
        foreach (var pair in navigation) { bool active = pair.Key == tabs.SelectedTab; pair.Value.BackColor = active ? Color.FromArgb(53, 75, 93) : Color.FromArgb(232, 231, 219); pair.Value.ForeColor = active ? Color.White : Color.FromArgb(30, 40, 48); pair.Value.AccessibleDescription = active ? "Current work view" : "Switch work view"; }
        viewHeading.Text = tabs.SelectedTab?.Text ?? "Workspace";
    }
    void ApplyPreferences()
    {
        if (applyingPreferences) return; applyingPreferences = true;
        try
        {
            workspace.ApplyPreferences(profile.Preferences); hints.Active = profile.Preferences.ShowTooltips;
            if (mainToolbar != null) mainToolbar.ShowItemToolTips = profile.Preferences.ShowTooltips;
            if (simulationSettings != null) simulationSettings.Visible = profile.Preferences.ShowSimulationSettings;
            if (settingsToggle != null) settingsToggle.Checked = profile.Preferences.ShowSimulationSettings;
            if (settingsMenu != null) settingsMenu.Checked = profile.Preferences.ShowSimulationSettings;
            if (profile.Preferences.EnableRecovery) recoveryTimer.Start(); else recoveryTimer.Stop();
            recoveryStatus.Text = profile.Preferences.EnableRecovery ? "Recovery on · 30s" : "Recovery off";
        }
        finally { applyingPreferences = false; }
    }
    void SavePreferences() { try { profile.Save(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Preferences could not be saved for this Windows account."; } }
    void EnsureRecovery()
    {
        if (recovery != null) return;
        try { recovery = new RecoveryStore(profile.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { recoveryStatus.Text = "Recovery unavailable"; }
    }
    void ClearRecovery()
    {
        try { recovery?.ClearCurrent(); recoveredRevision = -1; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { recoveryStatus.Text = "An older recovery copy was retained"; }
    }
    void CaptureRecovery()
    {
        if (!profile.Preferences.EnableRecovery || (!dirty && !workspace.HasCodeDraft) || recoveredRevision == revision) return;
        EnsureRecovery(); if (recovery == null) return;
        try
        {
            var snapshot = new StudioProject { Precision = (int)precision.Value, Interval = interval.Value, PerSecond = mode.SelectedIndex == 0,
                Values = values.Where(v => !string.IsNullOrWhiteSpace(v.Symbol)).Select(v => new StudioValue { Symbol = v.Symbol, Name = v.Name ?? "", Initial = v.Initial ?? "0", Formula = v.Formula ?? "0" }).ToList(), Workspace = workspace.RecoveryGraph() };
            recovery.Write(snapshot, projectPath); recoveredRevision = revision; recoveryStatus.Text = "Recovery saved " + DateTime.Now.ToString("T");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { recoveryStatus.Text = "Recovery could not be written · save a project checkpoint"; }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { recoveryStatus.Text = "Recovery deferred: " + ex.Message; }
    }
    void OfferRecovery()
    {
        Pause(); EnsureRecovery(); if (recovery == null) return;
        Try(() =>
        {
            var candidates = recovery.FindAvailable(); if (candidates.Count == 0) return;
            using var dialog = new RecoveryForm(candidates);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Selected == null || !ConfirmDiscard()) return;
            ClearRecovery(); recovery.Claim(dialog.Selected);
            SetProject(dialog.Selected.Snapshot.Project, dialog.Selected.Snapshot.OriginalPath); MarkDirty();
            status.Text = "Recovery restored as unsaved work. Review and Save; your saved project has not been overwritten.";
        });
    }
    void ShowManual(bool code, Form? owner = null)
    {
        var existing = code ? ifcloudManual : systemManual;
        if (existing == null || existing.IsDisposed)
        {
            existing = new ManualForm(code ? "IFCLOUD Manual" : "System Manual", code ? IfcloudGuide.Text : Guide,
                [new("Start here", code ? "1. Place a Module in Workspace.\n2. Double-click it to open the editor.\n3. Declare input and output ports at the top.\n4. Write assignments and if/else rules.\n5. Validate code, Apply, connect inputs and add an Output.\n6. Step and inspect Why this number?\n\nEach topic below documents supported syntax and examples. IFCLOUD is a bounded custom language with C#-style statements, not a C# compiler." : "1. Start with a blank Workspace or the optional example loop.\n2. Add Input, Module, Previous-step and Output blocks.\n3. Connect ports and author your rules.\n4. Use Run, Pause, Step and Reset.\n5. Inspect results in the table, graph and block inspector.\n6. Save a project, then export and test Unity code.\n\nUse Help → Getting started for the short walkthrough. View contains grid, snapping, inspector, live-value, settings, tooltip and recovery toggles.")]);
            if (code) ifcloudManual = existing; else systemManual = existing;
        }
        if (!existing.Visible) existing.Show(owner ?? this); else { existing.Owner = owner ?? this; existing.Activate(); }
    }
    void ShowWelcome()
    {
        if (welcome != null && !welcome.IsDisposed) { welcome.Activate(); return; }
        welcome = new WelcomeForm(profile.Preferences.ShowWelcomeAtStartup,
            () => { Pause(); if (!ConfirmDiscard()) return false; ClearRecovery(); SetProject(new StudioProject(), null); tabs.SelectedIndex = 0; return true; },
            () => { Pause(); if (!ConfirmDiscard()) return false; ClearRecovery(); SetProject(new StudioProject { Workspace = WorkspaceFiles.ExampleLoop() }, null); MarkDirty(); tabs.SelectedIndex = 0; status.Text = "Example loaded. Step to increment the counter; select its module to inspect the branch."; return true; },
            () => ShowManual(false), again => { profile.Preferences.HasSeenWelcome = true; profile.Preferences.ShowWelcomeAtStartup = again; SavePreferences(); });
        welcome.Show(this);
    }
    bool ConfirmDiscard()
    {
        if (!dirty) return true;
        var answer = MessageBox.Show(this, "Save project changes before continuing?\n\nYes saves; No discards these changes; Cancel keeps this project open.", "Unsaved project", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.Yes) return SaveProject(false);
        ClearRecovery(); return true;
    }
    void Try(Action action) { try { action(); } catch (Exception ex) { Pause(); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Incranalytic", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    StudioProject Collect()
    {
        editor.EndEdit(); BindingContext?[values]?.EndCurrentEdit();
        return new StudioProject { Precision = (int)precision.Value, Interval = interval.Value, PerSecond = mode.SelectedIndex == 0,
            Values = values.Select(v => new StudioValue { Symbol = v.Symbol ?? "", Name = v.Name ?? "", Initial = v.Initial ?? "0", Formula = v.Formula ?? "0" }).ToList(), Workspace = WorkspaceFiles.Clone(workspace.Graph) };
    }
    void SetProject(StudioProject p, string? path)
    {
        Pause(); session = null; loading = true; values = new BindingList<StudioValue>(p.Values); editor.DataSource = values;
        precision.Value = p.Precision; interval.Value = p.Interval; mode.SelectedIndex = p.PerSecond ? 0 : 1; loading = false;
        workspace.SetGraph(p.Workspace);
        Unlock(); samples.Clear(); history.Rows.Clear(); history.Columns.Clear(); plotted.Items.Clear(); projectPath = path; dirty = false;
        revision++; recoveredRevision = -1; UpdateTitle(); status.Text = "Ready — create blocks in Workspace or define project symbols in Values."; diagram.Invalidate(); graph.Invalidate();
    }
    void NewProject() { Pause(); if (ConfirmDiscard()) { ClearRecovery(); SetProject(new StudioProject(), null); } }
    void Example()
    {
        Pause(); if (!ConfirmDiscard()) return; ClearRecovery();
        SetProject(new StudioProject { Values = [new() { Symbol = "A", Name = "Coins", Initial = "0", Formula = "B * C" }, new() { Symbol = "B", Name = "Base gain per second", Initial = "1", Formula = "0" }, new() { Symbol = "C", Name = "Multiplier", Initial = "1", Formula = "0" }] }, null); dirty = true;
    }
    void Open()
    {
        Pause();
        using var d = new OpenFileDialog { Filter = "Incranalytic project|*.incr|JSON project|*.json" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() => { var project = StudioProject.Load(d.FileName); if (ConfirmDiscard()) { ClearRecovery(); SetProject(project, d.FileName); } });
    }
    void Save() => SaveProject(false);
    bool SaveProject(bool saveAs)
    {
        Pause(); string? target = projectPath;
        if (saveAs || target == null)
        {
            using var d = new SaveFileDialog { Filter = "Incranalytic project|*.incr", FileName = target ?? "My system.incr" };
            if (d.ShowDialog(this) != DialogResult.OK) return false; target = d.FileName;
        }
        try { Collect().Save(target); projectPath = target; dirty = false; ClearRecovery(); UpdateTitle(); status.Text = "Project saved, including workspace layout and code drafts."; return true; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save project", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
    }
    void EnsureSession()
    {
        if (session != null) return;
        var p = Collect(); p.Validate(); if (p.Values.Count == 0 && p.Workspace.Nodes.Count == 0) throw new InvalidOperationException("Add a workspace block or define a Values symbol first.");
        session = new StudioSession(p); samples.Clear(); history.Columns.Clear(); history.Rows.Clear(); plotted.Items.Clear();
        history.Columns.Add("time", "Elapsed seconds");
        channels.Clear();
        foreach (var v in p.Values) channels.Add(new(v.Symbol, v.Symbol + (v.Name.Length > 0 ? " — " + v.Name : "")));
        foreach (var n in p.Workspace.Nodes.Where(n => n.Kind == "Output")) channels.Add(new("@" + n.Id, "Workspace: " + n.Title));
        foreach (var channel in channels) { history.Columns.Add(channel.Key, channel.Label); history.Columns[^1].SortMode = DataGridViewColumnSortMode.NotSortable; plotted.Items.Add(channel); }
        if (plotted.Items.Count > 0) plotted.SelectedIndex = 0; workspace.SetEditable(false); editor.ReadOnly = true; editor.AllowUserToAddRows = false; editor.AllowUserToDeleteRows = false; interval.Enabled = precision.Enabled = mode.Enabled = false;
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
        samples.Add((session!.Elapsed + (session.Ticks == 0 && session.Project.Workspace.Nodes.Count > 0 ? " (preview)" : ""), session.DisplayState()));
        int limit = Math.Min(2001, Math.Max(20, 100000 / Math.Max(1, channels.Count)));
        if (samples.Count > limit) samples.RemoveAt(0);
    }
    void RefreshResults()
    {
        if (session == null) return;
        history.RowCount = samples.Count; history.Invalidate();
        status.Text = $"{(timer.Enabled ? "Running" : "Paused")} · {session.Ticks} ticks · elapsed {session.Elapsed} seconds · {samples.Count} retained samples. Reset to edit.";
        workspace.ShowEvaluation(session.LastWorkspace, session.Ticks);
        graph.Invalidate();
    }
    void Unlock() { workspace.SetEditable(true); editor.ReadOnly = false; editor.AllowUserToAddRows = editor.AllowUserToDeleteRows = true; interval.Enabled = precision.Enabled = mode.Enabled = true; }
    void Reset() { Pause(); session = null; samples.Clear(); history.Rows.Clear(); Unlock(); workspace.ShowEvaluation(null, 0); graph.Invalidate(); status.Text = "Reset to initial conditions — editing unlocked."; }
    void PaintGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (samples.Count < 2 || plotted.SelectedItem is not Channel selected) { g.DrawString("Run or Step a system to plot its values. Use an Output block to record workspace results.", Font, Brushes.DimGray, 30, 30); return; }
        string symbol = selected.Key;
        var points = samples.Select((s, i) => (Index: i, Value: s.State[symbol].PlotLog())).Where(p => double.IsFinite(p.Value)).ToArray();
        if (points.Length < 2) { g.DrawString("Need two positive samples within log10 plotting range ±1,000,000. All values remain in the table.", Font, Brushes.DimGray, 30, 30); return; }
        float w = Math.Max(1, graph.Width - 150), h = Math.Max(1, graph.Height - 110); double lo = points.Min(p => p.Value), hi = points.Max(p => p.Value); if (hi == lo) hi = lo + 1;
        g.DrawRectangle(Pens.Gray, 100, 35, w, h);
        for (int i = 0; i <= 5; i++) { float y = 35 + h * i / 5; g.DrawLine(Pens.Gainsboro, 100, y, 100 + w, y); g.DrawString((hi - (hi - lo) * i / 5).ToString("G6"), Font, Brushes.DimGray, 5, y); }
        using var pen = new Pen(Color.FromArgb(35, 90, 135), 2);
        PointF? previous = null; int prevIndex = -2;
        foreach (var p in points) { var point = new PointF(100 + w * p.Index / (samples.Count - 1), 35 + h * (float)((hi - p.Value) / (hi - lo))); if (previous.HasValue && p.Index == prevIndex + 1) g.DrawLine(pen, previous.Value, point); previous = point; prevIndex = p.Index; }
        g.DrawString($"{selected.Label} · log10(value) · zero, negative and out-of-range samples omitted", Font, Brushes.Black, 100, h + 50);
    }
    void PaintDiagram(object? sender, PaintEventArgs e)
    {
        var list = values.ToList(); var g = e.Graphics; g.TranslateTransform(diagram.AutoScrollPosition.X, diagram.AutoScrollPosition.Y);
        diagram.AutoScrollMinSize = new Size(900, ((list.Count + 2) / 3) * 130 + 80);
        if (list.Count == 0) { g.DrawString("Define symbols in Values, then connect them through gain formulas.", Font, Brushes.DimGray, 30, 30); return; }
        Rectangle Box(int i) => new(40 + i % 3 * 290, 40 + i / 3 * 130, 230, 75);
        using var arrow = new Pen(Color.SlateGray, 1.5f) { CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 5) };
        var symbols = list.Select((v, i) => (v.Symbol, Index: i)).Where(v => !string.IsNullOrEmpty(v.Symbol)).GroupBy(v => v.Symbol).ToDictionary(group => group.Key, group => group.First().Index);
        for (int i = 0; i < list.Count; i++) foreach (string reference in Regex.Matches(list[i].Formula ?? "", "[A-Za-z_][A-Za-z0-9_]*").Select(m => m.Value).Distinct())
        { if (symbols.TryGetValue(reference, out int j) && i != j) { var from = Box(j); var to = Box(i); g.DrawLine(arrow, from.Right, from.Top + 37, to.Left, to.Top + 37); } }
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
            File.WriteAllLines(d.FileName, new[] { "elapsed_seconds," + string.Join(",", channels.Select(v => Q(v.Label))) }.Concat(samples.Select(s => Q(s.Time) + "," + string.Join(",", channels.Select(v => Q(s.State[v.Key].ToString())))))); status.Text = "Simulation table exported.";
        });
    }
    void ExportUnity()
    {
        Pause(); using var d = new FolderBrowserDialog { Description = "Choose a parent folder for a new Unity export" };
        if (d.ShowDialog(this) == DialogResult.OK) Try(() => { var p = Collect(); p.Validate(); if (p.Values.Count == 0 && p.Workspace.Nodes.Count == 0) throw new InvalidOperationException("Create a workspace or define values first."); string folder = Path.Combine(d.SelectedPath, "Incranalytic-Unity-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")); UnityExport.Write(folder, p, ExportRuntime(), ExportRuntime("Ifcloud.cs"), ExportRuntime("WorkspaceRuntime.cs")); status.Text = "Unity scripts exported: " + folder; });
    }
    static string ExportRuntime(string source = "ScientificNumber.cs")
    {
        var assembly = typeof(StudioForm).Assembly;
        using var stream = assembly.GetManifestResourceStream("Incranalytic.Export." + source)!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public void VerifyStudio(string reportPath)
    {
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add("PASS: " + message); }
        Check(values.Count == 0 && tabs.TabPages[0].Text == "Workspace" && workspace.Graph.Nodes.Count == 0, "Startup has a blank Workspace and blank Values sheet");
        SetProject(new StudioProject { Values = [new() { Symbol = "Q", Name = "Any chosen meaning", Initial = "0", Formula = "R" }, new() { Symbol = "R", Initial = "1e1000", Formula = "0" }] }, null);
        Check(Collect().Values[0].Name == "Any chosen meaning", "Values binding preserves arbitrary symbol meanings");
        EnsureSession(); Check(Advance(true), "Step succeeds through studio controls");
        Check(session!.State["Q"].ToString() == "1e1000" && history.Rows.Count == 2, "Simulation table displays huge scientific values");
        Run(); Check(timer.Enabled, "Run starts simulation timer"); Pause(); Check(!timer.Enabled, "Pause stops simulation timer");
        Advance(true); tabs.SelectedTab = tabs.TabPages.Cast<StudioPage>().Single(t => t.Text == "Graph"); graph.Refresh(); tabs.SelectedTab = tabs.TabPages.Cast<StudioPage>().Single(t => t.Text == "Dependencies"); diagram.Refresh();
        Check(samples.Count == 3 && plotted.Items.Count == 2, "Graph and dependency views render with simulation data");
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "ui-export-" + Guid.NewGuid().ToString("N"));
        UnityExport.Write(folder, Collect(), ExportRuntime()); Check(File.Exists(Path.Combine(folder, "ScientificNumber.cs")), "Installed app exports its embedded C# runtime");
        Reset(); Check(session == null && !editor.ReadOnly && samples.Count == 0, "Reset clears history and unlocks editing");
        SetProject(new StudioProject { Workspace = WorkspaceFiles.ExampleLoop() }, null);
        EnsureSession(); Advance(true); Advance(true);
        Check(session!.LastWorkspace.Outputs[session.Project.Workspace.Nodes.Single(n => n.Kind == "Output").Id]["value"].ToString() == "2e0", "Workspace-only loop simulates through studio controls");
        Check(channels.Count == 1 && history.RowCount == 3, "Workspace output appears in the shared table and graph channels");
        tabs.SelectedIndex = 0; workspace.Canvas.Refresh(); workspace.Canvas.Select(workspace.Graph.Nodes.Single(n => n.Kind == "Module"));
        Check(session.LastWorkspace.Traces.Values.Any(t => t.Any(s => s.Contains("true"))), "Conditional branch trace powers the number inspector");
        string workspaceFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "ui-workspace-export-" + Guid.NewGuid().ToString("N"));
        UnityExport.Write(workspaceFolder, Collect(), ExportRuntime(), ExportRuntime("Ifcloud.cs"), ExportRuntime("WorkspaceRuntime.cs"));
        Check(File.Exists(Path.Combine(workspaceFolder, "Ifcloud.cs")), "Installed workspace exports its embedded IFCLOUD runtime");
        Reset(); workspace.AddNode("Input"); int added = workspace.Graph.Nodes.Count; workspace.Undo(); Check(workspace.Graph.Nodes.Count == added - 1, "Workspace editing supports undo");
        Check(navigation.Count == 5 && tabs.TabPages.All(t => !t.Text.Contains("Index")), "Five work views use the sidebar; manuals are separate from work navigation");
        Check(!profile.Preferences.ShowSimulationSettings && !simulationSettings!.Visible && profile.Preferences.EnableRecovery, "Simulation settings start hidden and local recovery starts enabled");
        profile.Preferences.ShowGrid = false; profile.Preferences.SnapToGrid = false; profile.Preferences.ShowBlockValues = false; ApplyPreferences();
        Check(!workspace.Canvas.ShowGrid && !workspace.Canvas.SnapToGrid && !workspace.Canvas.ShowBlockValues, "View preferences reach the workspace canvas");
        profile.Preferences.ShowGrid = profile.Preferences.SnapToGrid = profile.Preferences.ShowBlockValues = true; ApplyPreferences();
        ShowManual(false); ShowManual(true);
        Check(systemManual!.Visible && ifcloudManual!.Visible && systemManual.TopicCount > 10 && ifcloudManual.TopicCount > 5, "Help opens independent searchable System and IFCLOUD manuals");
        systemManual!.Close(); ifcloudManual!.Close();
        ShowWelcome(); Check(welcome!.Visible && welcome.PageCount == 6, "Getting started opens a six-page walkthrough with an optional example"); welcome.Close();
        MarkDirty(); CaptureRecovery();
        Check(Directory.GetFiles(Path.Combine(profile.Root, "recovery"), "snapshot-*.json").Length == 1, "Dirty projects create a separate local recovery snapshot");
        ClearRecovery();
        if (DistributionChannel.IsStore)
        {
            var helpMenu = MainMenuStrip!.Items.Cast<ToolStripMenuItem>().Single(m => m.Text == "&Help");
            Check(helpMenu.DropDownItems.Cast<ToolStripItem>().Any(m => m.Text == "Microsoft Store updates…") && !helpMenu.DropDownItems.Cast<ToolStripItem>().Any(m => m.Text == "Install / manage versions…") && Terms.Contains("MICROSOFT STORE EDITION"), "Store edition uses Store update guidance and Store-specific privacy terms");
        }
        workspace.Canvas.VerifyGestures(); Check(workspace.Graph.Edges.Count == 1, "Canvas mouse handlers wire ports, drag/snap blocks, zoom and pan");
        dirty = false; File.WriteAllLines(reportPath, checks);
    }
    public static string Terms { get { using var reader = new StreamReader(typeof(StudioForm).Assembly.GetManifestResourceStream("Incranalytic.Terms")!); return reader.ReadToEnd(); } }
    const string Guide = "INCRANALYTIC — SYSTEM INDEX\n\nVALUES\nSymbols are case-sensitive and user-defined. A can mean coins in one project and a multiplier in another. Use letters, digits and underscores; begin with a letter or underscore. Display names can contain spaces.\n\nINITIAL VALUE\nStarting number, e.g. 0, 1.25, 1e1000. All results display scientific notation.\n\nGAIN FORMULA\nAmount added to a symbol. Per-second mode multiplies it by the chosen interval; per-tick mode adds it once per tick. 0 keeps a value constant. Examples: B * C, A * 0.05, min(B, C).\n\nFUNCTIONS AND OPERATORS\n+ - * / ^ ( )\nabs(x), min(a,b), max(a,b)\nPowers require integer exponents from -100000 to 100000. Direct e notation supports much larger magnitudes. Values formulas use arithmetic. Use IFCLOUD modules in Workspace for comparisons and conditions. Fractional powers remain unsupported.\n\nTIME\nChoose interval in seconds. Time has no automatic project symbol. To model elapsed time, define any symbol with initial 0 and gain 1 in per-second mode.\n\nSIMULATION TICK\nOne update. Every formula reads the previous state; all results apply together. Example: A starts at 0 and gains B; B starts at 1 and gains 1. After two one-second steps A=3, B=3.\n\nRUN / PAUSE / STEP / RESET\nRun advances with elapsed wall time. Pause stops advancing. Step applies one interval. Reset clears recorded results, restores initial conditions on the next run, and unlocks editing. Save retains the authored model, not a running simulation. History retains up to 2,001 samples and approximately 100,000 signal cells.\n\nPRECISION AND BIG NUMBERS\nChoose 1–1,000 significant digits. Results truncate toward zero after each operation. Numbers use integer coefficients and arbitrary-size integer exponents, limited by memory. This does not mean infinite storage or exact arithmetic at finite precision.\n\nGRAPHS\nGraph plots positive values on a log10 vertical axis. Zero/negative/out-of-range points are omitted; the table retains their values. Horizontal axis is sample index; elapsed time is in the table.\n\nDEPENDENCY GRAPH\nShows which symbols influence each gain formula. Connect symbols edits the target gain formula. Feedback and self-reference use previous-tick values, avoiding same-tick circular evaluation.\n\nGROWTH REGIME\nLinear: constant gain. Exponential: gain proportional to existing value.\n\nCOMPOUNDING\nGrowth that acts on previous growth. Interval and modifier order matter.\n\nMODIFIER PIPELINE\nOrder of bonuses; write explicit parentheses to express your chosen order.\n\nBOTTLENECK / THROUGHPUT\nA limiting input / quantity processed per time. min(A,B) can model a simple limiting rate.\n\nSOFT CAP\nA rule reducing growth beyond a threshold. IFCLOUD modules can author thresholds and branches; fractional powers remain unsupported.\n\nCOST ESCALATION\nExample: baseCost * growth ^ owned, with integer owned.\n\nAFFORDABILITY / PAYBACK\nCost divided by positive income or production increase. Variable income needs a time simulation.\n\nPRESTIGE / RESET LAYERS\nAuthored reset mechanics; specialized event and reset nodes are future work.\n\nOFFLINE SIMULATION\nA modeled time interval. A constant rate can be integrated as rate × duration; changing rates require explicit simulation.\n\nPROGRESSION ENVELOPE\nA creator's expected bounds or target outcomes. Incranalytic does not decide which progression is correct.\n\nC# / UNITY EXPORT\nExports a MonoBehaviour plus shared scientific arithmetic. Copy every exported .cs file into a Unity project's Assets folder. Attach IncranalyticSystem to a GameObject. The export README explains integration, precision, and testing.\n\nPURPOSE\nYou create the rules. Incranalytic calculates, displays and tests them. It does not use AI to generate your system.";
}
