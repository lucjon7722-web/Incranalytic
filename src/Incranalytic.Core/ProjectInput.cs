using System.IO.Compression;
using System.Security.Cryptography;

namespace Incranalytic.Core;

public sealed record GameFile(string Path, long Size);

public sealed class ProjectInput : IDisposable
{
    public const int MaxEntries = 20_000;
    public const long MaxTotalBytes = 1_073_741_824;
    public const long MaxFileBytes = 134_217_728;

    private readonly ZipArchive? archive;
    private readonly FileStream? archiveStream;
    private readonly Dictionary<string, string> diskFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ZipArchiveEntry> zipFiles = new(StringComparer.OrdinalIgnoreCase);

    public string InputPath { get; }
    public string Name => System.IO.Path.GetFileName(InputPath.TrimEnd('\\', '/'));
    public bool IsArchive => archive != null;
    public bool IsDirectory => Directory.Exists(InputPath);
    public string InputKind => IsArchive ? "Android / ZIP package" : IsDirectory ? "Folder" : "Data file";
    public IReadOnlyList<GameFile> Files { get; }
    public string Engine { get; }
    public string? Sha256 { get; }

    private ProjectInput(string path)
    {
        InputPath = System.IO.Path.GetFullPath(path);
        var files = new List<GameFile>();
        long total = 0;

        if (Directory.Exists(InputPath))
        {
            var pending = new Stack<string>();
            pending.Push(InputPath);
            while (pending.Count > 0)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                {
                    var attributes = File.GetAttributes(entry);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (attributes.HasFlag(FileAttributes.Directory)) { pending.Push(entry); continue; }
                    var relative = Normalize(System.IO.Path.GetRelativePath(InputPath, entry));
                    ValidateName(relative);
                    var size = new FileInfo(entry).Length;
                    CheckLimits(++Count, size, ref total);
                    diskFiles.Add(relative, entry);
                    files.Add(new GameFile(relative, size));
                }
            }
        }
        else if (File.Exists(InputPath) && IsZip(InputPath))
        {
            archiveStream = new FileStream(InputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var relative = Normalize(entry.FullName);
                    ValidateName(relative);
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("Package contains a symbolic link.");
                    var size = entry.Length;
                    CheckLimits(++Count, size, ref total);
                    if (entry.CompressedLength > 0 && size / Math.Max(1, entry.CompressedLength) > 200)
                        throw new InvalidDataException("Package contains an unusually compressed file.");
                    zipFiles.Add(relative, entry);
                    files.Add(new GameFile(relative, size));
                }
            }
            catch { archive?.Dispose(); archiveStream.Dispose(); throw; }
        }
        else if (File.Exists(InputPath))
        {
            var size = new FileInfo(InputPath).Length;
            CheckLimits(1, size, ref total);
            var relative = System.IO.Path.GetFileName(InputPath);
            diskFiles.Add(relative, InputPath);
            files.Add(new GameFile(relative, size));
        }
        else throw new FileNotFoundException("Input was not found.", InputPath);

        Files = files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        Engine = DetectEngine(Files);
        Sha256 = IsDirectory ? null : ComputeHash(InputPath);
    }

    private int Count { get; set; }

    public static ProjectInput Open(string path) => new(path);

    public Stream OpenRead(GameFile file)
    {
        if (zipFiles.TryGetValue(file.Path, out var zip)) return zip.Open();
        if (diskFiles.TryGetValue(file.Path, out var disk))
            return new FileStream(disk, FileMode.Open, FileAccess.Read, FileShare.Read);
        throw new FileNotFoundException("File is not in the project.", file.Path);
    }

    public string? ReadText(GameFile file, long maxBytes = 2_000_000)
    {
        if (file.Size > maxBytes) return null;
        using var stream = OpenRead(file);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = stream.Read(block)) > 0)
        {
            if (buffer.Length + count > maxBytes) return null;
            buffer.Write(block, 0, count);
        }
        if (buffer.Length > maxBytes) return null;
        var bytes = buffer.ToArray();
        if (bytes.Take(Math.Min(bytes.Length, 1024)).Contains((byte)0)) return null;
        using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public void ExportOriginal(string destination)
    {
        if (IsDirectory) throw new InvalidOperationException("Folders have no single original package.");
        File.Copy(InputPath, destination, overwrite: false);
    }

    public void ExtractAll(string destination)
    {
        var root = System.IO.Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        foreach (var file in Files)
        {
            var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, file.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Package path escapes the chosen folder.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            if (File.Exists(target)) throw new IOException($"Refusing to overwrite {target}");
            using var input = OpenRead(file);
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static void CheckLimits(int count, long size, ref long total)
    {
        if (count > MaxEntries) throw new InvalidDataException("Too many files in input.");
        if (size < 0 || size > MaxFileBytes) throw new InvalidDataException("Input contains a file over the safety limit (128 MB).");
        total = checked(total + size);
        if (total > MaxTotalBytes) throw new InvalidDataException("Input exceeds the 1 GB extraction safety limit.");
    }

    private static void ValidateName(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(':') ||
            path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"Unsafe file path in input: {path}");
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
    private static bool IsZip(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4];
        return stream.Read(header) == 4 && header[0] == 'P' && header[1] == 'K' &&
               (header[2] == 3 || header[2] == 5 || header[2] == 7);
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string DetectEngine(IEnumerable<GameFile> files)
    {
        var names = files.Select(f => f.Path.ToLowerInvariant()).ToArray();
        if (names.Any(n => n.Contains("/il2cpp_data/") || n.EndsWith("libil2cpp.so"))) return "Unity (IL2CPP)";
        if (names.Any(n => n.Contains("/unity/", StringComparison.Ordinal) || n.EndsWith("globalgamemanagers") || n.EndsWith("assembly-csharp.dll"))) return "Unity";
        if (names.Any(n => n.EndsWith(".pck") || n.EndsWith("project.godot") || n.Contains("libgodot"))) return "Godot";
        if (names.Any(n => n.EndsWith("cocos2d-js.js") || n.Contains("cocos") || n.EndsWith("settings.js"))) return "Cocos / JavaScript";
        if (names.Any(n => n.EndsWith("index.html"))) return "Web / HTML5";
        if (names.Any(n => n.EndsWith("classes.dex") || n.EndsWith("androidmanifest.xml"))) return "Android (engine unknown)";
        return "Structured files / unknown engine";
    }

    public void Dispose()
    {
        archive?.Dispose();
        archiveStream?.Dispose();
    }
}
