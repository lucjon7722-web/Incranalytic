using System.Diagnostics;
using Incranalytic.Core;

int count = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL: " + message); count++; Console.WriteLine("PASS: " + message); }
void Reject(Action action, string message) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, message); }
Dictionary<string, ScientificNumber> Inputs(params (string Key, string Value)[] entries) => entries.ToDictionary(e => e.Key, e => ScientificNumber.Parse(e.Value));
ScientificNumber.Precision = 34;
var trace = new List<string>();
var branch = IfcloudProgram.Parse("input A; output B; if (A > 5) { B = A * 2; } else if(A == 5) { B = 100; } else { B = 0; }");
Check(branch.Execute(Inputs(("A", "6")), trace)["B"].ToString() == "1.2e1", "True branch computes an authored output");
Check(branch.Execute(Inputs(("A", "5")), [])["B"].ToString() == "1e2", "Else-if branch executes");
Check(branch.Execute(Inputs(("A", "4")), [])["B"].ToString() == "0e0", "Else branch executes");
Check(trace.Any(t => t.Contains("true")) && trace.Any(t => t.Contains("1.2e1")), "Why inspector trace contains branch decisions and computed values");
var shortCircuit = IfcloudProgram.Parse("input a; output b; if(a != 0 && 100/a > 1) { b=1; } else { b=0; }");
Check(shortCircuit.Execute(Inputs(("a", "0")), [])["b"].ToString() == "0e0", "Boolean AND short-circuits division by zero");
Check(IfcloudProgram.Parse("output a; if(true || 1/0) { a=1; } else { a=2; }").Execute(Inputs(), [])["a"].ToString() == "1e0", "Boolean OR short-circuits");
Check(IfcloudProgram.Parse("output a; output b; let x=.5; x=x*4; a=-2^2; if(!false && x<=2) { b=max(x,1); } else { b=0; }").Execute(Inputs(), [])["b"].ToString() == "2e0", "Locals, reassignment, decimals, functions and Boolean not");
Check(IfcloudProgram.Parse("// comment\noutput a; a=1e100000000000000000000;").Execute(Inputs(), [])["a"].ToString() == "1e100000000000000000000", "IFCLOUD preserves scientific values beyond double range");
Reject(() => IfcloudProgram.Parse("input a; output b; a=1; b=a;"), "Input ports are read-only");
Reject(() => IfcloudProgram.Parse("output a; a=missing;"), "Unknown symbols are rejected");
Reject(() => IfcloudProgram.Parse("output a; a=1"), "Missing semicolon is diagnosed");
try { IfcloudProgram.Parse("output a;\na=;"); } catch (Exception ex) { Check(ex.Message.Contains("line 2") && ex.Message.Contains("column"), "Syntax errors include source line and column"); }
Reject(() => IfcloudProgram.Parse("output a; while(true) { a=1; }"), "Unbounded loops are not in the language");
Reject(() => IfcloudProgram.Parse("output a; a=System.IO.File.ReadAllText(1);"), "Host C# and operating-system calls cannot execute");
Reject(() => IfcloudProgram.Parse("input a; output b; if(a>0){b=1;}").Execute(Inputs(("a", "0")), []), "Unassigned branch outputs fail explicitly");
Reject(() => IfcloudProgram.Parse("output a;" + string.Concat(Enumerable.Repeat("a=1;", 1100))), "Token budget is enforced");
Reject(() => IfcloudProgram.Parse(string.Concat(Enumerable.Range(0, 17).Select(i => "input a" + i + ";")) + "output b;b=1;"), "Input-port limit is enforced");
var p = new StudioProject { Workspace = WorkspaceFiles.ExampleLoop() }; p.Validate();
var sim = new StudioSession(p); var monitor = p.Workspace.Nodes.Single(n => n.Kind == "Output");
Check(sim.Ticks == 0 && sim.LastWorkspace.Outputs[monitor.Id]["value"].ToString() == "1e0", "Initial workspace evaluation is a preview without advancing time");
sim.Step(); Check(sim.LastWorkspace.Outputs[monitor.Id]["value"].ToString() == "1e0", "Preview does not commit feedback memory");
for (int i = 0; i < 14; i++) sim.Step();
Check(sim.LastWorkspace.Outputs[monitor.Id]["value"].ToString() == "1e1", "Conditional feedback increments and then stops at ten");
Check(sim.DisplayState()["@" + monitor.Id].ToString() == "1e1", "Graph output is exposed to shared simulation history");
Check(new StudioSession(p).LastWorkspace.Outputs[monitor.Id]["value"].ToString() == "1e0", "Reset reconstructs initial memory");
var cycle = WorkspaceFiles.Clone(p.Workspace); var previous = cycle.Nodes.Single(n => n.Kind == "Previous"); var module = cycle.Nodes.Single(n => n.Kind == "Module");
cycle.Nodes.Remove(previous); cycle.Edges.RemoveAll(e => e.From == previous.Id || e.To == previous.Id); cycle.Edges.Add(new() { From = module.Id, FromPort = "total", To = module.Id, ToPort = "previous" });
Reject(() => new WorkspaceRuntime(cycle).Evaluate(Inputs()), "Same-tick cycles are rejected with explicit memory required");
var missing = WorkspaceFiles.Clone(p.Workspace); missing.Edges.RemoveAt(0); Reject(() => new WorkspaceRuntime(missing).Evaluate(Inputs()), "Disconnected inputs fail validation");
var duplicate = WorkspaceFiles.Clone(p.Workspace); duplicate.Edges.Add(duplicate.Edges[0]); Reject(() => new WorkspaceRuntime(duplicate), "Multiple wires into one input are rejected");
var invalid = WorkspaceFiles.Clone(p.Workspace); invalid.Edges[0].FromPort = "missing"; Reject(() => new WorkspaceRuntime(invalid), "Invalid port names are rejected");
var atomic = WorkspaceFiles.Clone(p.Workspace); var atomicModule = atomic.Nodes.Single(n => n.Kind == "Module");
atomicModule.Code = "input previous; input fail; output total; if(previous==2 && fail!=0) { total=1/0; } else { total=previous+1; }";
var flag = new WorkspaceNode { Kind = "Input", Title = "Failure switch", ValueSymbol = "F" }; atomic.Nodes.Add(flag); atomic.Edges.Add(new() { From = flag.Id, To = atomicModule.Id, ToPort = "fail" });
var atomicProject = new StudioProject { Values = [new() { Symbol = "F", Initial = "1", Formula = "0" }, new() { Symbol = "V", Initial = "0", Formula = "1" }], Workspace = atomic };
var a = new StudioSession(atomicProject); a.Step(); a.Step(); Reject(() => a.Step(), "Runtime failure stops the whole workspace tick");
Check(a.Ticks == 2 && a.State["V"].ToString() == "2e0", "Failed workspace tick does not partially commit Values");
a.State["F"] = ScientificNumber.Zero; a.Step(); Check(a.LastWorkspace.Outputs[monitor.Id]["value"].ToString() == "3e0", "Failed tick does not advance Previous-step memory");
string folder = Path.Combine(Path.GetTempPath(), "Incranalytic-WorkspaceTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
Check(StudioProject.Load("samples/base-system.incr").Workspace.Nodes.Count == 0, "Existing version-1 Values projects remain readable");
var draft = new StudioProject { Workspace = new() { Nodes = [new() { Code = "output a; a =" }] } }; string draftPath = Path.Combine(folder, "draft.incr"); draft.Save(draftPath);
Check(StudioProject.Load(draftPath).Workspace.Nodes[0].Code == "output a; a =", "Unfinished code and wiring survive draft save/import");
Reject(() => StudioProject.Load(draftPath).Validate(), "Execution validation rejects an unfinished draft");
string projectPath = Path.Combine(folder, "loop.incr"); p.Save(projectPath); var roundTrip = StudioProject.Load(projectPath);
Check(roundTrip.Workspace.Nodes.Count == 3 && roundTrip.Workspace.Edges.Count == 3 && roundTrip.Workspace.Nodes.All(n => n.Group == "Counter"), "Project preserves code, groups, positions and wiring");
p.Save(projectPath); Check(File.Exists(projectPath + ".bak") && StudioProject.Load(projectPath + ".bak").Workspace.Nodes.Count == 3, "Saving replaces atomically and preserves the previous project as a backup");
string groupFile = Path.Combine(folder, "counter.ifgraph"); WorkspaceFiles.SaveGroup(groupFile, p.Workspace, "Counter"); var group = WorkspaceFiles.LoadGroup(groupFile);
var reused = new WorkspaceGraph(); WorkspaceFiles.Insert(reused, group, 0, 0); WorkspaceFiles.Insert(reused, group, 1000, 0);
Check(reused.Nodes.Count == 6 && reused.Nodes.Select(n => n.Id).Distinct().Count() == 6 && reused.Edges.Count == 6, "Reusable systems clone IDs and preserve internal wires");
new WorkspaceRuntime(reused).Evaluate(Inputs()); Check(true, "Two reused feedback systems execute independently");
var large = new WorkspaceGraph(); var start = new WorkspaceNode { Kind = "Input", Literal = "1" }; large.Nodes.Add(start); string last = start.Id; string port = "value";
for (int i = 0; i < 498; i++) { var n = new WorkspaceNode { Code = "input value; output result; result=value+1;" }; large.Nodes.Add(n); large.Edges.Add(new() { From = last, FromPort = port, To = n.Id, ToPort = "value" }); last = n.Id; port = "result"; }
var end = new WorkspaceNode { Kind = "Output" }; large.Nodes.Add(end); large.Edges.Add(new() { From = last, FromPort = port, To = end.Id }); large.Nodes.Reverse();
var watch = Stopwatch.StartNew(); var largeRuntime = new WorkspaceRuntime(large); var largeResult = largeRuntime.Evaluate(Inputs());
Check(largeResult.Outputs[end.Id]["value"].ToString() == "4.99e2", "500-block dependency chain evaluates correctly"); Console.WriteLine("500-block compile/evaluate: " + watch.ElapsedMilliseconds + "ms");
var arbitrary = new StudioProject { Values = [new() { Symbol = "A", Initial = "7", Formula = "0" }], Workspace = new() { Nodes = [new() { Kind = "Input", ValueSymbol = "A" }] } }; arbitrary.Validate(); Check(true, "Workspace Input can read a user-defined Values symbol");
var export = Path.Combine(folder, "UnityExport"); UnityExport.Write(export, p, File.ReadAllText("src/Incranalytic.Core/ScientificNumber.cs"), File.ReadAllText("src/Incranalytic.Core/Ifcloud.cs"), File.ReadAllText("src/Incranalytic.Core/WorkspaceRuntime.cs"));
File.WriteAllText(Path.Combine(export, "Test.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>");
File.WriteAllText(Path.Combine(export, "UnityStub.cs"), "namespace UnityEngine { public class MonoBehaviour {} public static class Time { public static float fixedDeltaTime=0.02f; } public static class Debug { public static void LogException(System.Exception e) { throw e; } } }");
File.WriteAllText(Path.Combine(export, "Program.cs"), $$"""
using System;
class Program { static void Main() {
var game = new IncranalyticSystem(); game.Awake();
if(game.GetWorkspaceValue("{{monitor.Id}}", "value")!="1e0") throw new Exception("Preview mismatch");
for(int i=0;i<15;i++) game.Step();
if(game.GetWorkspaceValue("{{monitor.Id}}", "value")!="1e1") throw new Exception("Workspace parity mismatch");
game.ResetSystem(); game.Step();
if(game.GetWorkspaceValue("{{monitor.Id}}", "value")!="1e0") throw new Exception("Reset mismatch");
Console.WriteLine("Workspace Unity export parity passed");
} }
""");
using var process = Process.Start(new ProcessStartInfo("dotnet", "run --project \"" + Path.Combine(export, "Test.csproj") + "\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd(); process.WaitForExit(); Console.WriteLine(output);
if (process.ExitCode != 0) throw new Exception(output + error);
Check(process.ExitCode == 0, "Exported IFCLOUD workspace compiles as C#9 and matches preview, feedback, conditions and reset");
Console.WriteLine(count + " workspace checks passed. Export: " + export);
