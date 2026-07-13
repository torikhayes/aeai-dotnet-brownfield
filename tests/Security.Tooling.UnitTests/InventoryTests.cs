namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Exercises security-inventory-dependencies.sh (FR-003) against a small fixture repo
/// under Fixtures/FakeRepo, covering: central package management version resolution
/// (including MSBuild $(Property) references), explicit per-project version overrides,
/// dev/test-only vs. shipped classification (Edge Case §1), and Aspire container
/// resource parsing (default vs. overridden image/tag).
/// </summary>
[TestClass]
public class InventoryTests
{
    private static readonly string FakeRepoRoot = Path.Combine(ScriptRunner.RepoRoot, "tests", "Security.Tooling.UnitTests", "Fixtures", "FakeRepo");

    private string _outPath = null!;

    [TestInitialize]
    public void Setup()
    {
        _outPath = Path.Combine(Path.GetTempPath(), $"dependency-components-{Guid.NewGuid()}.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_outPath))
        {
            File.Delete(_outPath);
        }
    }

    private JsonElement RunInventory()
    {
        var (exitCode, stdout, stderr) = ScriptRunner.Run(
            "security-inventory-dependencies.sh",
            "--repo-root", FakeRepoRoot,
            "--apphost-program-cs", "src/FakeApp.AppHost/Program.cs",
            "--out", _outPath);
        Assert.AreEqual(0, exitCode, stderr);
        return JsonDocument.Parse(File.ReadAllText(_outPath)).RootElement;
    }

    private static JsonElement FindByName(JsonElement components, string name) =>
        components.EnumerateArray().First(c => c.GetProperty("name").GetString() == name);

    [TestMethod]
    public void CentralPackageVersion_ResolvesMSBuildPropertyReference()
    {
        var components = RunInventory();
        var pkg = FindByName(components, "Fake.Central.Package");
        Assert.AreEqual("1.2.3", pkg.GetProperty("version").GetString());
    }

    [TestMethod]
    public void CentralPackageVersion_ResolvesLiteralVersion()
    {
        var components = RunInventory();
        var pkg = FindByName(components, "Fake.Literal.Package");
        Assert.AreEqual("9.9.9", pkg.GetProperty("version").GetString());
    }

    [TestMethod]
    public void PerProjectExplicitVersion_OverridesCentralVersion()
    {
        var components = RunInventory();
        var pkg = FindByName(components, "Fake.Explicit.Override.Package");
        Assert.AreEqual("5.5.5", pkg.GetProperty("version").GetString());
    }

    [TestMethod]
    public void PackageUsedOnlyByTestProject_IsMarkedNotShippedInRuntime()
    {
        var components = RunInventory();
        var pkg = FindByName(components, "Fake.Test.Only.Package");
        Assert.IsFalse(pkg.GetProperty("shipsInRuntime").GetBoolean());
        Assert.AreEqual("FakeProject.UnitTests", pkg.GetProperty("usedBy")[0].GetString());
    }

    [TestMethod]
    public void PackageUsedBySrcProject_IsMarkedShippedInRuntime()
    {
        var components = RunInventory();
        var pkg = FindByName(components, "Fake.Central.Package");
        Assert.IsTrue(pkg.GetProperty("shipsInRuntime").GetBoolean());
    }

    [TestMethod]
    public void AspireContainerResource_WithoutOverride_UsesDefaultImageAndPlaceholderTag()
    {
        var components = RunInventory();
        var redis = FindByName(components, "docker.io/library/redis (fake-redis)");
        Assert.AreEqual("container", redis.GetProperty("ecosystem").GetString());
        Assert.AreEqual("aspire-managed-default", redis.GetProperty("version").GetString());
    }

    [TestMethod]
    public void AspireContainerResource_WithImageAndTagOverride_UsesOverriddenValues()
    {
        var components = RunInventory();
        var postgres = FindByName(components, "example/custom-postgres (fake-postgres)");
        Assert.AreEqual("container", postgres.GetProperty("ecosystem").GetString());
        Assert.AreEqual("16.2", postgres.GetProperty("version").GetString());
    }
}
