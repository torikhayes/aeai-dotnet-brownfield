namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Exercises security-report-render.sh (FR-007, FR-009, Edge Case §2).
/// </summary>
[TestClass]
public class ReportRenderTests
{
    private string _findingsDir = null!;
    private string _reportPath = null!;

    [TestInitialize]
    public void Setup()
    {
        _findingsDir = Path.Combine(Path.GetTempPath(), "security-report-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_findingsDir);
        _reportPath = Path.Combine(_findingsDir, "report.md");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_findingsDir))
        {
            Directory.Delete(_findingsDir, recursive: true);
        }
    }

    [TestMethod]
    public void ZeroFindings_RendersExplicitStatement_NotEmpty()
    {
        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--out", _reportPath);

        Assert.AreEqual(0, exitCode, stderr);
        var report = File.ReadAllText(_reportPath);
        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
        StringAssert.Contains(report, "Zero findings across all three review capabilities");
        StringAssert.Contains(report, "No findings from this capability have been recorded yet");
    }

    [TestMethod]
    public void MultiSourceFindings_AreGroupedUnderTheirOwnCapabilitySection()
    {
        var runId = ScriptRunner.StartRun(_findingsDir, "code,dependency");
        UpsertFinding(runId, "code", "Missing authz check", "d1", "high", "OrdersController.cs:40");
        UpsertFinding(runId, "dependency", "Vulnerable Npgsql version", "d2", "medium", "Npgsql 8.0.0 / GHSA-xxxx", relevance: "reachable");

        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--out", _reportPath);
        Assert.AreEqual(0, exitCode, stderr);

        var report = File.ReadAllText(_reportPath);
        StringAssert.Contains(report, "## Code Review");
        StringAssert.Contains(report, "Missing authz check");
        StringAssert.Contains(report, "## Dependency/CVE Review");
        StringAssert.Contains(report, "Vulnerable Npgsql version");
        StringAssert.Contains(report, "Relevance: reachable");
    }

    [TestMethod]
    public void LinkedFindings_CollapseIntoOneConsolidatedEntry()
    {
        var run1 = ScriptRunner.StartRun(_findingsDir, "code");
        var codeId = UpsertFinding(run1, "code", "Missing authz check", "d1", "high", "OrdersController.cs:40");

        var run2 = ScriptRunner.StartRun(_findingsDir, "adversarial");
        var advId = UpsertFinding(run2, "adversarial", "Authz bypass exploited", "d2", "critical", "POST /orders with victim buyerId");

        var (linkExit, _, linkErr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "link", "--finding-ids", $"{codeId},{advId}");
        Assert.AreEqual(0, linkExit, linkErr);

        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--out", _reportPath);
        Assert.AreEqual(0, exitCode, stderr);

        var report = File.ReadAllText(_reportPath);
        var consolidatedSection = report.Substring(report.IndexOf("## All Findings", StringComparison.Ordinal));

        // Exactly one "Detected by" line listing both sources, not two separate entries.
        int detectedByCount = consolidatedSection.Split("Detected by").Length - 1;
        Assert.AreEqual(1, detectedByCount, "linked findings from different capabilities must collapse into a single consolidated entry");
        StringAssert.Contains(consolidatedSection, "Adversarial Review");
        StringAssert.Contains(consolidatedSection, "Code Review");
    }

    [TestMethod]
    public void CleanAdversarialRun_ReportsAttemptedScenarios_NotNothing()
    {
        var runId = ScriptRunner.StartRun(_findingsDir, "adversarial");
        var (scenarioExit, _, scenarioErr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "add-scenario", "--run-id", runId,
            "--description", "Attempted SQL injection on catalog search query param",
            "--target-service", "Catalog.API",
            "--outcome", "no-issue");
        Assert.AreEqual(0, scenarioExit, scenarioErr);

        var (finalizeExit, _, finalizeErr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "finalize-run", "--run-id", runId, "--full", "true");
        Assert.AreEqual(0, finalizeExit, finalizeErr);

        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--out", _reportPath);
        Assert.AreEqual(0, exitCode, stderr);

        var report = File.ReadAllText(_reportPath);
        var adversarialSection = report.Substring(
            report.IndexOf("## Adversarial Review", StringComparison.Ordinal),
            report.IndexOf("## All Findings", StringComparison.Ordinal) - report.IndexOf("## Adversarial Review", StringComparison.Ordinal));

        StringAssert.Contains(adversarialSection, "Scenarios attempted in run");
        StringAssert.Contains(adversarialSection, "Attempted SQL injection on catalog search query param");
        StringAssert.Contains(adversarialSection, "outcome: no-issue");
    }

    [TestMethod]
    public void CapabilitySnapshot_ContainsOnlyThatCapabilitysFindings_WithDateInTitle()
    {
        var codeRun = ScriptRunner.StartRun(_findingsDir, "code");
        UpsertFinding(codeRun, "code", "Missing authz check", "d1", "high", "OrdersController.cs:40");

        var depRun = ScriptRunner.StartRun(_findingsDir, "dependency");
        UpsertFinding(depRun, "dependency", "Vulnerable Npgsql version", "d2", "medium", "Npgsql 8.0.0 / GHSA-xxxx", relevance: "reachable");

        var snapshotPath = Path.Combine(_findingsDir, "snapshot.md");
        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--capability", "code",
            "--snapshot-date", "20260713",
            "--out", snapshotPath);
        Assert.AreEqual(0, exitCode, stderr);

        var snapshot = File.ReadAllText(snapshotPath);
        StringAssert.Contains(snapshot, "Code Review — 20260713");
        StringAssert.Contains(snapshot, "Missing authz check");
        Assert.IsFalse(snapshot.Contains("Vulnerable Npgsql version"), "a code-capability snapshot must not include dependency findings");
        // Only one heading in a standalone snapshot (the title line) — no duplicate "## Code Review" section heading.
        Assert.AreEqual(1, snapshot.Split('\n').Count(l => l.StartsWith('#')));
    }

    [TestMethod]
    public void CapabilitySnapshot_WithoutSnapshotDate_Errors()
    {
        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--capability", "code",
            "--out", Path.Combine(_findingsDir, "snapshot.md"));

        Assert.AreNotEqual(0, exitCode);
        StringAssert.Contains(stderr, "--snapshot-date is required");
    }

    [TestMethod]
    public void FullReport_StillIdentical_WhenCapabilityFlagOmitted()
    {
        var runId = ScriptRunner.StartRun(_findingsDir, "code");
        UpsertFinding(runId, "code", "Missing authz check", "d1", "high", "OrdersController.cs:40");

        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-report-render.sh",
            "--findings-dir", _findingsDir,
            "--out", _reportPath);
        Assert.AreEqual(0, exitCode, stderr);

        var report = File.ReadAllText(_reportPath);
        StringAssert.Contains(report, "# Security Review — Consolidated Findings Report");
        StringAssert.Contains(report, "## All Findings (grouped by severity, linked findings collapsed)");
    }

    private string UpsertFinding(string runId, string source, string title, string description, string severity, string evidence, string? relevance = null)
    {
        var args = new List<string>
        {
            "--findings-dir", _findingsDir,
            "upsert-finding",
            "--run-id", runId,
            "--source", source,
            "--title", title,
            "--description", description,
            "--severity", severity,
            "--evidence", evidence,
        };
        if (relevance is not null)
        {
            args.Add("--relevance");
            args.Add(relevance);
        }

        var (exitCode, stdout, stderr) = ScriptRunner.Run("security-findings-store.sh", args.ToArray());
        Assert.AreEqual(0, exitCode, stderr);
        return JsonDocument.Parse(stdout).RootElement.GetProperty("id").GetString()!;
    }
}
