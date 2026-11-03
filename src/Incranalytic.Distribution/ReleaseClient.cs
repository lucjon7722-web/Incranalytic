using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Incranalytic.Distribution;

public sealed record ReleaseAsset(long Id, string Name, long Size, string Digest);
public sealed record InstallableRelease(string Tag, Version Version, string Name, string Notes, bool Preview, ReleaseAsset Installer, ReleaseAsset Checksum)
{
    public override string ToString() => Tag + (Preview ? " (preview)" : "");
}

public sealed class ReleaseClient : IDisposable
{
    public const string Repository = "lucjon7722-web/Incranalytic";
    public const string Api = "https://api.github.com/repos/" + Repository;
    readonly HttpClient http;
    public ReleaseClient(HttpClient? transport = null)
    {
        http = transport ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        http.Timeout = TimeSpan.FromMinutes(15);
    }
    public async Task<List<InstallableRelease>> ListAsync(string token, bool includePreviews, CancellationToken cancel)
    {
        var list = new List<InstallableRelease>();
        for (int page = 1; page <= 20; page++)
        {
            using var response = await SendAsync(new Uri(Api + "/releases?per_page=100&page=" + page), token, false, cancel);
            var data = await ReadLimitedAsync(response, 8_000_000, cancel);
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("GitHub returned an invalid releases list.");
            list.AddRange(Parse(doc.RootElement, includePreviews));
            if (doc.RootElement.GetArrayLength() < 100) break;
        }
        return list.GroupBy(r => r.Tag, StringComparer.Ordinal).Select(g => g.First()).OrderByDescending(r => r.Version).ThenBy(r => r.Preview).ToList();
    }
    public static List<InstallableRelease> Parse(JsonElement array, bool includePreviews)
    {
        var result = new List<InstallableRelease>();
        foreach (var element in array.EnumerateArray())
        {
            bool draft = element.TryGetProperty("draft", out var d) && d.GetBoolean();
            bool preview = element.TryGetProperty("prerelease", out var p) && p.GetBoolean();
            if (draft || preview && !includePreviews) continue;
            string tag = Text(element, "tag_name");
            var match = Regex.Match(tag, @"^v?(\d+\.\d+\.\d+)(?:-[A-Za-z0-9.-]+)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version)) continue;
            string name = "Incranalytic-" + tag.TrimStart('v') + "-Setup.exe";
            if (!element.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            ReleaseAsset? installer = null, checksum = null;
            foreach (var a in assets.EnumerateArray())
            {
                string assetName = Text(a, "name");
                if (assetName != name && assetName != Path.ChangeExtension(name, ".sha256")) continue;
                if (!a.TryGetProperty("id", out var id) || !id.TryGetInt64(out long assetId) || assetId <= 0 || !a.TryGetProperty("size", out var s) || !s.TryGetInt64(out long size) || size <= 0) continue;
                var asset = new ReleaseAsset(assetId, assetName, size, Text(a, "digest"));
                if (assetName == name) installer = installer == null ? asset : throw new InvalidDataException("Release has duplicate installer names.");
                else checksum = checksum == null ? asset : throw new InvalidDataException("Release has duplicate checksum names.");
            }
            if (installer != null && checksum != null && installer.Size <= 2_000_000_000 && checksum.Size <= 4096)
                result.Add(new(tag, version, Text(element, "name"), Text(element, "body"), preview, installer, checksum));
        }
        return result;
    }
    static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    public static bool IsAllowedUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && new[] { "api.github.com", "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    async Task<HttpResponseMessage> SendAsync(Uri uri, string token, bool binary, CancellationToken cancel)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            if (!IsAllowedUri(uri)) throw new InvalidDataException("Download redirected outside GitHub's approved HTTPS hosts.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Incranalytic-Version-Manager/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(binary ? "application/octet-stream" : "application/vnd.github+json"));
            if (uri.Host == "api.github.com")
            {
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var next = response.Headers.Location; response.Dispose();
                if (!binary || next == null) throw new InvalidDataException("Unexpected GitHub redirect.");
                uri = next.IsAbsoluteUri ? next : new Uri(uri, next); continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode; response.Dispose();
                throw new HttpRequestException(status switch
                {
                    HttpStatusCode.NotFound => "The release is private or unavailable. For this private repository, enter a GitHub token with repository Contents: Read access.",
                    HttpStatusCode.Unauthorized => "GitHub rejected the access token. Check that it has not expired.",
                    HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => "GitHub denied the request or its request limit was reached. Check repository access or try later.",
                    _ => "GitHub request failed (HTTP " + (int)status + "). Try refreshing later."
                });
            }
            return response;
        }
        throw new InvalidDataException("Too many download redirects.");
    }
    static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("GitHub response exceeded its size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancel); using var buffer = new MemoryStream();
        var block = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(block, cancel)) > 0) { if (buffer.Length + read > limit) throw new InvalidDataException("GitHub response exceeded its size limit."); await buffer.WriteAsync(block.AsMemory(0, read), cancel); }
        return buffer.ToArray();
    }
    public static string ReadChecksum(string text, string expectedFile)
    {
        foreach (string line in text.TrimStart('\uFEFF').Split('\n'))
        {
            var m = Regex.Match(line.TrimEnd('\r'), @"^([A-Fa-f0-9]{64})[ \t]+\*?(.+?)\s*$");
            if (m.Success && m.Groups[2].Value == expectedFile) return m.Groups[1].Value.ToLowerInvariant();
        }
        throw new InvalidDataException("The release checksum does not name the selected installer.");
    }
    public async Task<string> DownloadVerifiedAsync(InstallableRelease release, string token, string cacheRoot, IProgress<int>? progress, CancellationToken cancel)
    {
        if (!Regex.IsMatch(release.Tag, @"^v?\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$")) throw new InvalidDataException("Invalid release tag.");
        string expectedName = "Incranalytic-" + release.Tag.TrimStart('v') + "-Setup.exe";
        if (release.Installer.Name != expectedName || release.Checksum.Name != Path.ChangeExtension(expectedName, ".sha256") || release.Installer.Id <= 0 || release.Checksum.Id <= 0 || release.Installer.Size <= 0 || release.Installer.Size > 2_000_000_000) throw new InvalidDataException("Invalid release assets.");
        string expected;
        using (var sum = await SendAsync(new Uri(Api + "/releases/assets/" + release.Checksum.Id), token, true, cancel)) expected = ReadChecksum(System.Text.Encoding.UTF8.GetString(await ReadLimitedAsync(sum, 4096, cancel)), expectedName);
        if (release.Installer.Digest.Length > 0 && !string.Equals(release.Installer.Digest, "sha256:" + expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("GitHub digest and release checksum disagree.");
        string directory = Path.Combine(cacheRoot, release.Tag);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, expectedName);
        if (File.Exists(destination))
        {
            await using var existing = File.OpenRead(destination);
            if (new FileInfo(destination).Length == release.Installer.Size && Convert.ToHexString(await SHA256.HashDataAsync(existing, cancel)).Equals(expected, StringComparison.OrdinalIgnoreCase)) { progress?.Report(100); return destination; }
        }
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await SendAsync(new Uri(Api + "/releases/assets/" + release.Installer.Id), token, true, cancel);
            if (response.Content.Headers.ContentLength is long length && length != release.Installer.Size) throw new InvalidDataException("Installer download length differs from its release metadata.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancel))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                var bytes = new byte[65536]; long total = 0; int read;
                while ((read = await input.ReadAsync(bytes, cancel)) > 0)
                {
                    total += read; if (total > release.Installer.Size) throw new InvalidDataException("Installer download exceeded its expected size.");
                    await output.WriteAsync(bytes.AsMemory(0, read), cancel); progress?.Report((int)(total * 100 / release.Installer.Size));
                }
                if (total != release.Installer.Size) throw new InvalidDataException("Installer download was incomplete.");
                await output.FlushAsync(cancel);
            }
            await using (var file = File.OpenRead(partial)) if (!Convert.ToHexString(await SHA256.HashDataAsync(file, cancel)).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer SHA-256 verification failed. Nothing was installed.");
            File.Move(partial, destination, true); progress?.Report(100); return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    public void Dispose() => http.Dispose();
}
