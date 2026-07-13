namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Exercises security-osv-query.sh (FR-003/FR-004, contracts/osv-dev-api.md) against a
/// local mock HTTP server standing in for OSV.dev, covering matched, unmatched, and
/// unreachable-endpoint cases without making real network calls.
/// </summary>
[TestClass]
public class OsvQueryTests
{
    private string _componentsPath = null!;
    private string _outPath = null!;

    [TestInitialize]
    public void Setup()
    {
        _componentsPath = Path.Combine(Path.GetTempPath(), $"components-{Guid.NewGuid()}.json");
        _outPath = Path.Combine(Path.GetTempPath(), $"matches-{Guid.NewGuid()}.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in new[] { _componentsPath, _outPath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void WriteComponents(string path, object components) =>
        File.WriteAllText(path, JsonSerializer.Serialize(components));

    [TestMethod]
    public void MatchedVulnerablePackage_ProducesVulnerabilityMatch()
    {
        WriteComponents(_componentsPath, new object[]
        {
            new { name = "Vulnerable.Package", ecosystem = "nuget", version = "1.0.0", usedBy = new[] { "FakeProject" }, shipsInRuntime = true },
        });

        using var mock = new MockOsvServer(vulnerablePackageName: "Vulnerable.Package", vulnId: "GHSA-fake-1234");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-osv-query.sh",
            "--components", _componentsPath,
            "--out", _outPath,
            "--osv-base-url", mock.BaseUrl);

        Assert.AreEqual(0, exitCode, stderr);
        Assert.AreEqual(1, JsonDocument.Parse(stdout).RootElement.GetProperty("matchCount").GetInt32());

        var matches = JsonDocument.Parse(File.ReadAllText(_outPath)).RootElement;
        Assert.AreEqual(1, matches.GetArrayLength());
        Assert.AreEqual("GHSA-fake-1234", matches[0].GetProperty("id").GetString());
        Assert.AreEqual("Vulnerable.Package", matches[0].GetProperty("dependencyComponentName").GetString());
    }

    [TestMethod]
    public void UnmatchedSafePackage_ProducesNoVulnerabilityMatch()
    {
        WriteComponents(_componentsPath, new object[]
        {
            new { name = "Safe.Package", ecosystem = "nuget", version = "2.0.0", usedBy = new[] { "FakeProject" }, shipsInRuntime = true },
        });

        using var mock = new MockOsvServer(vulnerablePackageName: "Vulnerable.Package", vulnId: "GHSA-fake-1234");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-osv-query.sh",
            "--components", _componentsPath,
            "--out", _outPath,
            "--osv-base-url", mock.BaseUrl);

        Assert.AreEqual(0, exitCode, stderr);
        Assert.AreEqual(0, JsonDocument.Parse(stdout).RootElement.GetProperty("matchCount").GetInt32());
    }

    [TestMethod]
    public void ComponentWithNoOsvEcosystem_IsSkippedAndReported_NotSilentlyOmitted()
    {
        WriteComponents(_componentsPath, new object[]
        {
            new { name = "docker.io/library/redis (redis)", ecosystem = "container", version = "aspire-managed-default", usedBy = new[] { "AppHost" }, shipsInRuntime = true },
        });

        using var mock = new MockOsvServer(vulnerablePackageName: "Vulnerable.Package", vulnId: "GHSA-fake-1234");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-osv-query.sh",
            "--components", _componentsPath,
            "--out", _outPath,
            "--osv-base-url", mock.BaseUrl);

        Assert.AreEqual(0, exitCode, stderr);
        var skipped = JsonDocument.Parse(stdout).RootElement.GetProperty("skippedNoEcosystem");
        Assert.AreEqual(1, skipped.GetArrayLength());
        Assert.AreEqual("docker.io/library/redis (redis)", skipped[0].GetString());
    }

    [TestMethod]
    public void UnreachableOsvDev_FailsCleanly_NotSilentEmptyResult()
    {
        WriteComponents(_componentsPath, new object[]
        {
            new { name = "Vulnerable.Package", ecosystem = "nuget", version = "1.0.0", usedBy = new[] { "FakeProject" }, shipsInRuntime = true },
        });

        // Nothing is listening on this port — simulates OSV.dev being unreachable.
        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-osv-query.sh",
            "--components", _componentsPath,
            "--out", _outPath,
            "--osv-base-url", "http://127.0.0.1:1");

        Assert.AreNotEqual(0, exitCode, "an unreachable OSV.dev must fail the run, not silently succeed with zero matches");
        StringAssert.Contains(stderr, "unreachable");
        Assert.IsFalse(File.Exists(_outPath), "no output file should be written on failure");
    }

    /// <summary>Minimal in-process stand-in for OSV.dev's querybatch + vuln detail endpoints.</summary>
    private sealed class MockOsvServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _vulnerablePackageName;
        private readonly string _vulnId;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public string BaseUrl { get; }

        public MockOsvServer(string vulnerablePackageName, string vulnId)
        {
            _vulnerablePackageName = vulnerablePackageName;
            _vulnId = vulnId;

            int port = GetFreeTcpPort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _loop = Task.Run(ServeLoop);
        }

        private static int GetFreeTcpPort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task ServeLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (Exception) when (_cts.IsCancellationRequested || !_listener.IsListening)
                {
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url!.AbsolutePath == "/v3/querybatch")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var body = JsonDocument.Parse(await reader.ReadToEndAsync()).RootElement;
                    var results = body.GetProperty("queries").EnumerateArray()
                        .Select(q => q.GetProperty("package").GetProperty("name").GetString() == _vulnerablePackageName
                            ? new { vulns = new[] { new { id = _vulnId } } }
                            : new { vulns = Array.Empty<object>() })
                        .ToArray();
                    await WriteJson(ctx, new { results });
                }
                else if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url!.AbsolutePath == $"/v3/vulns/{_vulnId}")
                {
                    await WriteJson(ctx, new
                    {
                        id = _vulnId,
                        severity = new[] { new { type = "CVSS_V3", score = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H" } },
                    });
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                }
            }
        }

        private static async Task WriteJson(HttpListenerContext ctx, object payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
        }
    }
}
