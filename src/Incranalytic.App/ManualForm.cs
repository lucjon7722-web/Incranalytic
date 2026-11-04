namespace Incranalytic.App;

public sealed record ManualTopic(string Title, string Body) { public override string ToString() => Title; }

public sealed class ManualForm : Form
{
    readonly ListBox topics = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    readonly RichTextBox body = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Font = new("Consolas", 11), DetectUrls = false, BorderStyle = BorderStyle.None };
    readonly List<ManualTopic> all;
    readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Search topics, terms or examples…" };
    public int TopicCount => all.Count;
    public ManualForm(string title, string source, IEnumerable<ManualTopic>? introduction = null)
    {
        Text = title; Width = 1080; Height = 760; MinimumSize = new(760, 500); StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 10);
        all = introduction?.ToList() ?? [];
        string heading = "Overview"; var buffer = new System.Text.StringBuilder();
        void Flush() { if (buffer.Length > 0) all.Add(new(heading, buffer.ToString().Trim())); buffer.Clear(); }
        foreach (string line in source.Replace("\r", "").Split('\n'))
        {
            if (line.Length > 1 && System.Text.RegularExpressions.Regex.IsMatch(line, "^[A-Z][A-Z0-9 /?()–—.:+#-]*$") && !line.StartsWith("IFCLOUD 0.")) { Flush(); heading = line; }
            else buffer.AppendLine(line);
        }
        Flush();
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1000, 650), SplitterDistance = 250, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(topics); split.Panel2.Padding = new(14); split.Panel2.Controls.Add(body);
        var headingBar = new Label { Text = title + "  •  choose a topic or search", Dock = DockStyle.Top, Height = 48, Padding = new(12), Font = new(Font, FontStyle.Bold), BackColor = Color.FromArgb(231, 229, 215) };
        Controls.Add(split); Controls.Add(search); Controls.Add(headingBar);
        search.TextChanged += (_, _) => Filter(); topics.SelectedIndexChanged += (_, _) => ShowTopic();
        Filter();
    }
    void Filter()
    {
        topics.Items.Clear(); foreach (var topic in all.Where(t => (t.Title + " " + t.Body).Contains(search.Text, StringComparison.OrdinalIgnoreCase))) topics.Items.Add(topic);
        if (topics.Items.Count > 0) topics.SelectedIndex = 0; else body.Text = "No matching topic. Try another word.";
    }
    void ShowTopic()
    {
        if (topics.SelectedItem is not ManualTopic topic) return;
        body.Text = topic.Title + "\n\n" + topic.Body;
        if (search.Text.Length > 0) body.Find(search.Text, RichTextBoxFinds.None);
    }
}
