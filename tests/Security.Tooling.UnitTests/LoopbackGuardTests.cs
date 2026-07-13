namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Exercises security-assert-loopback.sh (FR-006, SC-004): the adversarial review
/// capability must refuse to target anything but a loopback address, checked before
/// any scenario executes.
/// </summary>
[TestClass]
public class LoopbackGuardTests
{
    [DataTestMethod]
    [DataRow("http://localhost:8080")]
    [DataRow("http://127.0.0.1:9999")]
    [DataRow("http://[::1]:5000")]
    public void LoopbackUrl_IsAccepted(string url)
    {
        var (exitCode, _, stderr) = ScriptRunner.Run("security-assert-loopback.sh", url);
        Assert.AreEqual(0, exitCode, stderr);
    }

    [DataTestMethod]
    [DataRow("https://example.com")]
    [DataRow("http://192.168.1.5:8080")]
    [DataRow("http://staging.internal.example.com")]
    public void NonLoopbackUrl_IsRejected(string url)
    {
        var (exitCode, _, stderr) = ScriptRunner.Run("security-assert-loopback.sh", url);
        Assert.AreNotEqual(0, exitCode);
        StringAssert.Contains(stderr, "does not resolve to a loopback address");
    }
}
