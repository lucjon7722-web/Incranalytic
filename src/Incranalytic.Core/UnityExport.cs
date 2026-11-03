using System.Globalization;
using System.Text.Json;

namespace Incranalytic.Core;

public static class UnityExport
{
    public static void Write(string folder, StudioProject project, string runtimeSource)
    {
        project.Validate();
        if (Directory.Exists(folder)) throw new IOException("Export folder already exists. Choose a new destination.");
        Directory.CreateDirectory(folder);
        string Quote(string s) => JsonSerializer.Serialize(s);
        var symbols = string.Join(", ", project.Values.Select(v => Quote(v.Symbol)));
        var initials = string.Join(", ", project.Values.Select(v => Quote(v.Initial)));
        var formulas = string.Join(", ", project.Values.Select(v => Quote(v.Formula)));
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
    public void Awake() { ResetSystem(); }
    public void ResetSystem()
    {
        ScientificNumber.Precision = Digits;
        state.Clear(); accumulator = 0;
        for (int i = 0; i < symbols.Length; i++) state.Add(symbols[i], ScientificNumber.Parse(initials[i]));
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
    }
}
