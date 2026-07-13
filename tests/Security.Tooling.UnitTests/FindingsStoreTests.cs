namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Exercises security-findings-store.sh (specs/008-adversarial-security-review data-model.md
/// validation rules: FR-008, SC-003, Edge Case §2 linking).
/// </summary>
[TestClass]
public class FindingsStoreTests
{
    private string _findingsDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _findingsDir = Path.Combine(Path.GetTempPath(), "security-findings-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_findingsDir);
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
    public void InsertNewFinding_CreatesRecordWithStatusNew()
    {
        var runId = ScriptRunner.StartRun(_findingsDir, "code");

        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "upsert-finding",
            "--run-id", runId,
            "--source", "code",
            "--title", "Unsafe deserialization",
            "--description", "desc",
            "--severity", "high",
            "--evidence", "Foo.cs:12");

        Assert.AreEqual(0, exitCode, stderr);
        var result = JsonDocument.Parse(stdout).RootElement;
        Assert.AreEqual("new", result.GetProperty("status").GetString());
        Assert.IsTrue(result.GetProperty("isNew").GetBoolean());
    }

    [TestMethod]
    public void ReconfirmExistingFinding_UpdatesLastSeenRunId_StatusUnchanged()
    {
        var run1 = ScriptRunner.StartRun(_findingsDir, "code");
        UpsertFinding(run1, "code", "Unsafe deserialization", "desc", "high", "Foo.cs:12");

        var run2 = ScriptRunner.StartRun(_findingsDir, "code");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "upsert-finding",
            "--run-id", run2,
            "--source", "code",
            "--title", "Unsafe deserialization",
            "--description", "desc",
            "--severity", "high",
            "--evidence", "Foo.cs:12");

        Assert.AreEqual(0, exitCode, stderr);
        var result = JsonDocument.Parse(stdout).RootElement;
        Assert.AreEqual("new", result.GetProperty("status").GetString());
        Assert.IsFalse(result.GetProperty("isNew").GetBoolean());

        var findings = ReadFindings();
        Assert.AreEqual(1, findings.GetArrayLength(), "reconfirming must not create a duplicate finding");
        Assert.AreEqual(run2, findings[0].GetProperty("lastSeenRunId").GetString());
    }

    [TestMethod]
    public void AcknowledgedFinding_SurvivesRerun_DoesNotFlipBackToNew()
    {
        var run1 = ScriptRunner.StartRun(_findingsDir, "code");
        var findingId = UpsertFinding(run1, "code", "Unsafe deserialization", "desc", "high", "Foo.cs:12");

        var (ackExit, _, ackErr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "ack", "--finding-id", findingId, "--by", "maintainer");
        Assert.AreEqual(0, ackExit, ackErr);

        var run2 = ScriptRunner.StartRun(_findingsDir, "code");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "upsert-finding",
            "--run-id", run2,
            "--source", "code",
            "--title", "Unsafe deserialization",
            "--description", "desc",
            "--severity", "high",
            "--evidence", "Foo.cs:12");

        Assert.AreEqual(0, exitCode, stderr);
        Assert.AreEqual("acknowledged", JsonDocument.Parse(stdout).RootElement.GetProperty("status").GetString());
    }

    [TestMethod]
    public void FindingAbsentFromLatestFullRun_TransitionsToResolved()
    {
        var run1 = ScriptRunner.StartRun(_findingsDir, "code");
        var findingId = UpsertFinding(run1, "code", "Unsafe deserialization", "desc", "high", "Foo.cs:12");
        FinalizeRun(run1);

        // A second full run of the same capability that does not re-detect the finding.
        var run2 = ScriptRunner.StartRun(_findingsDir, "code");
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "finalize-run", "--run-id", run2, "--full", "true");

        Assert.AreEqual(0, exitCode, stderr);
        var resolvedIds = JsonDocument.Parse(stdout).RootElement.GetProperty("resolvedFindingIds");
        Assert.AreEqual(1, resolvedIds.GetArrayLength());
        Assert.AreEqual(findingId, resolvedIds[0].GetString());

        var findings = ReadFindings();
        Assert.AreEqual("resolved", findings[0].GetProperty("status").GetString());
    }

    [TestMethod]
    public void Link_SetsRelatedFindingIds_Bidirectionally()
    {
        var run1 = ScriptRunner.StartRun(_findingsDir, "code");
        var codeFindingId = UpsertFinding(run1, "code", "Missing authz check", "d1", "high", "OrdersController.cs:40");

        var run2 = ScriptRunner.StartRun(_findingsDir, "adversarial");
        var advFindingId = UpsertFinding(run2, "adversarial", "Authz bypass exploited", "d2", "critical", "POST /orders with victim buyerId");

        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "link", "--finding-ids", $"{codeFindingId},{advFindingId}");

        Assert.AreEqual(0, exitCode, stderr);

        var findings = ReadFindings();
        var byId = findings.EnumerateArray().ToDictionary(f => f.GetProperty("id").GetString()!, f => f);

        var codeRelated = byId[codeFindingId].GetProperty("relatedFindingIds").EnumerateArray().Select(x => x.GetString()).ToList();
        var advRelated = byId[advFindingId].GetProperty("relatedFindingIds").EnumerateArray().Select(x => x.GetString()).ToList();

        CollectionAssert.Contains(codeRelated, advFindingId);
        CollectionAssert.Contains(advRelated, codeFindingId);
    }

    private string UpsertFinding(string runId, string source, string title, string description, string severity, string evidence)
    {
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "upsert-finding",
            "--run-id", runId,
            "--source", source,
            "--title", title,
            "--description", description,
            "--severity", severity,
            "--evidence", evidence);
        Assert.AreEqual(0, exitCode, stderr);
        return JsonDocument.Parse(stdout).RootElement.GetProperty("id").GetString()!;
    }

    private void FinalizeRun(string runId)
    {
        var (exitCode, _, stderr) = ScriptRunner.Run(
            "security-findings-store.sh",
            "--findings-dir", _findingsDir,
            "finalize-run", "--run-id", runId, "--full", "true");
        Assert.AreEqual(0, exitCode, stderr);
    }

    private JsonElement ReadFindings()
    {
        var path = Path.Combine(_findingsDir, "findings.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }
}
