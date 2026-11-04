using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Incranalytic.Distribution;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
async Task Reject(Func<Task> action, string name) { bool failed = false; try { await action(); } catch { failed = true; } Check(failed, name); }
var payload = Encoding.UTF8.GetBytes("A verified installer fixture, never executed by tests.");
string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
object Release(string tag, bool draft = false, bool preview = false, bool checksum = true) => new
{
    tag_name = tag, name = tag, body = "Version notes", draft, prerelease = preview,
    assets = new[] { new { id = 10, name = "Incranalytic-" + tag.TrimStart('v') + "-Setup.exe", size = payload.Length, digest = "sha256:" + hash }, new { id = checksum ? 11 : 12, name = checksum ? "Incranalytic-" + tag.TrimStart('v') + "-Setup.sha256" : "Other.txt", size = 100, digest = "" } }
};
using var json = JsonDocument.Parse(JsonSerializer.Serialize(new[] { Release("v0.2.0"), Release("v0.3.0"), Release("v0.4.0", true), Release("v0.5.0-beta.1", preview:true), Release("v0.6.0", checksum:false) }));
var listed = ReleaseClient.Parse(json.RootElement, false);
Check(listed.Count == 2 && listed.Any(r => r.Version == new Version(0, 2, 0)) && listed.Any(r => r.Version == new Version(0, 3, 0)), "Catalog exposes older and newer installers");
Check(ReleaseClient.Parse(json.RootElement, true).Count == 3, "Preview releases are opt-in; drafts and missing checksums are excluded");
Check(ReleaseClient.ReadChecksum(hash.ToUpperInvariant() + "  Incranalytic-0.3.0-Setup.exe\n", "Incranalytic-0.3.0-Setup.exe") == hash, "Checksum filename and hexadecimal hash are parsed");
Check(!ReleaseClient.IsAllowedUri(new Uri("http://api.github.com/test")) && !ReleaseClient.IsAllowedUri(new Uri("https://attacker.invalid/file")) && !ReleaseClient.IsAllowedUri(new Uri("https://api.github.com:444/test")), "Plain HTTP, foreign hosts and nonstandard ports are rejected");
Check(ReleaseClient.IsAllowedUri(new Uri("https://release-assets.githubusercontent.com/asset")), "GitHub asset HTTPS host is allowed");
var release = listed.Single(r => r.Tag == "v0.3.0");
string directory = Path.Combine(Path.GetTempPath(), "Incranalytic-DistributionTests-" + Guid.NewGuid().ToString("N"));
int downloads = 0; bool tokenStayedPrivate = true;
using (var client = new ReleaseClient(new HttpClient(new Stub(request =>
{
    bool isApi = request.RequestUri!.Host == "api.github.com";
    if (isApi) tokenStayedPrivate &= request.Headers.Authorization?.Parameter == "dummy-token";
    else tokenStayedPrivate &= request.Headers.Authorization == null;
    if (request.RequestUri.AbsolutePath.Contains("releases/assets/11")) return Bytes(Encoding.UTF8.GetBytes(hash + "  " + release.Installer.Name));
    if (isApi) return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://release-assets.githubusercontent.com/test") } };
    downloads++; return Bytes(payload);
}))))
{
    string file = await client.DownloadVerifiedAsync(release, "dummy-token", directory, null, default);
    Check(File.ReadAllBytes(file).SequenceEqual(payload), "Verified bytes are saved as the selected installer");
    Check(tokenStayedPrivate, "Token is sent to API only, never to asset redirect hosts");
    await client.DownloadVerifiedAsync(release, "dummy-token", directory, null, default);
    Check(downloads == 1, "A cached verified installer is reused");
    Check(Directory.GetFiles(directory, "*.part", SearchOption.AllDirectories).Length == 0, "Successful download leaves no partial file");
}
using (var catalog = new ReleaseClient(new HttpClient(new Stub(_ => Bytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { Release("v0.2.0"), Release("v0.3.0") })))))))
    Check((await catalog.ListAsync("", false, default)).Select(r => r.Tag).SequenceEqual(new[] { "v0.3.0", "v0.2.0" }), "Version list sorts semantically newest first");
