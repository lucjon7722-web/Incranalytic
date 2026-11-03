using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Incranalytic.Core;

public sealed record Finding(string System, string Name, string Value, string Source, string Location, string Explanation, string Confidence);
public sealed record AnalysisResult(List<Finding> Findings, int ReadFiles, int SkippedFiles, List<string> Warnings);

public static class Analyzer
{
    public static AnalysisResult Analyze(ProjectInput input)
    {
        var result = new AnalysisResult([], 0, 0, []);
        int read = 0, skipped = 0;
        foreach (var file in input.Files)
        {
            if (result.Findings.Count >= 10000) { result.Warnings.Add("Finding limit reached (10,000)."); break; }
            var ext = Path.GetExtension(file.Path).ToLowerInvariant();
            if (!new[] { ".json", ".csv", ".txt", ".cs", ".gd", ".js", ".ts", ".lua", ".java", ".kt", ".xml" }.Contains(ext)) { skipped++; continue; }
            var text = input.ReadText(file);
            if (text == null) { skipped++; continue; }
            read++;
            try
            {
                if (ext == ".json")
                {
                    using var doc = JsonDocument.Parse(text);
                    Walk(doc.RootElement, "$", file.Path, result.Findings);
                }
                else if (ext == ".csv")
                {
                    var rows = text.Split('\n').Select(ParseCsvRow).ToArray();
                    for (int row = 1; row < rows.Length && result.Findings.Count < 10000; row++)
                        for (int col = 0; col < rows[row].Length && col < rows[0].Length; col++)
                            if (double.TryParse(rows[row][col], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                                Add(result.Findings, $"{rows[row].FirstOrDefault()}.{rows[0][col]}", rows[row][col], file.Path, $"row {row + 1}, column {col + 1}", false);
                }
                else
                {
                    var lines = text.Split('\n');
                    for (int i = 0; i < lines.Length && result.Findings.Count < 10000; i++)
                    {
                        foreach (Match match in Regex.Matches(lines[i], @"(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|:)\s*(?<value>-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)", RegexOptions.None, TimeSpan.FromSeconds(1)))
                            Add(result.Findings, match.Groups["key"].Value, match.Groups["value"].Value, file.Path, $"line {i + 1}", false);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or RegexMatchTimeoutException) { result.Warnings.Add($"{file.Path}: {ex.Message}"); }
        }
        return result with { ReadFiles = read, SkippedFiles = skipped };
    }

    private static string[] ParseCsvRow(string line)
    {
        var values = new List<string>(); var value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { values.Add(value.ToString().Trim()); value.Clear(); }
            else value.Append(c);
        }
        values.Add(value.ToString().Trim()); return values.ToArray();
    }

    private static void Walk(JsonElement node, string path, string file, List<Finding> findings)
    {
        if (findings.Count >= 10000) return;
        if (node.ValueKind == JsonValueKind.Object)
            foreach (var property in node.EnumerateObject()) Walk(property.Value, path + "." + property.Name, file, findings);
        else if (node.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in node.EnumerateArray()) Walk(item, $"{path}[{i++}]", file, findings);
        }
        else if (node.ValueKind == JsonValueKind.Number) Add(findings, path, node.GetRawText(), file, path, false);
        else if (node.ValueKind == JsonValueKind.String && (path.Contains("formula", StringComparison.OrdinalIgnoreCase) || path.Contains("expression", StringComparison.OrdinalIgnoreCase)))
            Add(findings, path, node.GetString() ?? "", file, path, true);
    }

    private static void Add(List<Finding> list, string name, string value, string file, string location, bool formula)
    {
        string key = name.ToLowerInvariant();
        string system = key.Contains("prestige") || key.Contains("reset") ? "Prestige" : key.Contains("offline") ? "Offline earnings" : key.Contains("upgrade") || key.Contains("multiplier") ? "Upgrades" : key.Contains("generator") || key.Contains("production") ? "Production" : key.Contains("currenc") ? "Currency" : "Other values";
        string explanation = formula ? "An explicit formula stored in the file. Open it in Formula Lab and supply the variables to explore its result." :
            key.Contains("growth") ? "A stored growth factor. When used in an exponential cost formula, a factor of 1.15 means a 15% increase per level. Confirm its use in the game code." :
            key.Contains("multiplier") ? "A stored multiplier. A value of 2 may double a quantity; its actual effect depends on where the game applies it." :
            key.Contains("cost") ? "A stored cost value. Follow the surrounding data or code to identify its currency and any scaling." :
            key.Contains("second") ? "A stored time or rate value whose name refers to seconds. Check whether it represents a duration or production rate." :
            "A numeric literal recovered from this file. Its name and surrounding context help identify its role; the value alone does not establish runtime behavior.";
        list.Add(new Finding(system, name.Replace("$.", ""), value, file, location, explanation, formula ? "Explicit formula" : "Literal; role inferred"));
    }
}

public static class Reports
{
    public static void Export(string path, ProjectInput project, AnalysisResult result)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".json") File.WriteAllText(path, JsonSerializer.Serialize(new { project = project.Name, engine = project.Engine, sha256 = project.Sha256, analysis = result }, new JsonSerializerOptions { WriteIndented = true }));
        else if (ext == ".csv")
        {
            string Q(string s) { if (s.Length > 0 && "=+@\t\r".Contains(s[0])) s = "'" + s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
            File.WriteAllLines(path, new[] { "System,Name,Value,Source,Location,Explanation,Confidence" }.Concat(result.Findings.Select(f => string.Join(",", new[] { f.System, f.Name, f.Value, f.Source, f.Location, f.Explanation, f.Confidence }.Select(Q)))));
        }
        else
        {
            string E(string s) => WebUtility.HtmlEncode(s);
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Incranalytic report</title><style>body{font:15px Segoe UI;margin:40px;color:#17212b}table{border-collapse:collapse;width:100%}td,th{border:1px solid #bbc3cc;padding:9px;text-align:left;vertical-align:top}th{background:#e7ebef}code{white-space:pre-wrap}</style>");
            html.Append($"<h1>Incranalytic analysis</h1><h2>{E(project.Name)}</h2><p>{E(project.Engine)} · {result.Findings.Count} findings · {result.ReadFiles} text files read</p><p>Recovered literals and inferred roles are evidence for investigation, not proof of runtime behavior.</p><table><tr><th>System / value</th><th>Number or formula</th><th>Evidence</th><th>Explanation</th></tr>");
            foreach (var f in result.Findings) html.Append($"<tr><td>{E(f.System)}<br>{E(f.Name)}</td><td><code>{E(f.Value)}</code></td><td>{E(f.Source)}<br>{E(f.Location)}<br>{E(f.Confidence)}</td><td>{E(f.Explanation)}</td></tr>");
            File.WriteAllText(path, html.Append("</table>").ToString());
        }
    }
}

public sealed class Formula
{
    private readonly string text;
    private readonly IDictionary<string, double> variables;
    private int position, depth;
    private Formula(string text, IDictionary<string, double> variables) { this.text = text; this.variables = variables; }
    public static double Evaluate(string expression, IDictionary<string, double> variables)
    {
        if (expression.Length > 1000) throw new FormatException("Formula is too long.");
        var parser = new Formula(expression, variables);
        double value = parser.Expression(0);
        parser.Space();
        if (parser.position != expression.Length) throw new FormatException($"Unexpected character at position {parser.position + 1}.");
        if (!double.IsFinite(value)) throw new ArithmeticException("Result is outside the finite number range.");
        return value;
    }
    private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    private double Expression(int minimum)
    {
        if (++depth > 64) throw new FormatException("Formula nesting is too deep.");
        double left = Primary();
        while (true)
        {
            Space();
            char op = position < text.Length ? text[position] : '\0';
            int precedence = op is '+' or '-' ? 1 : op is '*' or '/' ? 2 : op == '^' ? 3 : -1;
            if (precedence < minimum) break;
            position++;
            double right = Expression(op == '^' ? precedence : precedence + 1);
            left = op switch { '+' => left + right, '-' => left - right, '*' => left * right, '/' => right == 0 ? throw new ArithmeticException("Division by zero.") : left / right, '^' => Math.Pow(left, right), _ => left };
        }
        depth--;
        return left;
    }
    private double Primary()
    {
        Space();
        if (position >= text.Length) throw new FormatException("Expected a number, variable, or parenthesis.");
        char c = text[position];
        if (c is '+' or '-') { position++; return (c == '-' ? -1 : 1) * Expression(3); }
        if (c == '(') { position++; double v = Expression(0); Require(')'); return v; }
        if (char.IsDigit(c) || c == '.')
        {
            var m = Regex.Match(text[position..], @"^(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?");
            if (!m.Success) throw new FormatException("Invalid number.");
            position += m.Length;
            return double.Parse(m.Value, CultureInfo.InvariantCulture);
        }
        if (char.IsLetter(c) || c == '_')
        {
            int start = position++;
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_')) position++;
            string name = text[start..position]; Space();
            if (position < text.Length && text[position] == '(')
            {
                position++; double a = Expression(0); Space(); double? b = null;
                if (position < text.Length && text[position] == ',') { position++; b = Expression(0); }
                Require(')');
                return name.ToLowerInvariant() switch { "floor" when b == null => Math.Floor(a), "ceil" when b == null => Math.Ceiling(a), "sqrt" when b == null => Math.Sqrt(a), "log" when b == null => Math.Log(a), "min" when b != null => Math.Min(a, b.Value), "max" when b != null => Math.Max(a, b.Value), "pow" when b != null => Math.Pow(a, b.Value), _ => throw new FormatException("Unknown function or wrong argument count: " + name) };
            }
            return variables.TryGetValue(name, out double value) ? value : throw new FormatException("Supply a value for variable: " + name);
        }
        throw new FormatException($"Unexpected character: {c}");
    }
    private void Require(char c) { Space(); if (position >= text.Length || text[position++] != c) throw new FormatException($"Expected '{c}'."); }
}
