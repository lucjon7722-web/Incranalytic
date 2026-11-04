using System.Globalization;
using System.Text.Json;

namespace Incranalytic.Core;

public static class UnityExport
{
    public static void Write(string folder, StudioProject project, string runtimeSource, string ifcloudSource = "", string workspaceSource = "")
    {
        project.Validate();
        bool hasWorkspace = project.Workspace.Nodes.Count > 0;
        if (hasWorkspace && (ifcloudSource.Length == 0 || workspaceSource.Length == 0)) throw new InvalidOperationException("Workspace export requires the IFCLOUD and workspace runtimes.");
        if (Directory.Exists(folder)) throw new IOException("Export folder already exists. Choose a new destination.");
        Directory.CreateDirectory(folder);
        using (var permission = new StreamReader(typeof(UnityExport).Assembly.GetManifestResourceStream("Incranalytic.ExportLicense")!))
            File.WriteAllText(Path.Combine(folder, "EXPORT-LICENSE.txt"), permission.ReadToEnd());
        string Quote(string s) => JsonSerializer.Serialize(s);
        var symbols = string.Join(", ", project.Values.Select(v => Quote(v.Symbol)));
        var initials = string.Join(", ", project.Values.Select(v => Quote(v.Initial)));
        var formulas = string.Join(", ", project.Values.Select(v => Quote(v.Formula)));
        string workspaceFields = hasWorkspace ? "WorkspaceRuntime workspace; WorkspaceEvaluation lastWorkspace;\n    public string GetWorkspaceValue(string blockId, string port) { return lastWorkspace.Outputs[blockId][port].ToString(); }" : "";
        string workspaceInit = hasWorkspace ? "workspace = new WorkspaceRuntime(CreateWorkspace()); lastWorkspace = workspace.Evaluate(state);" : "";
        string workspaceStep = hasWorkspace ? "var evaluation = workspace.Evaluate(state); workspace.Commit(evaluation); lastWorkspace = evaluation;" : "";
        var factory = new System.Text.StringBuilder();
        if (hasWorkspace)
        {
            File.WriteAllText(Path.Combine(folder, "Ifcloud.cs"), ifcloudSource.Replace("namespace Incranalytic.Core", "namespace Incranalytic.Generated"));
            File.WriteAllText(Path.Combine(folder, "WorkspaceRuntime.cs"), workspaceSource.Replace("namespace Incranalytic.Core", "namespace Incranalytic.Generated"));
            factory.AppendLine("static WorkspaceGraph CreateWorkspace() { var graph = new WorkspaceGraph();");
            foreach (var n in project.Workspace.Nodes) factory.AppendLine($"graph.Nodes.Add(new WorkspaceNode {{ Id={Quote(n.Id)}, Kind={Quote(n.Kind)}, Title={Quote(n.Title)}, Group={Quote(n.Group)}, Literal={Quote(n.Literal)}, ValueSymbol={Quote(n.ValueSymbol)}, Code={Quote(n.Code)} }});");
            foreach (var e in project.Workspace.Edges) factory.AppendLine($"graph.Edges.Add(new WorkspaceEdge {{ From={Quote(e.From)}, FromPort={Quote(e.FromPort)}, To={Quote(e.To)}, ToPort={Quote(e.ToPort)} }});");
            factory.AppendLine("return graph; }");
        }
        File.WriteAllText(Path.Combine(folder, "ScientificNumber.cs"), runtimeSource.Replace("namespace Incranalytic.Core", "namespace Incranalytic.Generated"));
        File.WriteAllText(Path.Combine(folder, "IncranalyticSystem.cs"), $$"""
// Human-authored model exported by Incranalytic. No AI generation is used by this export.
using System;
using System.Collections.Generic;
using Incranalytic.Generated;
using UnityEngine;

public sealed class IncranalyticSystem : MonoBehaviour
{
    public bool autoRun = true;
    const int Digits = {{project.Precision}};
    const double Interval = {{project.Interval.ToString(CultureInfo.InvariantCulture)}};
    readonly string[] symbols = new string[] { {{symbols}} };
    readonly string[] initials = new string[] { {{initials}} };
    readonly string[] formulas = new string[] { {{formulas}} };
    Dictionary<string, ScientificNumber> state = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
    double accumulator;
    {{workspaceFields}}
    {{factory}}
    public void Awake() { ResetSystem(); }
    public void ResetSystem()
    {
        ScientificNumber.Precision = Digits;
        state.Clear(); accumulator = 0;
        for (int i = 0; i < symbols.Length; i++) state.Add(symbols[i], ScientificNumber.Parse(initials[i]));
        {{workspaceInit}}
    }
    public string GetValue(string symbol) { return state[symbol].ToString(); }
    public void SetValue(string symbol, string number)
    {
        if (!state.ContainsKey(symbol)) throw new ArgumentException("Unknown authored symbol: " + symbol);
        ScientificNumber.Precision = Digits;
        state[symbol] = ScientificNumber.Parse(number);
    }
    public void Step()
    {
        ScientificNumber.Precision = Digits;
        var next = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
        var factor = ScientificNumber.Parse("{{(project.PerSecond ? project.Interval.ToString(CultureInfo.InvariantCulture) : "1")}}");
        for (int i = 0; i < symbols.Length; i++) next[symbols[i]] = state[symbols[i]] + ScientificExpression.Evaluate(formulas[i], state) * factor;
        {{workspaceStep}}
        state = next;
    }
    public void FixedUpdate()
    {
        if (!autoRun) return;
        accumulator += Time.fixedDeltaTime;
        int steps = 0;
        try { while (accumulator >= Interval && steps++ < 200) { Step(); accumulator -= Interval; } }
        catch (Exception exception) { autoRun = false; Debug.LogException(exception); }
    }
}
""");
        project.Save(Path.Combine(folder, "system.incr"));
        File.WriteAllText(Path.Combine(folder, "README.txt"), "Incranalytic Unity export\nCopy ScientificNumber.cs and IncranalyticSystem.cs into Assets. Attach IncranalyticSystem to a GameObject. Use GetValue(symbol) for scientific strings and SetValue(symbol, number) to change an existing symbol from gameplay. autoRun uses FixedUpdate; set it false to call Step manually. ResetSystem restores authored initial conditions. Each tick reads previous state and updates simultaneously.\n\nAll operations use the selected significant digits and truncate toward zero; powers have integer exponents only. Do not convert results to float/double for game calculations.\n\nThe C# export is compiled and compared against the studio with a Unity API stub during development. It has not been validated inside your Unity project. Test integration before release. Exporting a second system under the same class name requires renaming its component. No Unity editor or external service is required to export.\n");
        if (hasWorkspace) File.AppendAllText(Path.Combine(folder, "README.txt"), "\nWORKSPACE\nAlso copy Ifcloud.cs and WorkspaceRuntime.cs into Assets. GetWorkspaceValue(blockId, port) exposes the last successful tick's outputs. After ResetSystem it returns the first-tick preview without committing memory. Modules execute once per tick; the interval is not an implicit module input.\nOutput blocks:\n" + string.Join("\n", project.Workspace.Nodes.Where(n => n.Kind == "Output").Select(n => n.Title + " => GetWorkspaceValue(\"" + n.Id + "\", \"value\")")));
    }
}
