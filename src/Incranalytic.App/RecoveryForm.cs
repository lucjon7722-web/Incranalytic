using Incranalytic.Core;

namespace Incranalytic.App;

public sealed class RecoveryForm : Form
{
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    public RecoveryCandidate? Selected { get; private set; }
    sealed record Item(RecoveryCandidate Candidate)
    {
        public override string ToString() => (Candidate.Snapshot.OriginalPath == null ? "Untitled project" : Path.GetFileName(Candidate.Snapshot.OriginalPath)) + " — " + Candidate.Snapshot.SavedAtUtc.ToLocalTime().ToString("g");
    }
    public RecoveryForm(IEnumerable<RecoveryCandidate> candidates)
    {
        Text = "Recover unsaved work"; Width = 720; Height = 380; StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 10);
        var intro = new Label { Dock = DockStyle.Top, Height = 100, Padding = new(14), Text = "Local recovery copies are available from an earlier session. Restore a copy to an unsaved project, then Save when ready. Your saved project is not overwritten. Unfinished code may need validation.\n\nKeep for later leaves all these copies available for another startup." };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new(8) };
        var restore = new Button { Text = "Restore selected", AutoSize = true }; restore.Click += (_, _) => { if (list.SelectedItem is Item item) { Selected = item.Candidate; DialogResult = DialogResult.OK; Close(); } };
        var later = new Button { Text = "Keep for later", AutoSize = true }; later.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        actions.Controls.AddRange([restore, later]);
        Controls.Add(list); Controls.Add(intro); Controls.Add(actions);
        foreach (var candidate in candidates) list.Items.Add(new Item(candidate)); if (list.Items.Count > 0) list.SelectedIndex = 0;
        CancelButton = later; AcceptButton = restore;
    }
}
