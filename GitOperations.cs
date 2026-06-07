using System.Diagnostics;

namespace RepoAnalyzer.Core;

/// <summary>
/// Thin wrapper around the git CLI.
/// All operations go through a temp clone so the user's working tree is never touched.
/// </summary>
public static class GitOperations
{
    // ── Clone ──────────────────────────────────────────────────────────────

    public static async Task<string> CloneAsync(string repoUrl, string? branch, string? token)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"repo-analyzer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var authedUrl = InjectToken(repoUrl, token);
        var branchArg = branch is not null ? $"--branch {branch} " : "";
        var args      = $"clone --depth 1 {branchArg}{authedUrl} {tempDir}";

        Console.WriteLine("📥  Cloning repository…");
        await RunAsync("git", args, workingDir: null, redactInError: authedUrl, safeDisplay: repoUrl);
        Console.WriteLine($"✅  Cloned to temp dir");

        return tempDir;
    }

    // ── Commit & push ──────────────────────────────────────────────────────

    public static async Task CommitAndPushAsync(string repoPath, IEnumerable<string> reportPaths, string? token)
    {
        // Configure a throwaway identity (won't override existing global config)
        await RunAsync("git", $"-C {Q(repoPath)} config user.email \"repo-analyzer[bot]@users.noreply.github.com\"");
        await RunAsync("git", $"-C {Q(repoPath)} config user.name  \"Repo Analyzer Bot\"");

        foreach (var path in reportPaths)
        {
            var rel = Path.GetRelativePath(repoPath, path);
            await RunAsync("git", $"-C {Q(repoPath)} add {Q(rel)}");
            Console.WriteLine($"📎  Staged: {rel}");
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");
        await RunAsync("git", $"-C {Q(repoPath)} commit -m \"chore(reports): automated analysis [{stamp}]\"");

        Console.WriteLine("📤  Pushing report to origin…");
        await RunAsync("git", $"-C {Q(repoPath)} push");
        Console.WriteLine("✅  Committed and pushed.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Inject a PAT into an HTTPS URL: https://TOKEN@github.com/…</summary>
    private static string InjectToken(string url, string? token)
    {
        if (string.IsNullOrEmpty(token) || !url.StartsWith("https://"))
            return url;

        return url.Replace("https://", $"https://{token}@");
    }

    private static async Task RunAsync(
        string  exe,
        string  args,
        string? workingDir     = null,
        string? redactInError  = null,   // replaced with safeDisplay in exceptions
        string? safeDisplay    = null)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            WorkingDirectory       = workingDir ?? "",
        };

        using var proc   = Process.Start(psi) ?? throw new Exception($"Failed to start: {exe}");
        var       stdout = await proc.StandardOutput.ReadToEndAsync();
        var       stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            var msg = $"{exe} {args}\n{stderr}";
            if (redactInError is not null && safeDisplay is not null)
                msg = msg.Replace(redactInError, safeDisplay);
            throw new Exception($"Git command failed (exit {proc.ExitCode}):\n{msg}");
        }
    }

    /// <summary>Quote a path so spaces don't break the shell command string.</summary>
    private static string Q(string path) => $"\"{path}\"";
}
