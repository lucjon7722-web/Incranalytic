using System.IO.Compression;
using Incranalytic.Core;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
void Reject(Action action, string name) { try { action(); } catch { Check(true, name); return; } throw new Exception("FAIL: " + name); }
var folder = Path.Combine(Path.GetTempPath(), "Incranalytic-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var sample = Path.GetFullPath(args.FirstOrDefault() ?? "samples/incremental-demo.json");
using var input = ProjectInput.Open(sample);
var analysis = Analyzer.Analyze(input);
Check(analysis.Findings.Count == 20, "Recover all 20 known demo numbers and formulas");
Check(analysis.Findings.Count(f => f.Confidence == "Explicit formula") == 3, "Identify three explicit formulas");
Check(analysis.Findings.All(f => f.Source == "incremental-demo.json" && f.Location.StartsWith("$.")), "Every finding has source evidence");
var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["baseCost"] = 15, ["costGrowth"] = 1.15, ["level"] = 10, ["totalApples"] = 1_000_000 };
Check(Math.Abs(Formula.Evaluate("baseCost * costGrowth ^ level", vars) - 60.6833660356) < 0.000001, "Level-10 cost matches independent expected value");
Check(Formula.Evaluate("floor(totalApples ^ 0.5 / 100)", vars) == 10, "Prestige reward calculation");
Check(Formula.Evaluate("2^3^2", vars) == 512, "Exponent associativity");
Check(Formula.Evaluate("-2^2", vars) == -4, "Unary minus precedence");
Check(Formula.Evaluate("max(2, 3) + 2 * (4+1)", vars) == 13, "Functions and arithmetic precedence");
Reject(() => Formula.Evaluate("1/0", vars), "Division by zero is rejected");
Reject(() => Formula.Evaluate("missing+1", vars), "Missing variables are reported");
Reject(() => Formula.Evaluate("sqrt(-1)", vars), "Nonfinite results are rejected");
Reject(() => Formula.Evaluate("System.IO.File.Delete(x)", vars), "Formula cannot execute code");
var zip = Path.Combine(folder, "demo.apk");
using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) { archive.CreateEntryFromFile(sample, "assets/incremental-demo.json"); using var writer = new StreamWriter(archive.CreateEntry("classes.dex").Open()); writer.Write("demo placeholder"); }
using (var package = ProjectInput.Open(zip))
{
    Check(package.Engine == "Android (engine unknown)", "APK engine detection");
    Check(Analyzer.Analyze(package).Findings.Count == 20, "Analyze data directly inside an APK");
    var export = Path.Combine(folder, "copy.apk"); package.ExportOriginal(export);
    Check(File.ReadAllBytes(zip).SequenceEqual(File.ReadAllBytes(export)), "Exported original is byte-for-byte identical");
    package.ExtractAll(Path.Combine(folder, "extracted"));
    Check(File.Exists(Path.Combine(folder, "extracted/assets/incremental-demo.json")), "Safe extraction preserves folders");
    Reject(() => package.ExtractAll(Path.Combine(folder, "extracted")), "Extraction refuses overwrite");
    foreach (var ext in new[] { ".html", ".json", ".csv" }) { var report = Path.Combine(folder, "report" + ext); Reports.Export(report, package, analysis); Check(new FileInfo(report).Length > 100, "Export " + ext); }
}
var bad = Path.Combine(folder, "unsafe.zip");
using (var archive = ZipFile.Open(bad, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open()); writer.Write("bad"); }
Reject(() => ProjectInput.Open(bad), "Archive traversal is rejected before extraction");
var csv = Path.Combine(folder, "values.csv"); File.WriteAllText(csv, "name,cost,rate\ntree,15,2.5\n");
using (var data = ProjectInput.Open(csv)) Check(Analyzer.Analyze(data).Findings.Count == 2, "CSV numeric columns are recovered");
Console.WriteLine($"\n{passed} checks passed. Test artifacts: {folder}");
