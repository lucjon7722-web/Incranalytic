#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Incranalytic.Core
{
    public sealed class WorkspaceNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Kind { get; set; } = "Module";
        public string Title { get; set; } = "Module";
        public string Group { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public string Literal { get; set; } = "1";
        public string ValueSymbol { get; set; } = "";
        public string Code { get; set; } = "input value;\noutput result;\n\nif (value >= 10) {\n    result = value * 2;\n} else {\n    result = value;\n}\n";
    }
    public sealed class WorkspaceEdge
    {
        public string From { get; set; } = "";
        public string FromPort { get; set; } = "value";
        public string To { get; set; } = "";
        public string ToPort { get; set; } = "value";
    }
    public sealed class ModuleTemplate
    {
        public string Name { get; set; } = "Module";
        public string Code { get; set; } = "output result; result = 0;";
    }
    public sealed class WorkspaceGraph
    {
        public List<WorkspaceNode> Nodes { get; set; } = new List<WorkspaceNode>();
        public List<WorkspaceEdge> Edges { get; set; } = new List<WorkspaceEdge>();
        public List<ModuleTemplate> Modules { get; set; } = new List<ModuleTemplate>();
        public void ValidateStructure()
        {
            if (Nodes == null || Edges == null || Modules == null || Nodes.Count > 500 || Edges.Count > 2000 || Modules.Count > 100) throw new FormatException("Workspace limits: 500 blocks, 2,000 connections, 100 saved modules.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in Nodes)
            {
                if (n == null || n.Id == null || n.Id.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(n.Id, "^[A-Za-z0-9_]+$") || !ids.Add(n.Id)) throw new FormatException("Workspace block IDs must be unique identifiers.");
                if (!new[] { "Input", "Module", "Previous", "Output" }.Contains(n.Kind)) throw new FormatException("Unknown workspace block kind.");
                if (n.Title == null || n.Group == null || n.Literal == null || n.ValueSymbol == null || n.Code == null || n.Title.Length > 100 || n.Group.Length > 100 || n.Literal.Length > 5000 || n.ValueSymbol.Length > 64 || n.Code.Length > 32000 || float.IsNaN(n.X) || float.IsNaN(n.Y) || Math.Abs(n.X) > 1000000 || Math.Abs(n.Y) > 1000000) throw new FormatException("Invalid block text or position.");
            }
            foreach (var e in Edges) if (e == null || e.From == null || e.To == null || e.FromPort == null || e.ToPort == null || !ids.Contains(e.From) || !ids.Contains(e.To) || e.FromPort.Length > 64 || e.ToPort.Length > 64) throw new FormatException("A connection references a missing block or invalid port.");
            foreach (var m in Modules) if (m == null || m.Name == null || m.Code == null || m.Name.Length > 100 || m.Code.Length > 32000) throw new FormatException("Invalid saved module.");
        }
    }
    public sealed class WorkspaceEvaluation
    {
        public Dictionary<string, Dictionary<string, ScientificNumber>> Outputs = new Dictionary<string, Dictionary<string, ScientificNumber>>(StringComparer.Ordinal);
        public Dictionary<string, List<string>> Traces = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public Dictionary<string, ScientificNumber> NextMemory = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
    }
    public sealed class WorkspaceRuntime
    {
        readonly WorkspaceGraph graph;
        readonly Dictionary<string, WorkspaceNode> nodes;
        readonly Dictionary<string, IfcloudProgram> programs = new Dictionary<string, IfcloudProgram>();
        readonly Dictionary<string, WorkspaceEdge> incoming = new Dictionary<string, WorkspaceEdge>();
        Dictionary<string, ScientificNumber> memory = new Dictionary<string, ScientificNumber>();
        public WorkspaceRuntime(WorkspaceGraph graph)
        {
            graph.ValidateStructure(); this.graph = graph; nodes = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            foreach (var n in graph.Nodes)
            {
                try { if (n.Kind == "Module") programs[n.Id] = IfcloudProgram.Parse(n.Code); if (n.Kind == "Previous") memory[n.Id] = ScientificNumber.Parse(n.Literal); }
                catch (Exception ex) { throw new FormatException(n.Title + ": " + ex.Message, ex); }
            }
            foreach (var e in graph.Edges)
            {
                if (!InputPorts(nodes[e.To]).Contains(e.ToPort) || !OutputPorts(nodes[e.From]).Contains(e.FromPort)) throw new FormatException("Connection uses a missing port. Edit or disconnect the affected block.");
                string key = e.To + ":" + e.ToPort;
                if (incoming.ContainsKey(key)) throw new FormatException("Only one connection can feed each input port."); incoming.Add(key, e);
            }
        }
        public static string[] InputPorts(WorkspaceNode n) { return n.Kind == "Input" ? new string[0] : n.Kind == "Previous" ? new[] { "next" } : n.Kind == "Output" ? new[] { "value" } : IfcloudProgram.Parse(n.Code).Inputs.ToArray(); }
        public static string[] OutputPorts(WorkspaceNode n) { return n.Kind == "Module" ? IfcloudProgram.Parse(n.Code).Outputs.ToArray() : new[] { "value" }; }
        public WorkspaceEvaluation Evaluate(IDictionary<string, ScientificNumber> values)
        {
            var result = new WorkspaceEvaluation(); var visiting = new HashSet<string>();
            foreach (var n in graph.Nodes) EvalNode(n.Id, values, result, visiting);
            foreach (var n in graph.Nodes.Where(n => n.Kind == "Previous"))
            {
                var next = Input(n, "next", values, result, visiting); result.NextMemory.Add(n.Id, next);
                result.Traces[n.Id].Add("Next tick remembers " + next + "; current output stays " + memory[n.Id]);
            }
            return result;
        }
        ScientificNumber Input(WorkspaceNode n, string port, IDictionary<string, ScientificNumber> values, WorkspaceEvaluation result, HashSet<string> visiting)
        {
            WorkspaceEdge edge; if (!incoming.TryGetValue(n.Id + ":" + port, out edge)) throw new FormatException(n.Title + ": connect input '" + port + "'.");
            return EvalNode(edge.From, values, result, visiting)[edge.FromPort];
        }
        Dictionary<string, ScientificNumber> EvalNode(string id, IDictionary<string, ScientificNumber> values, WorkspaceEvaluation result, HashSet<string> visiting)
        {
            Dictionary<string, ScientificNumber> cached; if (result.Outputs.TryGetValue(id, out cached)) return cached;
            if (!visiting.Add(id)) throw new FormatException("Same-tick cycle detected at '" + nodes[id].Title + "'. Insert a Previous-step block in the loop.");
            var n = nodes[id]; var trace = new List<string>(); var output = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
            try
            {
                if (n.Kind == "Input")
                {
                    ScientificNumber value;
                    if (n.ValueSymbol.Length > 0) { if (!values.TryGetValue(n.ValueSymbol, out value)) throw new FormatException("Define Values symbol '" + n.ValueSymbol + "'."); trace.Add("Values snapshot " + n.ValueSymbol + " = " + value); }
                    else { value = ScientificNumber.Parse(n.Literal); trace.Add("Authored constant " + n.Literal + " → " + value); }
                    output.Add("value", value);
                }
                else if (n.Kind == "Previous") { output.Add("value", memory[id]); trace.Add("Previous-step memory = " + memory[id] + " (initial " + n.Literal + ")"); }
                else
                {
                    var inputs = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
                    var ports = n.Kind == "Module" ? programs[id].Inputs : (IReadOnlyList<string>)new[] { "value" };
                    foreach (string port in ports)
                    {
                        var value = Input(n, port, values, result, visiting); inputs.Add(port, value); var edge = incoming[id + ":" + port];
                        trace.Add("Wire " + nodes[edge.From].Title + "." + edge.FromPort + " → " + port + " = " + value);
                    }
                    if (n.Kind == "Module") output = programs[id].Execute(inputs, trace); else { output.Add("value", inputs["value"]); trace.Add("Observed output = " + inputs["value"]); }
                }
            }
            catch (Exception ex) { throw new FormatException(n.Title + ": " + ex.Message, ex); }
            visiting.Remove(id); result.Outputs.Add(id, output); result.Traces.Add(id, trace); return output;
        }
        public void Commit(WorkspaceEvaluation evaluation) { memory = new Dictionary<string, ScientificNumber>(evaluation.NextMemory); }
    }
}
