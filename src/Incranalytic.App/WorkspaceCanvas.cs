using System.Drawing.Drawing2D;
using System.ComponentModel;
using Incranalytic.Core;

namespace Incranalytic.App;

public sealed class WorkspaceCanvas : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WorkspaceGraph Graph { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WorkspaceEvaluation? Evaluation { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Editable { get; set; } = true;
    public WorkspaceNode? Selected { get; private set; }
    public event Action? Changing, Changed, SelectionChanged, EditRequested;
    public event Action<string>? Message;
    PointF pan = new(80, 100); float zoom = 1;
    Point last; bool panning, dragging, moved;
    (WorkspaceNode Node, string Port)? wire;
    readonly Dictionary<string, (string Code, string Kind, string[] Inputs, string[] Outputs)> portCache = [];
    public WorkspaceCanvas() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.FromArgb(244, 244, 236); TabStop = true; }
    public PointF CenterWorld => World(new Point(Width / 2, Height / 2));
    PointF World(Point p) => new((p.X - pan.X) / zoom, (p.Y - pan.Y) / zoom);
    public void Select(WorkspaceNode? n) { Selected = n; SelectionChanged?.Invoke(); Invalidate(); }
    public void Fit()
    {
        if (Graph.Nodes.Count == 0) { pan = new(80, 100); zoom = 1; }
        else { float x = Graph.Nodes.Min(n => n.X), y = Graph.Nodes.Min(n => n.Y), w = Graph.Nodes.Max(n => n.X + 240) - x, h = Graph.Nodes.Max(n => n.Y + Box(n).Height) - y; zoom = Math.Clamp(Math.Min((Width - 100) / Math.Max(1, w), (Height - 100) / Math.Max(1, h)), .25f, 1.25f); pan = new(50 - x * zoom, 60 - y * zoom); }
        Invalidate();
    }
    (string[] Inputs, string[] Outputs) Ports(WorkspaceNode n)
    {
        if (!portCache.TryGetValue(n.Id, out var p) || p.Code != n.Code || p.Kind != n.Kind)
        {
            try { p = (n.Code, n.Kind, WorkspaceRuntime.InputPorts(n), WorkspaceRuntime.OutputPorts(n)); }
            catch { p = (n.Code, n.Kind, [], []); }
            portCache[n.Id] = p;
        }
        return (p.Inputs, p.Outputs);
    }
    RectangleF Box(WorkspaceNode n) { var p = Ports(n); return new(n.X, n.Y, 240, Math.Max(100, 65 + Math.Max(p.Inputs.Length, p.Outputs.Length) * 24)); }
    PointF Port(WorkspaceNode n, string name, bool output)
    {
        var p = Ports(n); int index = Array.IndexOf(output ? p.Outputs : p.Inputs, name);
        return index < 0 ? new(float.NaN, float.NaN) : new(n.X + (output ? 240 : 0), n.Y + 49 + index * 24);
    }
    (WorkspaceNode Node, string Port)? HitPort(PointF at, bool output)
    {
        foreach (var n in Graph.Nodes.AsEnumerable().Reverse()) foreach (var name in output ? Ports(n).Outputs : Ports(n).Inputs)
        { var p = Port(n, name, output); if (Math.Abs(p.X - at.X) <= 12 / zoom && Math.Abs(p.Y - at.Y) <= 12 / zoom) return (n, name); }
        return null;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float spacing = 32 * zoom;
        using var grid = new Pen(Color.FromArgb(221, 222, 214));
        for (float x = pan.X % spacing; x < Width; x += spacing) g.DrawLine(grid, x, 0, x, Height);
        for (float y = pan.Y % spacing; y < Height; y += spacing) g.DrawLine(grid, 0, y, Width, y);
        g.TranslateTransform(pan.X, pan.Y); g.ScaleTransform(zoom, zoom);
        var viewport = new RectangleF(-pan.X / zoom, -pan.Y / zoom, Width / zoom, Height / zoom);
        using var groupPen = new Pen(Color.FromArgb(153, 165, 151), 1) { DashStyle = DashStyle.Dash };
        foreach (var group in Graph.Nodes.Where(n => n.Group.Length > 0).GroupBy(n => n.Group))
        {
            var bounds = group.Select(Box).Aggregate(RectangleF.Union); bounds.Inflate(24, 30);
            g.DrawRectangle(groupPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            g.DrawString(group.Key, Font, Brushes.DarkOliveGreen, bounds.X + 5, bounds.Y + 3);
        }
        using var line = new Pen(Color.SlateGray, 2) { CustomEndCap = new AdjustableArrowCap(3, 4) };
        var lookup = Graph.Nodes.ToDictionary(n => n.Id);
        foreach (var edge in Graph.Edges)
        {
            if (!lookup.TryGetValue(edge.From, out var from) || !lookup.TryGetValue(edge.To, out var to)) continue;
            var a = Port(from, edge.FromPort, true); var b = Port(to, edge.ToPort, false); if (float.IsNaN(a.X) || float.IsNaN(b.X)) continue;
            float bend = Math.Max(65, Math.Abs(b.X - a.X) / 2); g.DrawBezier(line, a, new(a.X + bend, a.Y), new(b.X - bend, b.Y), b);
        }
        if (wire is { } current) { var a = Port(current.Node, current.Port, true); var b = World(last); g.DrawLine(Pens.SteelBlue, a, b); }
        foreach (var n in Graph.Nodes)
        {
            var b = Box(n); if (!viewport.IntersectsWith(b)) continue;
            g.FillRectangle(n.Kind == "Previous" ? Brushes.LightGoldenrodYellow : Brushes.WhiteSmoke, b);
            using var border = new Pen(Selected == n ? Color.DodgerBlue : Color.SlateGray, Selected == n ? 3 : 1);
            g.DrawRectangle(border, b.X, b.Y, b.Width, b.Height);
            using var header = new SolidBrush(n.Kind == "Module" ? Color.FromArgb(55, 77, 94) : n.Kind == "Input" ? Color.FromArgb(66, 105, 84) : n.Kind == "Output" ? Color.FromArgb(104, 76, 117) : Color.FromArgb(121, 97, 44));
            g.FillRectangle(header, b.X, b.Y, b.Width, 29);
            g.DrawString(n.Title, Font, Brushes.White, new RectangleF(b.X + 8, b.Y + 6, b.Width - 12, 22));
            var ports = Ports(n);
            foreach (var name in ports.Inputs) { var p = Port(n, name, false); g.FillEllipse(Brushes.SteelBlue, p.X - 6, p.Y - 6, 12, 12); g.DrawString(name, Font, Brushes.Black, p.X + 10, p.Y - 8); }
            foreach (var name in ports.Outputs) { var p = Port(n, name, true); g.FillEllipse(Brushes.DarkOliveGreen, p.X - 6, p.Y - 6, 12, 12); var width = g.MeasureString(name, Font).Width; g.DrawString(name, Font, Brushes.Black, p.X - width - 10, p.Y - 8); }
            string summary = n.Kind == "Input" ? (n.ValueSymbol.Length > 0 ? "Values: " + n.ValueSymbol : n.Literal) : n.Kind == "Previous" ? "Previous step · initial " + n.Literal : n.Kind == "Module" ? "IFCLOUD · double-click to code" : "Recorded in table and graph";
            if (Evaluation != null && Evaluation.Outputs.TryGetValue(n.Id, out var output)) summary = string.Join(" · ", output.Select(p => p.Key + "=" + p.Value));
            g.DrawString(summary, Font, Brushes.DimGray, new RectangleF(b.X + 8, b.Bottom - 25, b.Width - 16, 22));
        }
        g.ResetTransform();
        g.DrawString($"{zoom:P0} · middle/right drag to pan · wheel to zoom · wire right port → left port", Font, Brushes.DimGray, 10, Height - 25);
        if (Graph.Nodes.Count == 0) g.DrawString("Add an Input or IFCLOUD Module to begin.\nDouble-click empty grid to create a custom module.", Font, Brushes.DimGray, 40, 40);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); last = e.Location;
        if (e.Button is MouseButtons.Middle or MouseButtons.Right) { panning = true; Capture = true; return; }
        if (e.Button != MouseButtons.Left) return;
        var at = World(e.Location);
        if (Editable && HitPort(at, true) is { } port) { wire = port; Capture = true; return; }
        var n = Graph.Nodes.LastOrDefault(n => Box(n).Contains(at)); Select(n);
        if (Editable && n != null) { Changing?.Invoke(); dragging = true; moved = false; Capture = true; }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (panning) { pan.X += e.X - last.X; pan.Y += e.Y - last.Y; }
        else if (dragging && Selected != null) { Selected.X = Math.Clamp(Selected.X + (e.X - last.X) / zoom, -1000000, 1000000); Selected.Y = Math.Clamp(Selected.Y + (e.Y - last.Y) / zoom, -1000000, 1000000); moved |= e.Location != last; }
        last = e.Location; if (panning || dragging || wire != null) Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (wire is { } from && HitPort(World(e.Location), false) is { } to && Editable)
        {
            Changing?.Invoke(); Graph.Edges.RemoveAll(edge => edge.To == to.Node.Id && edge.ToPort == to.Port);
            Graph.Edges.Add(new WorkspaceEdge { From = from.Node.Id, FromPort = from.Port, To = to.Node.Id, ToPort = to.Port }); Changed?.Invoke(); Message?.Invoke("Connected. Use Previous-step for feedback loops.");
        }
        if (dragging && moved && Selected != null) { Selected.X = (float)Math.Round(Selected.X / 32) * 32; Selected.Y = (float)Math.Round(Selected.Y / 32) * 32; Changed?.Invoke(); }
        wire = null; panning = dragging = false; Capture = false; Invalidate();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e); var before = World(e.Location); zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.12f : 1 / 1.12f), .25f, 2);
        pan = new(e.X - before.X * zoom, e.Y - before.Y * zoom); Invalidate();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); if (Editable && e.Button == MouseButtons.Left) EditRequested?.Invoke(); }
    internal void VerifyGestures()
    {
        var input = new WorkspaceNode { Kind = "Input", X = 0, Y = 0 };
        var output = new WorkspaceNode { Kind = "Output", X = 360, Y = 0 };
        Graph = new WorkspaceGraph { Nodes = [input, output] }; Editable = true; pan = new(80, 100); zoom = 1;
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 320, 149, 0));
        OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 440, 149, 0));
        OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 440, 149, 0));
        if (Graph.Edges.Count != 1 || Graph.Edges[0].From != input.Id || Graph.Edges[0].To != output.Id) throw new Exception("Port-to-port mouse wiring failed.");
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 120, 110, 0));
        OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 184, 174, 0));
        OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 184, 174, 0));
        if (input.X != 64 || input.Y != 64) throw new Exception("Block drag/snap failed.");
        OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 200, 200, 120));
        if (zoom <= 1) throw new Exception("Mouse-wheel zoom failed.");
        var before = pan; OnMouseDown(new MouseEventArgs(MouseButtons.Middle, 1, 200, 200, 0));
        OnMouseMove(new MouseEventArgs(MouseButtons.Middle, 0, 250, 250, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Middle, 1, 250, 250, 0));
        if (pan.X != before.X + 50 || pan.Y != before.Y + 50) throw new Exception("Canvas pan failed.");
    }
}
