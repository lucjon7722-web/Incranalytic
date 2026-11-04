using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Incranalytic.App;

public sealed class IfcloudEditor : UserControl
{
    public readonly RichTextBox Source = new() { Dock = DockStyle.Fill, Font = new("Consolas", 11), AcceptsTab = true, WordWrap = false, DetectUrls = false, MaxLength = 32000, BorderStyle = BorderStyle.None };
    readonly Panel gutter = new() { Dock = DockStyle.Left, Width = 54, BackColor = Color.FromArgb(239, 237, 225) };
    readonly CheckBox colors = new() { Text = "Keyword colors", Checked = true, AutoSize = true };
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 250 };
    bool formatting;
    static readonly Regex Tokens = new(@"//[^\r\n]*|\b(input|output|let|if|else|true|false|min|max|abs)\b|\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b", RegexOptions.Compiled);
    [DllImport("user32.dll")] static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint SendPoint(nint window, int message, nint wParam, ref Point point);
    public IfcloudEditor(string code)
    {
        Dock = DockStyle.Fill;
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, BackColor = gutter.BackColor };
        var lines = new CheckBox { Text = "Line numbers", Checked = true, AutoSize = true };
        options.Controls.AddRange([lines, colors]); Controls.Add(Source); Controls.Add(gutter); Controls.Add(options);
        Source.Text = code;
        lines.CheckedChanged += (_, _) => gutter.Visible = lines.Checked;
        colors.CheckedChanged += (_, _) => Highlight();
        Source.TextChanged += (_, _) => { if (formatting) return; debounce.Stop(); debounce.Start(); gutter.Invalidate(); };
        Source.VScroll += (_, _) => gutter.Invalidate(); Source.Resize += (_, _) => gutter.Invalidate();
        gutter.Paint += (_, e) =>
        {
            int first = Source.GetLineFromCharIndex(Source.GetCharIndexFromPosition(new(0, 0)));
            int last = Source.GetLineFromCharIndex(Source.GetCharIndexFromPosition(new(0, Source.ClientSize.Height)));
            for (int line = first; line <= last; line++)
            {
                int index = Source.GetFirstCharIndexFromLine(line); if (index < 0) break;
                int y = Source.GetPositionFromCharIndex(index).Y;
                TextRenderer.DrawText(e.Graphics, (line + 1).ToString(), Source.Font, new Rectangle(0, y, gutter.Width - 5, Source.Font.Height + 3), Color.DimGray, TextFormatFlags.Right | TextFormatFlags.NoPadding);
            }
        };
        debounce.Tick += (_, _) => { debounce.Stop(); Highlight(); };
        Source.HandleCreated += (_, _) => BeginInvoke(new Action(Highlight));
    }
    void Highlight()
    {
        if (!Source.IsHandleCreated || IsDisposed) return;
        int start = Source.SelectionStart, length = Source.SelectionLength; Point scroll = new();
        formatting = true; SendPoint(Source.Handle, 0x4DD, 0, ref scroll); SendMessage(Source.Handle, 0xB, 0, 0);
        try
        {
            Source.SelectAll(); Source.SelectionColor = Color.Black;
            if (colors.Checked) foreach (Match match in Tokens.Matches(Source.Text))
            {
                Source.Select(match.Index, match.Length);
                Source.SelectionColor = match.Value.StartsWith("//") ? Color.DarkGreen : match.Groups[1].Success ? Color.FromArgb(34, 65, 160) : Color.FromArgb(145, 58, 25);
            }
            Source.Select(start, length); if (length == 0) Source.SelectionColor = Color.Black;
        }
        finally { SendPoint(Source.Handle, 0x4DE, 0, ref scroll); SendMessage(Source.Handle, 0xB, 1, 0); Source.Invalidate(); gutter.Invalidate(); formatting = false; }
    }
    protected override void Dispose(bool disposing) { if (disposing) debounce.Dispose(); base.Dispose(disposing); }
}
