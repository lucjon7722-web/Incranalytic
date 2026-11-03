using System.Text.Json;

namespace Incranalytic.Core;

public static class WorkspaceFiles
{
    public static WorkspaceGraph Clone(WorkspaceGraph graph) => JsonSerializer.Deserialize<WorkspaceGraph>(JsonSerializer.Serialize(graph))!;
    public static void SaveGroup(string path, WorkspaceGraph graph, string group)
    {
        var copy = Clone(graph);
        if (group.Length > 0) copy.Nodes.RemoveAll(n => n.Group != group);
        var ids = copy.Nodes.Select(n => n.Id).ToHashSet(); copy.Edges.RemoveAll(e => !ids.Contains(e.From) || !ids.Contains(e.To));
        copy.ValidateStructure(); File.WriteAllText(path, JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static WorkspaceGraph LoadGroup(string path)
    {
        if (new FileInfo(path).Length > 20_000_000) throw new FormatException("System file exceeds 20 MB.");
        var graph = JsonSerializer.Deserialize<WorkspaceGraph>(File.ReadAllText(path)) ?? throw new FormatException("Invalid system file."); graph.ValidateStructure(); return graph;
    }
    public static void Insert(WorkspaceGraph target, WorkspaceGraph source, float x, float y)
    {
        var copy = Clone(source); var ids = copy.Nodes.ToDictionary(n => n.Id, _ => Guid.NewGuid().ToString("N"));
        float minX = copy.Nodes.Count > 0 ? copy.Nodes.Min(n => n.X) : 0, minY = copy.Nodes.Count > 0 ? copy.Nodes.Min(n => n.Y) : 0;
        foreach (var n in copy.Nodes) { n.Id = ids[n.Id]; n.X = Math.Clamp(x + n.X - minX, -1000000, 1000000); n.Y = Math.Clamp(y + n.Y - minY, -1000000, 1000000); }
        foreach (var e in copy.Edges) { e.From = ids[e.From]; e.To = ids[e.To]; }
        var candidate = Clone(target); candidate.Nodes.AddRange(copy.Nodes); candidate.Edges.AddRange(copy.Edges); candidate.ValidateStructure();
        target.Nodes.AddRange(copy.Nodes); target.Edges.AddRange(copy.Edges);
    }
    public static WorkspaceGraph ExampleLoop()
    {
        var prior = new WorkspaceNode { Kind = "Previous", Title = "Previous total", Literal = "0", X = 0, Y = 0, Group = "Counter" };
        var add = new WorkspaceNode { Title = "Conditional increment", X = 340, Y = 0, Group = "Counter", Code = "input previous;\noutput total;\n\n// A custom rule: increment until the total reaches 10.\nif (previous < 10) {\n    total = previous + 1;\n} else {\n    total = previous;\n}\n" };
        var output = new WorkspaceNode { Kind = "Output", Title = "Total", X = 680, Y = 0, Group = "Counter" };
        return new WorkspaceGraph { Nodes = [prior, add, output], Edges = [new() { From = prior.Id, To = add.Id, ToPort = "previous" }, new() { From = add.Id, FromPort = "total", To = prior.Id, ToPort = "next" }, new() { From = add.Id, FromPort = "total", To = output.Id }] };
    }
}
