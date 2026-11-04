using System.Text.Json;
using System.Text.RegularExpressions;

namespace Incranalytic.Core;

public sealed class StudioPreferences
{
    public bool ShowGrid { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public bool ShowBlockValues { get; set; } = true;
    public bool ShowInspector { get; set; } = true;
    public bool ShowSimulationSettings { get; set; }
    public bool ShowTooltips { get; set; } = true;
    public bool EnableRecovery { get; set; } = true;
    public bool HasSeenWelcome { get; set; }
    public bool ShowWelcomeAtStartup { get; set; }
}

public sealed class StudioProfile
{
    public string Root { get; }
    public StudioPreferences Preferences { get; }
    public string? Warning { get; }
    public StudioProfile(string root)
    {
        Root = Path.GetFullPath(root);
        string file = Path.Combine(Root, "preferences.json");
        try
        {
            if (File.Exists(file))
            {
                if (new FileInfo(file).Length > 65536) throw new InvalidDataException("Preferences file is too large.");
                Preferences = JsonSerializer.Deserialize<StudioPreferences>(File.ReadAllText(file)) ?? throw new InvalidDataException("Invalid preferences.");
            }
            else Preferences = new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { Preferences = new(); Warning = "Preferences could not be loaded; defaults are in use."; }
    }
    public void Save() => AtomicJson.Write(Path.Combine(Root, "preferences.json"), Preferences);
}

public sealed class RecoverySnapshot
{
    public int Format { get; set; } = 1;
    public string? OriginalPath { get; set; }
    public DateTime SavedAtUtc { get; set; }
    public StudioProject Project { get; set; } = new();
}
public sealed record RecoveryCandidate(string FilePath, RecoverySnapshot Snapshot);

public sealed class RecoveryStore : IDisposable
{
    readonly string directory, sessionId = Guid.NewGuid().ToString("N"), currentFile;
    readonly FileStream sessionLock;
    FileStream? claimedLock;
    string? claimedFile;
    public RecoveryStore(string profileRoot)
    {
        directory = Path.Combine(Path.GetFullPath(profileRoot), "recovery"); Directory.CreateDirectory(directory);
        currentFile = Path.Combine(directory, "snapshot-" + sessionId + ".json");
        sessionLock = new FileStream(LockPath(sessionId), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }
    string LockPath(string id) => Path.Combine(directory, "session-" + id + ".lock");
    public void Write(StudioProject project, string? originalPath)
    {
        project.Validate(false);
        AtomicJson.Write(currentFile, new RecoverySnapshot { Project = project, OriginalPath = originalPath, SavedAtUtc = DateTime.UtcNow });
    }
    public List<RecoveryCandidate> FindAvailable()
    {
        var result = new List<RecoveryCandidate>();
        foreach (string file in Directory.EnumerateFiles(directory, "snapshot-*.json"))
        {
            if (file == currentFile || file == claimedFile) continue;
            var match = Regex.Match(Path.GetFileName(file), "^snapshot-([a-f0-9]{32})\\.json$"); if (!match.Success) continue;
            try
            {
                using var probe = new FileStream(LockPath(match.Groups[1].Value), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (new FileInfo(file).Length > 20_000_000) continue;
                var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(file));
                if (snapshot == null || snapshot.Format != 1 || snapshot.Project == null) continue;
                snapshot.Project.Validate(false); result.Add(new(file, snapshot));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or ArgumentException or NullReferenceException) { }
        }
        return result.OrderByDescending(c => c.Snapshot.SavedAtUtc).ToList();
    }
    public void Claim(RecoveryCandidate candidate)
    {
        if (claimedFile != null) throw new InvalidOperationException("A recovery is already open in this window.");
        string file = Path.GetFullPath(candidate.FilePath);
        var match = Regex.Match(Path.GetFileName(file), "^snapshot-([a-f0-9]{32})\\.json$");
        if (!match.Success || Path.GetDirectoryName(file) != directory) throw new InvalidDataException("Recovery is outside this profile.");
        claimedLock = new FileStream(LockPath(match.Groups[1].Value), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        claimedFile = file;
    }
    public void ClearCurrent()
    {
        if (File.Exists(currentFile)) File.Delete(currentFile);
        if (claimedFile != null)
        {
            if (File.Exists(claimedFile)) File.Delete(claimedFile);
            string lockFile = claimedLock!.Name; claimedLock.Dispose(); claimedLock = null; claimedFile = null;
            File.Delete(lockFile);
        }
    }
    public void Dispose()
    {
        string? oldLock = claimedLock?.Name; claimedLock?.Dispose(); sessionLock.Dispose();
        try { File.Delete(LockPath(sessionId)); if (oldLock != null) File.Delete(oldLock); } catch (IOException) { }
    }
}
static class AtomicJson
{
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
                if (stream.Position > 20_000_000) throw new FormatException("Recovery exceeds the 20 MB file limit.");
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
