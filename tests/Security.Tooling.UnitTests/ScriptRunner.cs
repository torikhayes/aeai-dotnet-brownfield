namespace eShop.Security.Tooling.UnitTests;

/// <summary>
/// Invokes the bash scripts under .specify/scripts/bash/ as subprocesses so tests
/// exercise the real CLI contract (see contracts/skill-commands.md) rather than
/// re-implementing the logic in C#.
/// </summary>
internal static class ScriptRunner
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ScriptsDir => Path.Combine(RepoRoot, ".specify", "scripts", "bash");

    public static (int ExitCode, string StdOut, string StdErr) Run(string scriptName, params string[] args)
    {
        var scriptPath = Path.Combine(ScriptsDir, scriptName);
        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(scriptPath);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = Process.Start(psi)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    public static string StartRun(string findingsDir, string capabilities, string scope = "full", string trigger = "manual")
    {
        var (exitCode, stdout, stderr) = Run(
            "security-findings-store.sh",
            "--findings-dir", findingsDir,
            "start-run",
            "--capabilities", capabilities,
            "--scope", scope,
            "--trigger", trigger);
        Assert.AreEqual(0, exitCode, $"start-run failed: {stderr}");
        return JsonDocument.Parse(stdout).RootElement.GetProperty("id").GetString()!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eShop.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (eShop.slnx not found in any ancestor directory).");
    }
}
