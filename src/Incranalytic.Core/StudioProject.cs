using System.Text.Json;
using System.Text.RegularExpressions;

namespace Incranalytic.Core;

public sealed class StudioValue
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Initial { get; set; } = "0";
    public string Formula { get; set; } = "0";
}
public sealed class StudioProject
{
    public int FormatVersion { get; set; } = 1;
    public int Precision { get; set; } = 34;
    public decimal Interval { get; set; } = 1;
    public bool PerSecond { get; set; } = true;
    public List<StudioValue> Values { get; set; } = [];
    public void Validate()
    {
        if (FormatVersion != 1) throw new FormatException("Unsupported project version.");
        if (Precision < 1 || Precision > 1000) throw new FormatException("Precision must be 1–1000 significant digits.");
        if (Interval < 0.001m || Interval > 3600) throw new FormatException("Interval must be 0.001–3600 seconds.");
        if (Values.Count > 100) throw new FormatException("The base studio supports up to 100 values per project.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in Values)
        {
            if (v.Symbol.Length > 64 || !Regex.IsMatch(v.Symbol, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new FormatException("Symbols need letters, digits or underscores, and cannot start with a digit.");
            if (!names.Add(v.Symbol)) throw new FormatException("Duplicate symbol: " + v.Symbol);
            if (v.Name.Length > 200 || v.Initial.Length > 5000 || v.Formula.Length > 2000) throw new FormatException("Value text exceeds the project limits.");
        }
        var session = new StudioSession(this);
        foreach (var v in Values) ScientificExpression.Evaluate(v.Formula, session.State);
    }
    public void Save(string path) { Validate(); File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
    public static StudioProject Load(string path)
    {
        if (new FileInfo(path).Length > 2_000_000) throw new FormatException("Project file exceeds 2 MB.");
        var p = JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(path)) ?? throw new FormatException("Invalid project.");
        if (p.Values == null || p.Values.Any(v => v == null || v.Symbol == null || v.Name == null || v.Initial == null || v.Formula == null)) throw new FormatException("Incomplete Values data.");
        p.Validate(); return p;
    }
}
public sealed class StudioSession
{
    public StudioProject Project { get; }
    public Dictionary<string, ScientificNumber> State { get; private set; } = new(StringComparer.Ordinal);
    public ScientificNumber Elapsed { get; private set; } = ScientificNumber.Zero;
    public long Ticks { get; private set; }
    public StudioSession(StudioProject project)
    {
        Project = project; ScientificNumber.Precision = project.Precision;
        foreach (var v in project.Values) State.Add(v.Symbol, ScientificNumber.Parse(v.Initial));
    }
    public void Step()
    {
        ScientificNumber.Precision = Project.Precision;
        var dt = ScientificNumber.Parse(Project.Interval.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var next = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
        foreach (var v in Project.Values)
        {
            var gain = ScientificExpression.Evaluate(v.Formula, State);
            next[v.Symbol] = State[v.Symbol] + gain * (Project.PerSecond ? dt : ScientificNumber.Parse("1"));
        }
        State = next; Elapsed = Elapsed + dt; Ticks++;
    }
}
