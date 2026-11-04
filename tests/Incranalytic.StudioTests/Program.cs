using System.Diagnostics;
using Incranalytic.Core;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); checks++; }
void Reject(Action action, string name) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, name); }
ScientificNumber.Precision = 34;
Check((ScientificNumber.Parse("1e100000000000000000000") * ScientificNumber.Parse("1e10")).ToString() == "1e100000000000000000010", "Exponent beyond floating-point limits");
Check((ScientificNumber.Parse("1.25") + ScientificNumber.Parse("2.75")).ToString() == "4e0", "Decimal addition");
Check((ScientificNumber.Parse("1e100") + ScientificNumber.Parse("-1e100")).ToString() == "0e0", "Cancellation");
Check((ScientificNumber.Parse("1") / ScientificNumber.Parse("8")).ToString() == "1.25e-1", "Division");
ScientificNumber.Precision = 5;
Check(ScientificNumber.Parse("1234567").ToString() == "1.2345e6", "User-selected significant digits truncate");
ScientificNumber.Precision = 1000;
Check(ScientificNumber.Parse(new string('9', 1000)).ToString().Length > 1000, "1000 significant digits");
ScientificNumber.Precision = 34;
var vars = new Dictionary<string, ScientificNumber> { ["A"] = ScientificNumber.Parse("2") };
Check(ScientificExpression.Evaluate("-A^2 + max(3,4)", vars).ToString() == "0e0", "Expression precedence and functions");
Check(ScientificExpression.Evaluate("2^3^2", vars).ToString() == "5.12e2", "Right associative powers");
Reject(() => ScientificExpression.Evaluate("1/0", vars), "Reject division by zero");
Reject(() => ScientificExpression.Evaluate("B + 1", vars), "Missing symbol explanation");
Reject(() => ScientificExpression.Evaluate("4 ^ 0.5", vars), "Unsupported fractional powers are explicit");
var p = new StudioProject { Values = [new() { Symbol = "A", Name = "Stars", Initial = "0", Formula = "B" }, new() { Symbol = "B", Initial = "1", Formula = "1" }] };
p.Validate(); var sim = new StudioSession(p); sim.Step(); sim.Step();
Check(sim.State["A"].ToString() == "3e0" && sim.State["B"].ToString() == "3e0", "Simultaneous updates from previous state");
for (int i = 0; i < 8; i++) sim.Step();
Check(sim.State["A"].ToString() == "5.5e1", "Ten-step trajectory");
var reset = new StudioSession(p); Check(reset.State["A"].ToString() == "0e0" && reset.Ticks == 0, "Reset initial conditions");
var fractional = new StudioProject { Interval = .25m, Values = [new() { Symbol = "Z", Initial = "0", Formula = "4" }] };
var second = new StudioSession(fractional); second.Step(); Check(second.State["Z"].ToString() == "1e0", "Per-second mode scales by interval");
fractional.PerSecond = false; var tick = new StudioSession(fractional); tick.Step(); Check(tick.State["Z"].ToString() == "4e0", "Per-tick mode adds once");
var atomic = new StudioProject { Values = [new() { Symbol = "a", Initial = "1", Formula = "1" }, new() { Symbol = "b", Initial = "0", Formula = "1/b" }] };
var atomicSession = new StudioSession(atomic); Reject(() => atomicSession.Step(), "Invalid simulation step fails"); Check(atomicSession.State["a"].ToString() == "1e0" && atomicSession.Ticks == 0, "Failed step leaves all values untouched");
Reject(() => new StudioProject { Values = [new() { Symbol = "A" }, new() { Symbol = "A" }] }.Validate(), "Reject duplicate symbols");
var temp = Path.Combine(Path.GetTempPath(), "Incranalytic-StudioTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
string model = Path.Combine(temp, "test.incr"); p.Save(model); var loaded = StudioProject.Load(model); Check(loaded.Values[0].Name == "Stars" && loaded.Values[0].Formula == "B", "Save/import preserves user meanings and formulas");
string runtime = File.ReadAllText(Path.GetFullPath("src/Incranalytic.Core/ScientificNumber.cs"));
string exported = Path.Combine(temp, "UnityExport"); UnityExport.Write(exported, p, runtime);
Check(File.Exists(Path.Combine(exported, "IncranalyticSystem.cs")) && File.Exists(Path.Combine(exported, "system.incr")), "Export C# and editable project");
File.WriteAllText(Path.Combine(exported, "ExportCheck.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>");
File.WriteAllText(Path.Combine(exported, "UnityStub.cs"), "namespace UnityEngine { public class MonoBehaviour {} public static class Time { public static float fixedDeltaTime = 0.02f; } public static class Debug { public static void LogException(System.Exception e) { throw e; } } }");
File.WriteAllText(Path.Combine(exported, "Program.cs"), "using System; class Program { static void Main() { var game = new IncranalyticSystem(); game.Awake(); for(int i=0;i<10;i++)game.Step(); if(game.GetValue(\"A\")!=\"5.5e1\")throw new Exception(\"Mismatch\"); game.ResetSystem(); if(game.GetValue(\"A\")!=\"0e0\")throw new Exception(\"Reset failed\"); for(int i=0;i<501;i++) game.FixedUpdate(); if(game.GetValue(\"A\")!=\"5.5e1\")throw new Exception(\"FixedUpdate mismatch\"); Console.WriteLine(\"Generated C# parity passed\"); } }");
using var proc = Process.Start(new ProcessStartInfo("dotnet", "run --project \"" + Path.Combine(exported, "ExportCheck.csproj") + "\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
string output = proc.StandardOutput.ReadToEnd(); string error = proc.StandardError.ReadToEnd(); proc.WaitForExit(); Console.WriteLine(output); if (proc.ExitCode != 0) throw new Exception(error + output);
Check(proc.ExitCode == 0, "Generated C# compiles as C#9 and matches studio Step/Reset/FixedUpdate with Unity API stub");
Check(File.ReadAllText(Path.Combine(exported, "EXPORT-LICENSE.txt")).Contains("commercial games"), "Exports include personal and commercial runtime redistribution permission");
var oversized = new StudioProject { Workspace = new WorkspaceGraph { Nodes = Enumerable.Range(0, 110).Select(_ => new WorkspaceNode { Kind = "Module", Code = new string('\u0001', 32000) }).ToList() } };
string originalSaved = File.ReadAllText(model);
Reject(() => oversized.Save(model), "Oversized draft cannot replace a readable saved project");
Check(File.ReadAllText(model) == originalSaved && !Directory.GetFiles(temp, "*.tmp").Any(), "Rejected oversized save preserves the original and removes temporary data");
string profileRoot = Path.Combine(temp, "profile");
var profile = new StudioProfile(profileRoot);
Check(profile.Preferences.EnableRecovery && !profile.Preferences.ShowSimulationSettings && !profile.Preferences.HasSeenWelcome, "First launch defaults enable recovery, hide advanced settings, and offer the guide");
profile.Preferences.ShowGrid = false; profile.Preferences.HasSeenWelcome = true; profile.Save();
Check(!new StudioProfile(profileRoot).Preferences.ShowGrid && new StudioProfile(profileRoot).Preferences.HasSeenWelcome, "View and onboarding preferences persist");
File.WriteAllText(Path.Combine(profileRoot, "preferences.json"), "{broken");
Check(new StudioProfile(profileRoot).Warning != null && new StudioProfile(profileRoot).Preferences.EnableRecovery, "Corrupt preferences fall back to safe defaults");
using (var active = new RecoveryStore(profileRoot))
{
    active.Write(p, model);
    using var observer = new RecoveryStore(profileRoot);
    Check(observer.FindAvailable().Count == 0, "Recovery does not offer another active window's work");
}
using (var restored = new RecoveryStore(profileRoot))
{
    var candidates = restored.FindAvailable();
    Check(candidates.Count == 1 && candidates[0].Snapshot.Project.Values[0].Name == "Stars", "Closed session recovery retains authored values and original path");
    Check(candidates[0].Snapshot.OriginalPath == model && File.ReadAllText(model).Contains("Stars"), "Recovery leaves the original saved project intact");
    restored.Claim(candidates[0]);
    using var other = new RecoveryStore(profileRoot);
    Check(other.FindAvailable().Count == 0, "Claimed recovery is hidden from other windows");
    restored.Write(p, model);
    Reject(() => restored.Write(oversized, model), "Oversized recovery cannot replace the previous checkpoint");
    var invalid = new StudioProject { Values = [new() { Symbol = "bad symbol" }] };
    Reject(() => restored.Write(invalid, model), "Invalid recovery write is rejected before replacing an earlier copy");
    Check(Directory.GetFiles(Path.Combine(profileRoot, "recovery"), "snapshot-*.json").Length == 2, "Failed write preserves both earlier recovery copies");
    Reject(() => other.Claim(new(Path.Combine(temp, "snapshot-" + new string('a', 32) + ".json"), new())), "Recovery cannot claim a file outside its profile");
    restored.ClearCurrent();
    Check(Directory.GetFiles(Path.Combine(profileRoot, "recovery"), "snapshot-*.json").Length == 0, "Saving or discarding removes only the current and claimed recovery copies");
}
File.WriteAllText(Path.Combine(profileRoot, "recovery", "snapshot-" + new string('b', 32) + ".json"), "{broken");
using (var scan = new RecoveryStore(profileRoot)) Check(scan.FindAvailable().Count == 0, "Malformed recovery is ignored without preventing startup");
Console.WriteLine($"{checks} studio checks passed. Export validation folder: {exported}");
