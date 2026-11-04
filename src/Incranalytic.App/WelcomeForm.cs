namespace Incranalytic.App;

public sealed class WelcomeForm : Form
{
    readonly Label heading = new() { Dock = DockStyle.Top, Height = 70, Padding = new(20), Font = new("Segoe UI", 17, FontStyle.Bold) };
    readonly RichTextBox body = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None, Font = new("Segoe UI", 12), DetectUrls = false };
    readonly CheckBox showAgain = new() { Text = "Show this walkthrough when the studio starts", AutoSize = true };
    readonly Button back = new() { Text = "Back", AutoSize = true }, next = new() { Text = "Next", AutoSize = true };
    int page;
    static readonly (string Title, string Text)[] Pages =
    [
        ("Welcome to Incranalytic", "Build and test your own incremental systems.\n\nYou define the values and rules. Incranalytic calculates and displays what they do. No AI service generates systems in this software.\n\nThis walkthrough takes you from a blank workspace to a tested loop. You can keep it open while working, or reopen it from Help → Getting started."),
        ("1 · Choose a work view", "Use the left sidebar to switch between Workspace, Values, Simulation Table, Graph and Dependencies.\n\nWorkspace is the main grid. Values is an optional dictionary of your own symbols: A could mean coins, production, or something completely different.\n\nSimulation settings are hidden by default. Use the toolbar toggle or View menu when you need interval and precision controls."),
        ("2 · Place and connect blocks", "In Workspace, open Add block.\n\n• Input supplies a constant or a Values symbol.\n• Module contains IFCLOUD code you write.\n• Previous step remembers a number across ticks.\n• Output records a connected result.\n\nDrag a right-side output port to a left-side input port. Drag block bodies to move them. Middle/right drag pans; the mouse wheel zooms. Grid and snapping can be toggled in View."),
        ("3 · Write a conditional rule", "Double-click a module to open its code editor. Declare input/output ports first, then write assignments and if/else rules.\n\nExample:\ninput amount;\noutput result;\nif (amount < 10) {\n    result = amount + 1;\n} else {\n    result = amount;\n}\n\nUse Validate code and Apply valid code, or Save draft if it is unfinished. Help → IFCLOUD Manual explains the supported language."),
        ("4 · Run and inspect", "Run advances time. Pause stops it. Step completes one tick. Reset clears simulation history and unlocks editing.\n\nSelect a block to see Why this number?: the actual input values, conditions, branch decisions and outputs from its last successful tick.\n\nOutput blocks appear in the table and graph. The first row is a preview, not a completed tick. Previous-step blocks make feedback timing explicit."),
        ("5 · Save, recover and export", "Save your project as .incr. Saving keeps a previous-file .bak backup. Automatic local recovery is enabled by default and checks for changes every 30 seconds; it never overwrites the saved project.\n\nRecovery can preserve unfinished module code. Use Save draft or Save project for immediate checkpoints. After an interruption, the next startup offers available recovery copies.\n\nExport Unity C# when the system validates. Test the integration in your Unity game. Separate System and IFCLOUD manuals are always available from Help.")
    ];
    public int PageCount => Pages.Length;
    public WelcomeForm(bool alwaysShow, Func<bool> startBlank, Func<bool> example, Action openManual, Action<bool> onClosed)
    {
        Text = "Getting started — Incranalytic Studio"; Width = 850; Height = 710; MinimumSize = new(700, 600); StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 10);
        Padding = new(16); BackColor = Color.FromArgb(235, 233, 220); showAgain.Checked = alwaysShow;
        var choices = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
        var blank = new Button { Text = "Start blank", AutoSize = true }; blank.Click += (_, _) => { if (startBlank()) Close(); };
        var load = new Button { Text = "Load example loop", AutoSize = true }; load.Click += (_, _) => { if (example()) Close(); };
        var manual = new Button { Text = "Open manuals", AutoSize = true }; manual.Click += (_, _) => openManual();
        var finish = new Button { Text = "Close guide", AutoSize = true }; finish.Click += (_, _) => Close();
        choices.Controls.AddRange([blank, load, manual, finish]);
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 }; navigation.Controls.AddRange([back, next]);
        var option = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 }; option.Controls.Add(showAgain);
        Controls.Add(body); Controls.Add(heading); Controls.Add(navigation); Controls.Add(option); Controls.Add(choices);
        back.Click += (_, _) => { if (page > 0) page--; Render(); }; next.Click += (_, _) => { if (page < Pages.Length - 1) page++; Render(); };
        FormClosed += (_, _) => onClosed(showAgain.Checked); Render();
    }
    void Render() { heading.Text = Pages[page].Title; body.Text = Pages[page].Text; back.Enabled = page > 0; next.Enabled = page < Pages.Length - 1; }
}