string badDirectory = Path.Combine(directory, "bad");
using (var corrupt = new ReleaseClient(new HttpClient(new Stub(r => r.RequestUri!.AbsolutePath.EndsWith("11") ? Bytes(Encoding.UTF8.GetBytes(hash + "  " + release.Installer.Name)) : Bytes(Enumerable.Repeat((byte)0, payload.Length).ToArray())))))
{
    await Reject(() => corrupt.DownloadVerifiedAsync(release, "", badDirectory, null, default), "Corrupted installer bytes are rejected");
    Check(Directory.GetFiles(badDirectory, "*", SearchOption.AllDirectories).Length == 0, "Corruption never leaves an executable or partial download");
}
using (var mismatch = new ReleaseClient(new HttpClient(new Stub(_ => Bytes(Encoding.UTF8.GetBytes(new string('0',64) + "  " + release.Installer.Name))))))
    await Reject(() => mismatch.DownloadVerifiedAsync(release, "", directory, null, default), "API digest and checksum disagreement is rejected");
using (var foreign = new ReleaseClient(new HttpClient(new Stub(r => r.RequestUri!.AbsolutePath.EndsWith("11") ? Bytes(Encoding.UTF8.GetBytes(hash + "  " + release.Installer.Name)) : new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://attacker.invalid/payload") } }))))
    await Reject(() => foreign.DownloadVerifiedAsync(release, "dummy-token", Path.Combine(directory, "foreign"), null, default), "Foreign asset redirects are rejected before credentials or data leave GitHub");
using (var denied = new ReleaseClient(new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))))
    await Reject(() => denied.ListAsync("", false, default), "Private-feed failures are handled without attempting installation");
using (var cancelled = new ReleaseClient(new HttpClient(new Stub(_ => Bytes(payload)))))
{
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    await Reject(() => cancelled.DownloadVerifiedAsync(release, "", directory, null, cancellation.Token), "Cancellation stops work without executing a file");
}
await Reject(() => { ReleaseClient.ReadChecksum(hash + "  Other.exe", release.Installer.Name); return Task.CompletedTask; }, "Checksums for a different executable are rejected");
string interruptedFolder = Path.Combine(directory, "interrupted");
using (var interrupted = new ReleaseClient(new HttpClient(new Stub(r => r.RequestUri!.AbsolutePath.EndsWith("11") ? Bytes(Encoding.UTF8.GetBytes(hash + "  " + release.Installer.Name)) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream()) }))))
{
    await Reject(() => interrupted.DownloadVerifiedAsync(release, "", interruptedFolder, null, default), "Interrupted download stops without an installer");
    Check(Directory.GetFiles(interruptedFolder, "*", SearchOption.AllDirectories).Length == 0, "Interrupted stream removes its partial file");
}
if (args.Contains("--public-live"))
{
    using var publicClient = new ReleaseClient(); var publicReleases = await publicClient.ListAsync("", false, default);
    Check(publicReleases.Any(r => r.Tag == "v0.5.0"), "Public catalog lists 0.5.0 without credentials");
    var selected = publicReleases.Single(r => r.Tag == "v0.5.0");
    string publicFile = await publicClient.DownloadVerifiedAsync(selected, "", directory, null, default);
    Check(new FileInfo(publicFile).Length == selected.Installer.Size, "Public 0.5.0 download passes size and SHA-256 checks without credentials");
}
if (args.Contains("--live"))
{
    string token = Console.In.ReadToEnd().Trim(); if (token.Length == 0) throw new Exception("Live test requires authentication on standard input.");
    using var client = new ReleaseClient(); var releases = await client.ListAsync(token, false, default);
    Check(releases.Any(r => r.Tag == "v0.2.0") && releases.Any(r => r.Tag == "v0.3.0"), "Live private GitHub catalog lists both published versions");
    var old = releases.Single(r => r.Tag == "v0.3.0"); string file = await client.DownloadVerifiedAsync(old, token, directory, null, default);
    Check(new FileInfo(file).Length == old.Installer.Size, "Live private installer download passes size and SHA-256 checks");
    var older = releases.Single(r => r.Tag == "v0.2.0"); string olderFile = await client.DownloadVerifiedAsync(older, token, directory, null, default);
    Check(new FileInfo(olderFile).Length == older.Installer.Size && olderFile != file, "Live version switching downloads and verifies a different selected installer");
    token = "";
}
Console.WriteLine(checks + " distribution checks passed. No installers were executed.");

static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(answer(request)); }
}
sealed class InterruptedStream : Stream
{
    bool read;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) { if (read) throw new IOException("Simulated connection interruption"); read = true; buffer[offset] = 1; return 1; }
    public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
