using Incranalytic.Core;

namespace Incranalytic.App;

public sealed class WorkspacePane : UserControl
{
    public readonly WorkspaceCanvas Canvas = new() { Dock = DockStyle.Fill };
    readonly TextBox title = Field(), group = Field(), literal = Field(), symbol = Field();
    readonly RichTextBox why = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Font = new("Consolas", 9), DetectUrls = false };
    readonly Label kind = new() { AutoSize = true, Font = new("Segoe UI", 10, FontStyle.Bold) };
    readonly Button apply = new() { Text = "Apply block changes", AutoSize = true };
    readonly Stack<WorkspaceGraph> undo = new(), redo = new();
    readonly ToolStripMenuItem templates = new("Saved modules");
    bool editable = true;
    string resultCaption = "Run or Step to inspect calculations.";
    public event Action? Changed;
    public event Action<string>? Message;
    public WorkspaceGraph Graph => Canvas.Graph;
    static TextBox Field() => new() { Width = 275 };
    public WorkspacePane()
    {
        Dock = DockStyle.Fill;
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1200, 600), SplitterDistance = 875, FixedPanel = FixedPanel.Panel2, Panel2MinSize = 290 };
        split.Panel1.Controls.Add(Canvas);
        var props = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 300, Padding = new(8), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        props.Controls.Add(kind);
        void Add(string caption, Control control) { props.Controls.Add(new Label { Text = caption, AutoSize = true }); props.Controls.Add(control); }
        Add("Title", title); Add("Group (optional)", group); Add("Constant / initial memory", literal); Add("Values symbol (Input only; blank = constant)", symbol); props.Controls.Add(apply);
        var code = new Button { Text = "Edit IFCLOUD code…", Dock = DockStyle.Top, Height = 32 }; code.Click += (_, _) => Edit();
        var disconnect = new Button { Text = "Disconnect selected block", Dock = DockStyle.Top, Height = 30 }; disconnect.Click += (_, _) => Change(() => { if (Canvas.Selected is { } n) Graph.Edges.RemoveAll(e => e.From == n.Id || e.To == n.Id); });
        split.Panel2.Controls.Add(why); split.Panel2.Controls.Add(disconnect); split.Panel2.Controls.Add(code); split.Panel2.Controls.Add(props);
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(239, 237, 225) };
        var add = new ToolStripDropDownButton("Add block");
        foreach (string type in new[] { "Input", "Module", "Previous", "Output" }) add.DropDownItems.Add(type == "Previous" ? "Previous step" : type, null, (_, _) => AddNode(type));
        toolbar.Items.Add(add); toolbar.Items.Add("Edit", null, (_, _) => Edit()); toolbar.Items.Add("Delete", null, (_, _) => Delete());
        toolbar.Items.Add("Undo", null, (_, _) => Undo()); toolbar.Items.Add("Redo", null, (_, _) => Redo()); toolbar.Items.Add("Fit", null, (_, _) => Canvas.Fit());
        var library = new ToolStripDropDownButton("Modules");
        library.DropDownItems.Add("Save selected as reusable module", null, (_, _) => SaveTemplate());
        library.DropDownItems.Add("Import IFCLOUD module…", null, (_, _) => ImportModule());
        library.DropDownItems.Add("Export selected IFCLOUD…", null, (_, _) => ExportModule()); library.DropDownItems.Add(templates); toolbar.Items.Add(library);
        var systems = new ToolStripDropDownButton("Systems / groups");
        systems.DropDownItems.Add("Import reusable system…", null, (_, _) => ImportSystem());
        systems.DropDownItems.Add("Export selected group or workspace…", null, (_, _) => ExportSystem()); toolbar.Items.Add(systems);
        toolbar.Items.Add("Insert example loop", null, (_, _) => Change(() => { var at = Canvas.CenterWorld; WorkspaceFiles.Insert(Graph, WorkspaceFiles.ExampleLoop(), at.X, at.Y); Canvas.Fit(); }));
        Controls.Add(split); Controls.Add(toolbar);
        Canvas.Changing += Checkpoint; Canvas.Changed += Notify; Canvas.SelectionChanged += Selection; Canvas.EditRequested += () => { if (Canvas.Selected == null) AddNode("Module"); else Edit(); }; Canvas.Message += s => Message?.Invoke(s);
        Canvas.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) { Delete(); e.Handled = true; } };
        apply.Click += (_, _) => Change(() => { if (Canvas.Selected is not { } n) return; n.Title = title.Text; n.Group = group.Text; n.Literal = literal.Text; n.ValueSymbol = symbol.Text.Trim(); });
        Selection();
    }
    public void SetGraph(WorkspaceGraph graph) { Canvas.Graph = graph; Canvas.Evaluation = null; undo.Clear(); redo.Clear(); Canvas.Select(null); RefreshTemplates(); Canvas.Fit(); }
    public void SetEditable(bool value) { editable = Canvas.Editable = value; apply.Enabled = value; title.ReadOnly = group.ReadOnly = literal.ReadOnly = symbol.ReadOnly = !value; }
    void Checkpoint() { undo.Push(WorkspaceFiles.Clone(Graph)); if (undo.Count > 50) { var keep = undo.Take(50).Reverse().ToArray(); undo.Clear(); foreach (var item in keep) undo.Push(item); } redo.Clear(); }
    void Notify() { Canvas.Evaluation = null; resultCaption = "Edited model — Run or Step to inspect calculations."; Canvas.Invalidate(); Selection(); RefreshTemplates(); Changed?.Invoke(); }
    void Change(Action action)
    {
        if (!editable) { Message?.Invoke("Reset simulation to edit the workspace."); return; }
        var before = WorkspaceFiles.Clone(Graph); Checkpoint();
        try { action(); Graph.ValidateStructure(); Notify(); }
        catch (Exception ex) { Canvas.Graph = before; Canvas.Select(null); Canvas.Invalidate(); MessageBox.Show(this, ex.Message, "Workspace"); }
    }
    public void Undo() { if (!editable || undo.Count == 0) return; redo.Push(WorkspaceFiles.Clone(Graph)); Canvas.Graph = undo.Pop(); Canvas.Select(null); Notify(); }
    void Redo() { if (!editable || redo.Count == 0) return; undo.Push(WorkspaceFiles.Clone(Graph)); Canvas.Graph = redo.Pop(); Canvas.Select(null); Notify(); }
    public void AddNode(string type)
    {
        Change(() => { var at = Canvas.CenterWorld; var n = new WorkspaceNode { Kind = type, Title = type == "Module" ? "Custom module" : type == "Previous" ? "Previous step" : type, X = (float)Math.Round(at.X / 32) * 32, Y = (float)Math.Round(at.Y / 32) * 32, Literal = type == "Previous" ? "0" : "1" }; Graph.Nodes.Add(n); Canvas.Select(n); });
    }
    void Delete() { if (Canvas.Selected is not { } n) return; Change(() => { Graph.Edges.RemoveAll(e => e.From == n.Id || e.To == n.Id); Graph.Nodes.Remove(n); Canvas.Select(null); }); }
    void Selection()
    {
        var n = Canvas.Selected; kind.Text = n == null ? "Select a block" : n.Kind + " · Why this number?";
        title.Text = n?.Title ?? ""; group.Text = n?.Group ?? ""; literal.Text = n?.Literal ?? ""; symbol.Text = n?.ValueSymbol ?? "";
        if (n == null) why.Text = "WORKSPACE\n\nAdd blocks, drag them on the grid, and connect an output port on the right to an input port on the left.\n\nMiddle/right drag pans. Mouse wheel zooms.\n\nPrevious-step stores a value for the next tick, allowing deliberate feedback loops.\n\nSave your project at any time, including unfinished code or wiring. Use Validate before Run.";
        else if (Canvas.Evaluation?.Traces.TryGetValue(n.Id, out var trace) == true) why.Text = resultCaption + "\n\n" + string.Join("\n\n", trace) + "\n\nOutputs:\n" + string.Join("\n", Canvas.Evaluation.Outputs[n.Id].Select(p => p.Key + " = " + p.Value));
        else why.Text = resultCaption + "\n\n" + (n.Kind == "Module" ? n.Code : "Input uses a constant or a Values snapshot. Output records the connected number. Previous-step emits memory and remembers its next input after a successful tick.");
    }
    public void ShowEvaluation(WorkspaceEvaluation? result, long ticks) { Canvas.Evaluation = result; resultCaption = ticks == 0 ? "FIRST-TICK PREVIEW — no state has advanced." : "Last successful tick " + ticks + " — all inputs use that tick's snapshot."; Canvas.Invalidate(); Selection(); }
    void Edit()
    {
        if (!editable) { Message?.Invoke("Reset simulation to edit code."); return; }
        if (Canvas.Selected is not { } n) return;
        if (n.Kind != "Module") { Message?.Invoke("Edit this block's properties in the inspector, then Apply."); return; }
        using var d = new Form { Text = "IFCLOUD — " + n.Title, Width = 850, Height = 700, MinimumSize = new(650, 450), StartPosition = FormStartPosition.CenterParent };
        var text = new RichTextBox { Dock = DockStyle.Fill, Text = n.Code, Font = new("Consolas", 11), AcceptsTab = true, WordWrap = false, DetectUrls = false };
        var result = new Label { Dock = DockStyle.Bottom, Height = 70, Padding = new(8), Text = "Declare input/output ports first. Use if (...) { ... } else { ... }, let and assignments.\nSee the IFCLOUD Index tab for syntax and examples." };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        var validate = new Button { Text = "Validate code", AutoSize = true }; validate.Click += (_, _) => { try { var p = IfcloudProgram.Parse(text.Text); result.Text = "Valid syntax · inputs: " + string.Join(", ", p.Inputs) + " · outputs: " + string.Join(", ", p.Outputs) + "\nRun the workspace to validate branches with real inputs."; } catch (Exception ex) { result.Text = ex.Message; } };
        var save = new Button { Text = "Apply valid code", AutoSize = true }; var draft = new Button { Text = "Save draft", AutoSize = true }; var cancel = new Button { Text = "Cancel", AutoSize = true };
        void Apply(bool requireValid)
        {
            try
            {
                if (text.Text.Length > 32000) throw new FormatException("Code exceeds 32,000 characters.");
                IfcloudProgram? program = null; try { program = IfcloudProgram.Parse(text.Text); } catch { if (requireValid) throw; }
                Change(() => { n.Code = text.Text; if (program != null) Graph.Edges.RemoveAll(e => e.To == n.Id && !program.Inputs.Contains(e.ToPort) || e.From == n.Id && !program.Outputs.Contains(e.FromPort)); });
                d.Close();
            }
            catch (Exception ex) { result.Text = ex.Message; }
        }
        save.Click += (_, _) => Apply(true); draft.Click += (_, _) => Apply(false); cancel.Click += (_, _) => d.Close();
        buttons.Controls.AddRange([validate, save, draft, cancel]); d.Controls.Add(text); d.Controls.Add(result); d.Controls.Add(buttons); d.ShowDialog(this);
    }
    void RefreshTemplates()
    {
        templates.DropDownItems.Clear(); foreach (var template in Graph.Modules) templates.DropDownItems.Add(template.Name, null, (_, _) => Change(() => { var at = Canvas.CenterWorld; var n = new WorkspaceNode { Title = template.Name, Code = template.Code, X = at.X, Y = at.Y }; Graph.Nodes.Add(n); Canvas.Select(n); }));
        if (Graph.Modules.Count == 0) templates.DropDownItems.Add("No saved modules").Enabled = false;
    }
    void SaveTemplate()
    {
        if (Canvas.Selected is not { Kind: "Module" } n) return;
        Change(() => { IfcloudProgram.Parse(n.Code); string name = n.Title; while (Graph.Modules.Any(m => m.Name == name)) name += " copy"; Graph.Modules.Add(new ModuleTemplate { Name = name, Code = n.Code }); Message?.Invoke("Reusable module saved. New instances copy this code; editing an instance does not modify other instances."); });
    }
    void ImportModule()
    {
        if (!editable) return; using var d = new OpenFileDialog { Filter = "IFCLOUD module|*.ifcloud" };
        if (d.ShowDialog(this) == DialogResult.OK) Change(() => { if (new FileInfo(d.FileName).Length > 128000) throw new FormatException("Module file is too large."); var code = File.ReadAllText(d.FileName); IfcloudProgram.Parse(code); Graph.Modules.Add(new ModuleTemplate { Name = Path.GetFileNameWithoutExtension(d.FileName), Code = code }); });
    }
    void ExportModule()
    {
        if (Canvas.Selected is not { Kind: "Module" } n) return; using var d = new SaveFileDialog { Filter = "IFCLOUD module|*.ifcloud", FileName = "module.ifcloud" };
        if (d.ShowDialog(this) == DialogResult.OK) { try { File.WriteAllText(d.FileName, n.Code); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    }
    void ImportSystem()
    {
        if (!editable) return; using var d = new OpenFileDialog { Filter = "Reusable workspace system|*.ifgraph" };
        if (d.ShowDialog(this) == DialogResult.OK) Change(() => { var source = WorkspaceFiles.LoadGroup(d.FileName); var at = Canvas.CenterWorld; WorkspaceFiles.Insert(Graph, source, at.X, at.Y); Canvas.Fit(); });
    }
    void ExportSystem()
    {
        string selectedGroup = Canvas.Selected?.Group ?? ""; using var d = new SaveFileDialog { Filter = "Reusable workspace system|*.ifgraph", FileName = "system.ifgraph", Title = selectedGroup.Length > 0 ? "Export group: " + selectedGroup + " (internal wires only)" : "Export entire workspace" };
        if (d.ShowDialog(this) == DialogResult.OK) { try { WorkspaceFiles.SaveGroup(d.FileName, Graph, selectedGroup); Message?.Invoke("Exported internal connections. Values references and external group connections must be supplied when reusing the system."); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    }
}
