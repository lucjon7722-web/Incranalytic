using System.ComponentModel;

namespace Incranalytic.App;

public sealed class StudioPage(string name) : Panel
{
    public StudioPage() : this("") { }
    public string ViewName { get; } = name;
}
public sealed class StudioViewHost : Panel
{
    public List<StudioPage> TabPages { get; } = [];
    int selected = -1;
    public event EventHandler? SelectedIndexChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value < 0 || value >= TabPages.Count) return;
            selected = value;
            for (int i = 0; i < TabPages.Count; i++) TabPages[i].Visible = i == value;
            TabPages[value].BringToFront(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StudioPage? SelectedTab { get => selected < 0 ? null : TabPages[selected]; set { if (value != null) SelectedIndex = TabPages.IndexOf(value); } }
    public void InitializeViews()
    {
        foreach (var page in TabPages) { page.Text = page.ViewName; page.Dock = DockStyle.Fill; Controls.Add(page); }
        SelectedIndex = 0;
    }
}
